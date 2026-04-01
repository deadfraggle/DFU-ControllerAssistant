using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TravelMapAssist
    {
        private FieldInfo fiFindButton;
        private FieldInfo fiAtButton;
        private FieldInfo fiHorizontalArrowButton;
        private FieldInfo fiVerticalArrowButton;
        private FieldInfo fiExitButton;
        private FieldInfo fiDungeonsFilterButton;
        private FieldInfo fiTemplesFilterButton;
        private FieldInfo fiHomesFilterButton;
        private FieldInfo fiTownsFilterButton;
        private FieldInfo fiMapIndex;
        private FieldInfo fiSelectedRegionMapNames;
        private FieldInfo fiOffsetLookup;

        private RegionNeighborIndicatorOverlay regionViewNeighborIndicator;

        private struct VisibleNeighborInfo
        {
            public int region;
            public Rect rect;
            public float centerX;
            public float centerY;
            public int pixelCount;
        }

        private List<VisibleNeighborInfo> visibleNeighbors = new List<VisibleNeighborInfo>();
        private bool inNeighborMode = false;
        private bool neighborIterationReverse = false;
        private int neighborSelectedIndex = 0;

        private int cachedNeighborRegion = -2;
        private int cachedNeighborMapIndex = -2;


        private DefaultSelectorBoxHost regionViewButtonSelector;
        private SelectorButtonInfo[] regionViewButtons;

        private int buttonSelected = RegionFindButton;
        private bool buttonsInitialized = false;
        private bool suppressNeighborInputOnce = false;

        private const int RegionFindButton = 2000;
        private const int RegionImAtButton = 2001;
        private const int RegionDungeonsButton = 2002;
        private const int RegionTemplesButton = 2003;
        private const int RegionHomesButton = 2004;
        private const int RegionTownsButton = 2005;
        private const int RegionShiftLeftRightButton = 2006;
        private const int RegionShiftUpDownButton = 2007;
        private const int RegionExitButton = 2008;
        const int MIN_PIXEL_COUNT = 40; // tweak to taste

        partial void CacheRegionViewReflection(System.Type type)
        {
            fiFindButton = CacheField(type, "findButton");
            fiAtButton = CacheField(type, "atButton");
            fiExitButton = CacheField(type, "exitButton");
            fiDungeonsFilterButton = CacheField(type, "dungeonsFilterButton");
            fiTemplesFilterButton = CacheField(type, "templesFilterButton");
            fiHomesFilterButton = CacheField(type, "homesFilterButton");
            fiTownsFilterButton = CacheField(type, "townsFilterButton");
            fiHorizontalArrowButton = CacheField(type, "horizontalArrowButton");
            fiVerticalArrowButton = CacheField(type, "verticalArrowButton");
            fiMapIndex = CacheField(type, "mapIndex");
            fiSelectedRegionMapNames = CacheField(type, "selectedRegionMapNames");
            fiOffsetLookup = CacheField(type, "offsetLookup");
        }

        partial void OnOpenedRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            buttonSelected = RegionFindButton;
            buttonsInitialized = true;
            RebuildRegionViewButtons(menuWindow);
            RebuildVisibleNeighbors(menuWindow);
            inNeighborMode = false;
            neighborSelectedIndex = 0;
            suppressNeighborInputOnce = false;
            neighborIterationReverse = false;
        }

        partial void TickRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            if (!buttonsInitialized)
                return;

            RebuildRegionViewButtons(menuWindow);
            RebuildVisibleNeighbors(menuWindow);

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (inNeighborMode)
            {
                RefreshRegionViewNeighborSelector(menuWindow);

                if (suppressNeighborInputOnce)
                {
                    suppressNeighborInputOnce = false;
                    return;
                }

                if (dir != ControllerManager.StickDir8.None)
                {
                    if (IsNorth(dir))
                    {
                        if (visibleNeighbors.Count > 0)
                        {
                            if (!neighborIterationReverse)
                                neighborSelectedIndex++;
                            else
                                neighborSelectedIndex--;

                            if (!neighborIterationReverse && neighborSelectedIndex >= visibleNeighbors.Count)
                            {
                                ExitNeighborMode();
                                buttonSelected = RegionExitButton;
                                RefreshRegionViewButtonSelector(menuWindow);
                            }
                            else if (neighborIterationReverse && neighborSelectedIndex < 0)
                            {
                                ExitNeighborMode();
                                buttonSelected = RegionFindButton;
                                RefreshRegionViewButtonSelector(menuWindow);
                            }
                            else
                            {
                                RefreshRegionViewNeighborSelector(menuWindow);
                            }
                        }

                        return;
                    }

                    if (IsSouth(dir))
                    {
                        if (!neighborIterationReverse)
                            neighborSelectedIndex--;
                        else
                            neighborSelectedIndex++;

                        if (!neighborIterationReverse && neighborSelectedIndex < 0)
                        {
                            ExitNeighborMode();
                            buttonSelected = RegionFindButton;
                            RefreshRegionViewButtonSelector(menuWindow);
                        }
                        else if (neighborIterationReverse && neighborSelectedIndex >= visibleNeighbors.Count)
                        {
                            ExitNeighborMode();
                            buttonSelected = RegionExitButton;
                            RefreshRegionViewButtonSelector(menuWindow);
                        }
                        else
                        {
                            RefreshRegionViewNeighborSelector(menuWindow);
                        }

                        return;
                    }
                }

                if (cm.Action1Released)
                {
                    ActivateSelectedNeighbor(menuWindow);
                    return;
                }

                return;
            }

            RefreshRegionViewButtonSelector(menuWindow);

            if (dir != ControllerManager.StickDir8.None)
            {
                if (IsNorth(dir) && visibleNeighbors.Count > 0)
                {
                    bool canEnterNeighbors =
                        buttonSelected == RegionFindButton ||
                        buttonSelected == RegionDungeonsButton ||
                        buttonSelected == RegionHomesButton ||
                        buttonSelected == RegionExitButton ||
                        buttonSelected == RegionShiftLeftRightButton ||
                        buttonSelected == RegionShiftUpDownButton;

                    if (canEnterNeighbors)
                    {
                        int startIndex = 0;
                        bool reverse = false;

                        if (buttonSelected == RegionExitButton)
                        {
                            startIndex = visibleNeighbors.Count - 1;
                            reverse = true;
                        }

                        EnterNeighborMode(menuWindow, startIndex, reverse);
                        return;
                    }
                }

                int next = GetRegionViewDirectionalTarget(buttonSelected, dir);
                if (next >= 0 && next != buttonSelected)
                {
                    buttonSelected = next;
                    RefreshRegionViewButtonSelector(menuWindow);
                }
            }

            if (cm.Action1Released)
            {
                ActivateRegionViewButton(menuWindow);
                RebuildRegionViewButtons(menuWindow);
                RebuildVisibleNeighbors(menuWindow);
                RefreshRegionViewButtonSelector(menuWindow);
            }
        }

        partial void ResetRegionViewState()
        {
            buttonSelected = RegionFindButton;
            buttonsInitialized = false;
            regionViewButtons = null;

            inNeighborMode = false;
            neighborSelectedIndex = 0;
            suppressNeighborInputOnce = false;
            neighborIterationReverse = false;
            visibleNeighbors.Clear();
            cachedNeighborRegion = -2;
            cachedNeighborMapIndex = -2;

            if (regionViewButtonSelector != null)
            {
                regionViewButtonSelector.Destroy();
                regionViewButtonSelector = null;
            }

            if (regionViewNeighborIndicator != null)
            {
                regionViewNeighborIndicator.Destroy();
                regionViewNeighborIndicator = null;
            }
        }

        private void RebuildRegionViewButtons(DaggerfallTravelMapWindow menuWindow)
        {
            bool hasLeftRight = IsRegionViewButtonEnabled(fiHorizontalArrowButton, menuWindow);
            bool hasUpDown = IsRegionViewButtonEnabled(fiVerticalArrowButton, menuWindow);

            int homesEast = hasLeftRight
                ? RegionShiftLeftRightButton
                : (hasUpDown ? RegionShiftUpDownButton : RegionExitButton);

            int townsEast = hasLeftRight
                ? RegionShiftLeftRightButton
                : (hasUpDown ? RegionShiftUpDownButton : RegionExitButton);

            int leftRightEast = hasUpDown ? RegionShiftUpDownButton : RegionExitButton;
            int upDownWest = hasLeftRight ? RegionShiftLeftRightButton : RegionHomesButton;

            regionViewButtons = new SelectorButtonInfo[]
            {
                new SelectorButtonInfo
                {
                    id = RegionFindButton,
                    rect = new Rect(2.7f, 174.6f, 45.6f, 11.5f),
                    E = RegionDungeonsButton,
                    SE = RegionTemplesButton,
                    S = RegionImAtButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionImAtButton,
                    rect = new Rect(2.7f, 185.8f, 45.6f, 11.5f),
                    N = RegionFindButton,
                    NE = RegionDungeonsButton,
                    E = RegionTemplesButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionDungeonsButton,
                    rect = new Rect(49.6f, 174.5f, 99.8f, 11.5f),
                    E = RegionHomesButton,
                    SE = RegionTownsButton,
                    S = RegionTemplesButton,
                    W = RegionFindButton,
                    SW = RegionImAtButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionTemplesButton,
                    rect = new Rect(49.6f, 185.6f, 99.8f, 11.5f),
                    N = RegionDungeonsButton,
                    NE = RegionHomesButton,
                    E = RegionTownsButton,
                    W = RegionImAtButton,
                    NW = RegionFindButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionHomesButton,
                    rect = new Rect(148.5f, 174.5f, 80.8f, 11.5f),
                    E = homesEast,
                    S = RegionTownsButton,
                    SW = RegionTemplesButton,
                    W = RegionDungeonsButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionTownsButton,
                    rect = new Rect(148.5f, 185.6f, 80.8f, 11.5f),
                    N = RegionHomesButton,
                    W = RegionTemplesButton,
                    NW = RegionDungeonsButton,
                    E = townsEast,
                },
                new SelectorButtonInfo
                {
                    id = RegionShiftLeftRightButton,
                    rect = new Rect(230.9f, 175.6f, 22.3f, 13.3f),
                    W = RegionHomesButton,
                    E = leftRightEast,
                },
                new SelectorButtonInfo
                {
                    id = RegionShiftUpDownButton,
                    rect = new Rect(253.8f, 175.6f, 22.3f, 13.3f),
                    W = upDownWest,
                    E = RegionExitButton,
                },
                new SelectorButtonInfo
                {
                    id = RegionExitButton,
                    rect = new Rect(277.8f, 174.0f, 39.7f, 23.5f),
                    W = hasUpDown
                        ? RegionShiftUpDownButton
                        : (hasLeftRight ? RegionShiftLeftRightButton : RegionHomesButton),
                },
            };

            if (!HasRegionViewButtonId(buttonSelected))
            {
                buttonSelected = RegionFindButton;
            }
        }

        private bool HasRegionViewButtonId(int id)
        {
            if (regionViewButtons == null)
                return false;

            for (int i = 0; i < regionViewButtons.Length; i++)
            {
                if (regionViewButtons[i].id == id)
                    return true;
            }

            return false;
        }

        private bool IsRegionViewButtonEnabled(FieldInfo fi, DaggerfallTravelMapWindow menuWindow)
        {
            if (fi == null || menuWindow == null)
                return false;

            BaseScreenComponent c = fi.GetValue(menuWindow) as BaseScreenComponent;
            return c != null && c.Enabled;
        }

        private SelectorButtonInfo GetRegionViewButtonInfo(int id)
        {
            if (regionViewButtons == null)
                return new SelectorButtonInfo { id = -1 };

            for (int i = 0; i < regionViewButtons.Length; i++)
            {
                if (regionViewButtons[i].id == id)
                    return regionViewButtons[i];
            }

            return new SelectorButtonInfo { id = -1 };
        }

        private void RefreshRegionViewButtonSelector(DaggerfallTravelMapWindow menuWindow)
        {
            if (inNeighborMode)
                return;

            Panel currentPanel = fiPanelRenderWindow != null ? fiPanelRenderWindow.GetValue(menuWindow) as Panel : null;
            if (currentPanel == null)
                return;

            SelectorButtonInfo info = GetRegionViewButtonInfo(buttonSelected);
            if (info.id < 0)
                return;

            if (info.id == RegionShiftLeftRightButton && !IsRegionViewButtonEnabled(fiHorizontalArrowButton, menuWindow))
                return;

            if (info.id == RegionShiftUpDownButton && !IsRegionViewButtonEnabled(fiVerticalArrowButton, menuWindow))
                return;

            if (regionViewButtonSelector == null)
                regionViewButtonSelector = new DefaultSelectorBoxHost();

            regionViewButtonSelector.ShowAtNativeRect(
                currentPanel,
                info.rect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private int GetRegionViewDirectionalTarget(int current, ControllerManager.StickDir8 dir)
        {
            SelectorButtonInfo info = GetRegionViewButtonInfo(current);
            if (info.id < 0)
                return -1;

            return TargetFromRect(dir, info.N, info.NE, info.E, info.SE, info.S, info.SW, info.W, info.NW);
        }

        private void ActivateRegionViewButton(DaggerfallTravelMapWindow menuWindow)
        {
            BaseScreenComponent target = null;

            switch (buttonSelected)
            {
                case RegionFindButton:
                    target = fiFindButton != null ? fiFindButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionImAtButton:
                    target = fiAtButton != null ? fiAtButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionDungeonsButton:
                    target = fiDungeonsFilterButton != null ? fiDungeonsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionTemplesButton:
                    target = fiTemplesFilterButton != null ? fiTemplesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionHomesButton:
                    target = fiHomesFilterButton != null ? fiHomesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionTownsButton:
                    target = fiTownsFilterButton != null ? fiTownsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionShiftLeftRightButton:
                    target = fiHorizontalArrowButton != null ? fiHorizontalArrowButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionShiftUpDownButton:
                    target = fiVerticalArrowButton != null ? fiVerticalArrowButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case RegionExitButton:
                    target = fiExitButton != null ? fiExitButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
            }

            Button button = target as Button;
            if (button != null && button.Enabled)
                button.TriggerMouseClick();
        }

        private void EnterNeighborMode(DaggerfallTravelMapWindow menuWindow, int index, bool reverse)
        {
            if (visibleNeighbors == null || visibleNeighbors.Count == 0)
                return;

            inNeighborMode = true;
            neighborSelectedIndex = Mathf.Clamp(index, 0, visibleNeighbors.Count - 1);
            suppressNeighborInputOnce = true;
            neighborIterationReverse = reverse;

            if (regionViewButtonSelector != null)
            {
                regionViewButtonSelector.Destroy();
                regionViewButtonSelector = null;
            }

            RefreshRegionViewNeighborSelector(menuWindow);
        }

        private void ExitNeighborMode()
        {
            inNeighborMode = false;
            neighborSelectedIndex = 0;
            suppressNeighborInputOnce = false;
            neighborIterationReverse = false;

            if (regionViewNeighborIndicator != null)
            {
                regionViewNeighborIndicator.Destroy();
                regionViewNeighborIndicator = null;
            }

        }

        private void RefreshRegionViewNeighborSelector(DaggerfallTravelMapWindow menuWindow)
        {
            if (!inNeighborMode || visibleNeighbors == null || visibleNeighbors.Count == 0)
                return;

            if (neighborSelectedIndex < 0 || neighborSelectedIndex >= visibleNeighbors.Count)
                return;

            Panel currentPanel = fiPanelRenderWindow != null ? fiPanelRenderWindow.GetValue(menuWindow) as Panel : null;
            if (currentPanel == null)
                return;

            if (regionViewNeighborIndicator == null)
                regionViewNeighborIndicator = new RegionNeighborIndicatorOverlay(currentPanel);

            float now = Time.realtimeSinceStartup;
            float pulse = 1.0f + Mathf.Sin(now * 6f) * 0.22f;

            regionViewNeighborIndicator.ShowAtNativeRect(
                visibleNeighbors[neighborSelectedIndex].rect,
                pulse);
        }

        private void ActivateSelectedNeighbor(DaggerfallTravelMapWindow menuWindow)
        {
            if (!inNeighborMode || visibleNeighbors == null || visibleNeighbors.Count == 0)
                return;

            if (neighborSelectedIndex < 0 || neighborSelectedIndex >= visibleNeighbors.Count)
                return;

            int nextRegion = visibleNeighbors[neighborSelectedIndex].region;
            ExitNeighborMode();

            if (miOpenRegionPanel != null)
                miOpenRegionPanel.Invoke(menuWindow, new object[] { nextRegion });
        }

        private bool IsNorth(ControllerManager.StickDir8 dir)
        {
            return dir == ControllerManager.StickDir8.N ||
                   dir == ControllerManager.StickDir8.NE ||
                   dir == ControllerManager.StickDir8.NW;
        }

        private bool IsSouth(ControllerManager.StickDir8 dir)
        {
            return dir == ControllerManager.StickDir8.S ||
                   dir == ControllerManager.StickDir8.SE ||
                   dir == ControllerManager.StickDir8.SW;
        }
        private void RebuildVisibleNeighbors(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            int mapIndex = fiMapIndex != null ? (int)fiMapIndex.GetValue(menuWindow) : -1;

            if (selectedRegion < 0 || mapIndex < 0)
            {
                visibleNeighbors.Clear();
                cachedNeighborRegion = selectedRegion;
                cachedNeighborMapIndex = mapIndex;
                return;
            }

            if (selectedRegion == cachedNeighborRegion && mapIndex == cachedNeighborMapIndex)
                return;

            cachedNeighborRegion = selectedRegion;
            cachedNeighborMapIndex = mapIndex;

            visibleNeighbors.Clear();

            string[] mapNames = fiSelectedRegionMapNames != null
                ? fiSelectedRegionMapNames.GetValue(menuWindow) as string[]
                : null;

            if (mapNames == null || mapIndex < 0 || mapIndex >= mapNames.Length)
                return;

            Dictionary<string, Vector2> offsetLookup = fiOffsetLookup != null
                ? fiOffsetLookup.GetValue(menuWindow) as Dictionary<string, Vector2>
                : null;

            if (offsetLookup == null)
                return;

            string mapName = mapNames[mapIndex];
            Vector2 origin;
            if (!offsetLookup.TryGetValue(mapName, out origin))
                return;

            Dictionary<int, NeighborAccumulator> accumulators = new Dictionary<int, NeighborAccumulator>();

            for (int y = 0; y < regionTextureHeight; y++)
            {
                for (int x = 0; x < regionTextureWidth; x++)
                {
                    int sampleRegion = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(
                        (int)origin.x + x,
                        (int)origin.y + y) - 128;

                    if (sampleRegion < 0 || sampleRegion == selectedRegion)
                        continue;

                    NeighborAccumulator acc;
                    if (!accumulators.TryGetValue(sampleRegion, out acc))
                    {
                        acc = new NeighborAccumulator();
                        acc.minX = x;
                        acc.maxX = x;
                        acc.minY = y;
                        acc.maxY = y;
                        acc.sumX = x;
                        acc.sumY = y;
                        acc.count = 1;
                    }
                    else
                    {
                        if (x < acc.minX) acc.minX = x;
                        if (x > acc.maxX) acc.maxX = x;
                        if (y < acc.minY) acc.minY = y;
                        if (y > acc.maxY) acc.maxY = y;
                        acc.sumX += x;
                        acc.sumY += y;
                        acc.count++;
                    }

                    accumulators[sampleRegion] = acc;
                }
            }

            foreach (KeyValuePair<int, NeighborAccumulator> kvp in accumulators)
            {
                NeighborAccumulator acc = kvp.Value;

                float cx = (float)acc.sumX / acc.count;
                float cy = (float)acc.sumY / acc.count;

                Rect rect = new Rect(
                    cx - 9f,
                    cy + regionPanelOffset - 5f,
                    18f,
                    10f);

                VisibleNeighborInfo info = new VisibleNeighborInfo();
                info.region = kvp.Key;
                info.rect = rect;
                info.centerX = cx;
                info.centerY = cy;
                info.pixelCount = acc.count;

                if (acc.count >= MIN_PIXEL_COUNT)
                {
                    visibleNeighbors.Add(info);
                }
            }

            Vector2 center = new Vector2(regionTextureWidth * 0.5f, regionTextureHeight * 0.5f);

            visibleNeighbors.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.centerY - center.y, a.centerX - center.x);
                float angleB = Mathf.Atan2(b.centerY - center.y, b.centerX - center.x);
                return angleA.CompareTo(angleB);
            });

            if (inNeighborMode)
            {
                if (visibleNeighbors.Count == 0)
                {
                    ExitNeighborMode();
                    buttonSelected = RegionFindButton;
                }
                else
                {
                    neighborSelectedIndex = Mathf.Clamp(neighborSelectedIndex, 0, visibleNeighbors.Count - 1);
                }
            }
        }

        private struct NeighborAccumulator
        {
            public int minX;
            public int maxX;
            public int minY;
            public int maxY;
            public int sumX;
            public int sumY;
            public int count;
        }

        private class RegionNeighborIndicatorOverlay
        {
            private const float NativeWidth = 320f;
            private const float NativeHeight = 200f;

            private readonly Panel parentPanel;

            private Panel root;
            private TextLabel label;

            private Rect lastNativeRect;

            public bool IsBuilt
            {
                get { return root != null; }
            }

            public RegionNeighborIndicatorOverlay(Panel parentPanel)
            {
                this.parentPanel = parentPanel;
            }

            public bool IsAttached()
            {
                return root != null && root.Parent == parentPanel;
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parent = root.Parent as Panel;
                    if (parent != null)
                        parent.Components.Remove(root);
                }

                root = null;
                label = null;
            }

            public void ShowAtNativeRect(Rect nativeRect, float pulseScale)
            {
                lastNativeRect = nativeRect;

                Rect panelRect = NativeToPanelRect(nativeRect);

                float x = panelRect.x + panelRect.width * 0.5f;
                float y = panelRect.y + panelRect.height * 0.5f;

                if (root == null)
                {
                    root = DaggerfallUI.AddPanel(new Rect(x, y, 1f, 1f), parentPanel);
                    root.BackgroundColor = Color.clear;
                    root.Enabled = true;

                    label = DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, Vector2.zero, ">>>", root);
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.TextColor = new Color(1f, 0.1f, 0.1f, 1f);
                    label.ShadowColor = new Color(0f, 0f, 0f, 1f);
                }
                else
                {
                    root.Position = new Vector2(x, y);
                    root.Size = new Vector2(1f, 1f);
                    root.BackgroundColor = Color.clear;
                    root.Enabled = true;
                }

                if (label != null)
                {
                    float uiScale = parentPanel.Size.x / NativeWidth;
                    float t = Mathf.InverseLerp(2.0f, 12.0f, uiScale);

                    // Much larger base scale than before
                    float baseTextScale = Mathf.Lerp(7.0f, 14.0f, t);
                    float textScale = baseTextScale * pulseScale;

                    label.TextScale = textScale;
                    label.Text = ">>>";
                    label.TextColor = new Color(1f, 0.1f, 0.1f, 1f);
                    label.ShadowColor = new Color(0f, 0f, 0f, 1f);

                    float scaledFontHeight = 7f * textScale;

                    // Center visually around the indicator point
                    label.Position = new Vector2(
                        -12f * textScale,
                        -scaledFontHeight * 0.52f
                    );
                }
            }


            private Rect NativeToPanelRect(Rect nativeRect)
            {
                if (parentPanel == null)
                    return nativeRect;

                float parentWidth = parentPanel.Size.x;
                float parentHeight = parentPanel.Size.y;

                float scaleX = parentWidth / NativeWidth;
                float scaleY = parentHeight / NativeHeight;
                float scale = Mathf.Min(scaleX, scaleY);

                float scaledNativeWidth = NativeWidth * scale;
                float scaledNativeHeight = NativeHeight * scale;

                float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
                float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

                return new Rect(
                    offsetX + nativeRect.x * scale,
                    offsetY + nativeRect.y * scale,
                    nativeRect.width * scale,
                    nativeRect.height * scale
                );
            }
        }

    }
}