using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class SpellIconPickerAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Selector
        private DefaultSelectorBoxHost selectorHost;
        private bool selectorInitializedThisOpen = false;
        private int selectorInitStableTicks = 0;
        private float selectorInitLastWidth = -1;
        private float selectorInitLastHeight = -1;

        private FieldInfo fiScrollingPanel;
        private FieldInfo fiScroller;
        private FieldInfo fiSelectedIcon;
        private FieldInfo fiMainPanel;

        private object scrollingPanelObject;
        private object scrollerObject;

        private List<Panel> iconPanels = new List<Panel>();
        private int iconSelected = -1;

        private const float iconNativeSelectorPad = 0.4f;
        private const float iconSelectorOffsetX = 23.0f;
        private const float iconSelectorOffsetY = 10.0f;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        //private AnchorEditor editor;

        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is SpellIconPickerWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            SpellIconPickerWindow menuWindow = top as SpellIconPickerWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;
            closeDeferred = false;

            DestroyLegend();
            DestroySelectorBox();

            legendVisible = false;
            panelRenderWindow = null;

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;

            scrollingPanelObject = null;
            scrollerObject = null;
            iconPanels.Clear();
            iconSelected = -1;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(SpellIconPickerWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            // One-time delayed selector attach for windows whose panel is not ready in OnOpened().
            if (!selectorInitializedThisOpen)
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                {
                    float w = currentPanel.Rectangle.width;
                    float h = currentPanel.Rectangle.height;

                    if (w > 0 && h > 0)
                    {
                        if (w == selectorInitLastWidth && h == selectorInitLastHeight)
                            selectorInitStableTicks++;
                        else
                            selectorInitStableTicks = 1;

                        selectorInitLastWidth = w;
                        selectorInitLastHeight = h;

                        if (selectorInitStableTicks >= 2)
                        {
                            RebuildIconPanelList(menuWindow);
                            if (iconSelected < 0)
                                SelectInitialIcon(menuWindow);

                            RefreshSelectorToCurrentIcon(menuWindow);
                            selectorInitializedThisOpen = true;
                        }
                    }
                }
            }

            if (selectorInitializedThisOpen)
            {
                RebuildIconPanelList(menuWindow);

                if (iconSelected >= iconPanels.Count)
                    iconSelected = iconPanels.Count - 1;

                if (iconSelected >= 0)
                    RefreshSelectorToCurrentIcon(menuWindow);
            }

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
            {
                ScrollByRows(menuWindow, -1);
                return;
            }

            if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
            {
                ScrollByRows(menuWindow, +1);
                return;
            }

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelected(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
                //editor.Toggle();
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        // =========================
        // Assist action helpers
        // =========================

        private Panel GetCurrentRenderPanel(SpellIconPickerWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RebuildIconPanelList(SpellIconPickerWindow menuWindow)
        {
            iconPanels.Clear();

            if (scrollingPanelObject == null && fiScrollingPanel != null)
                scrollingPanelObject = fiScrollingPanel.GetValue(menuWindow);

            Panel scrollingPanel = scrollingPanelObject as Panel;
            if (scrollingPanel == null)
                return;

            foreach (BaseScreenComponent component in scrollingPanel.Components)
            {
                Panel panel = component as Panel;
                if (panel == null)
                    continue;

                if (panel.Tag == null)
                    continue;

                // Icon panels have SpellIcon tags. Header labels are TextLabels, not Panels.
                iconPanels.Add(panel);
            }
        }

        private void SelectInitialIcon(SpellIconPickerWindow menuWindow)
        {
            if (iconPanels.Count == 0)
            {
                iconSelected = -1;
                return;
            }

            iconSelected = 0;
            SyncWindowSelectedIconToCurrent(menuWindow);
        }

        private Rect GetSelectorNativeRectForIcon(SpellIconPickerWindow menuWindow, Panel iconPanel)
        {
            if (menuWindow == null || iconPanel == null)
                return new Rect();

            Panel mainPanel = fiMainPanel != null ? fiMainPanel.GetValue(menuWindow) as Panel : null;
            Panel scrollingPanel = fiScrollingPanel != null ? fiScrollingPanel.GetValue(menuWindow) as Panel : null;

            Vector2 mainPos = mainPanel != null ? mainPanel.Position : Vector2.zero;
            Vector2 scrollingPos = scrollingPanel != null ? scrollingPanel.Position : Vector2.zero;

            int scrollIndex = 0;
            int scrollTransform = 22; // matches vanilla iconSpacing/ScrollTransform default

            if (scrollingPanelObject == null && fiScrollingPanel != null)
                scrollingPanelObject = fiScrollingPanel.GetValue(menuWindow);

            if (scrollingPanelObject != null)
            {
                PropertyInfo piPanelScrollIndex = scrollingPanelObject.GetType().GetProperty("ScrollIndex");
                PropertyInfo piScrollTransform = scrollingPanelObject.GetType().GetProperty("ScrollTransform");

                if (piPanelScrollIndex != null)
                    scrollIndex = (int)piPanelScrollIndex.GetValue(scrollingPanelObject, null);

                if (piScrollTransform != null)
                    scrollTransform = (int)piScrollTransform.GetValue(scrollingPanelObject, null);
            }

            Vector2 pos = iconPanel.Position;
            Vector2 size = iconPanel.Size;

            float drawX = mainPos.x + scrollingPos.x + pos.x + iconSelectorOffsetX;
            float drawY = mainPos.y + scrollingPos.y + pos.y - (scrollIndex * scrollTransform) + iconSelectorOffsetY;

            return new Rect(
                drawX - iconNativeSelectorPad,
                drawY - iconNativeSelectorPad,
                size.x + iconNativeSelectorPad * 2f,
                size.y + iconNativeSelectorPad * 2f
            );
        }

        private void RefreshSelectorToCurrentIcon(SpellIconPickerWindow menuWindow)
        {
            if (iconSelected < 0 || iconSelected >= iconPanels.Count)
                return;

            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                GetSelectorNativeRectForIcon(menuWindow, iconPanels[iconSelected]),
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(SpellIconPickerWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
                selectorHost.Destroy();
        }

        private void SyncWindowSelectedIconToCurrent(SpellIconPickerWindow menuWindow)
        {
            if (fiSelectedIcon == null || menuWindow == null)
                return;

            if (iconSelected < 0 || iconSelected >= iconPanels.Count)
            {
                fiSelectedIcon.SetValue(menuWindow, null);
                return;
            }

            fiSelectedIcon.SetValue(menuWindow, iconPanels[iconSelected].Tag);
        }

        private void TryMoveSelector(SpellIconPickerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (iconSelected < 0 || iconSelected >= iconPanels.Count)
                return;

            int next = FindBestDirectionalIcon(iconSelected, dir);
            if (next < 0 || next == iconSelected)
                return;

            iconSelected = next;
            SyncWindowSelectedIconToCurrent(menuWindow);
            EnsureIconVisible(menuWindow, iconSelected);
            RefreshSelectorToCurrentIcon(menuWindow);
        }

        private int FindBestDirectionalIcon(int currentIndex, ControllerManager.StickDir8 dir)
        {
            if (currentIndex < 0 || currentIndex >= iconPanels.Count)
                return -1;

            Panel current = iconPanels[currentIndex];
            Vector2 c = GetPanelCenter(current);

            int bestIndex = -1;
            float bestPrimary = float.MaxValue;
            float bestSecondary = float.MaxValue;

            for (int i = 0; i < iconPanels.Count; i++)
            {
                if (i == currentIndex)
                    continue;

                Vector2 p = GetPanelCenter(iconPanels[i]);
                float dx = p.x - c.x;
                float dy = p.y - c.y;

                bool accept = false;
                float primary = float.MaxValue;
                float secondary = float.MaxValue;

                switch (dir)
                {
                    case ControllerManager.StickDir8.N:
                        accept = dy < 0f;
                        primary = -dy;
                        secondary = Mathf.Abs(dx);
                        break;
                    case ControllerManager.StickDir8.S:
                        accept = dy > 0f;
                        primary = dy;
                        secondary = Mathf.Abs(dx);
                        break;
                    case ControllerManager.StickDir8.W:
                        accept = dx < 0f;
                        primary = -dx;
                        secondary = Mathf.Abs(dy);
                        break;
                    case ControllerManager.StickDir8.E:
                        accept = dx > 0f;
                        primary = dx;
                        secondary = Mathf.Abs(dy);
                        break;
                    case ControllerManager.StickDir8.NW:
                        accept = dx < 0f && dy < 0f;
                        primary = (-dx) + (-dy);
                        secondary = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy));
                        break;
                    case ControllerManager.StickDir8.NE:
                        accept = dx > 0f && dy < 0f;
                        primary = dx + (-dy);
                        secondary = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy));
                        break;
                    case ControllerManager.StickDir8.SW:
                        accept = dx < 0f && dy > 0f;
                        primary = (-dx) + dy;
                        secondary = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy));
                        break;
                    case ControllerManager.StickDir8.SE:
                        accept = dx > 0f && dy > 0f;
                        primary = dx + dy;
                        secondary = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy));
                        break;
                }

                if (!accept)
                    continue;

                if (primary < bestPrimary || (Mathf.Approximately(primary, bestPrimary) && secondary < bestSecondary))
                {
                    bestPrimary = primary;
                    bestSecondary = secondary;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private Vector2 GetPanelCenter(Panel panel)
        {
            return new Vector2(
                panel.Position.x + panel.Size.x * 0.5f,
                panel.Position.y + panel.Size.y * 0.5f
            );
        }

        private void ScrollByRows(SpellIconPickerWindow menuWindow, int delta)
        {
            if (scrollerObject == null && fiScroller != null)
                scrollerObject = fiScroller.GetValue(menuWindow);

            if (scrollingPanelObject == null && fiScrollingPanel != null)
                scrollingPanelObject = fiScrollingPanel.GetValue(menuWindow);

            if (scrollerObject == null || scrollingPanelObject == null)
                return;

            PropertyInfo piScrollIndex = scrollerObject.GetType().GetProperty("ScrollIndex");
            PropertyInfo piPanelScrollIndex = scrollingPanelObject.GetType().GetProperty("ScrollIndex");

            if (piScrollIndex == null || piPanelScrollIndex == null)
                return;

            int current = (int)piScrollIndex.GetValue(scrollerObject, null);
            int next = current + delta;

            piScrollIndex.SetValue(scrollerObject, next, null);
            int clamped = (int)piScrollIndex.GetValue(scrollerObject, null);
            piPanelScrollIndex.SetValue(scrollingPanelObject, clamped, null);

            RefreshSelectorToCurrentIcon(menuWindow);
        }

        private void EnsureIconVisible(SpellIconPickerWindow menuWindow, int index)
        {
            if (index < 0 || index >= iconPanels.Count)
                return;

            if (scrollerObject == null && fiScroller != null)
                scrollerObject = fiScroller.GetValue(menuWindow);

            if (scrollingPanelObject == null && fiScrollingPanel != null)
                scrollingPanelObject = fiScrollingPanel.GetValue(menuWindow);

            if (scrollerObject == null || scrollingPanelObject == null)
                return;

            PropertyInfo piScrollIndex = scrollerObject.GetType().GetProperty("ScrollIndex");
            PropertyInfo piDisplayUnits = scrollerObject.GetType().GetProperty("DisplayUnits");
            PropertyInfo piPanelScrollIndex = scrollingPanelObject.GetType().GetProperty("ScrollIndex");
            PropertyInfo piScrollTransform = scrollingPanelObject.GetType().GetProperty("ScrollTransform");

            if (piScrollIndex == null || piDisplayUnits == null || piPanelScrollIndex == null || piScrollTransform == null)
                return;

            int scrollIndex = (int)piScrollIndex.GetValue(scrollerObject, null);
            int displayUnits = (int)piDisplayUnits.GetValue(scrollerObject, null);
            int scrollTransform = (int)piScrollTransform.GetValue(scrollingPanelObject, null);

            Panel icon = iconPanels[index];
            int iconRow = Mathf.RoundToInt(icon.Position.y / scrollTransform);

            int newScroll = scrollIndex;

            if (iconRow < scrollIndex)
                newScroll = iconRow;
            else if (iconRow > scrollIndex + displayUnits - 1)
                newScroll = iconRow - (displayUnits - 1);

            if (newScroll != scrollIndex)
            {
                piScrollIndex.SetValue(scrollerObject, newScroll, null);
                int clamped = (int)piScrollIndex.GetValue(scrollerObject, null);
                piPanelScrollIndex.SetValue(scrollingPanelObject, clamped, null);
            }
        }

        private void ActivateSelected(SpellIconPickerWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (iconSelected < 0 || iconSelected >= iconPanels.Count)
                return;

            SyncWindowSelectedIconToCurrent(menuWindow);
            menuWindow.CloseWindow();
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(SpellIconPickerWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            scrollingPanelObject = fiScrollingPanel != null ? fiScrollingPanel.GetValue(menuWindow) : null;
            scrollerObject = fiScroller != null ? fiScroller.GetValue(menuWindow) : null;

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;

            RebuildIconPanelList(menuWindow);
            SelectInitialIcon(menuWindow);

            //// Anchor Editor
            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("SpellIconPickerWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(SpellIconPickerWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiMainPanel = CacheField(type, "mainPanel");
            fiScrollingPanel = CacheField(type, "scrollingPanel");
            fiScroller = CacheField(type, "scroller");
            fiSelectedIcon = CacheField(type, "selectedIcon");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(SpellIconPickerWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                //! TUNING MAY REQUIRE ADJUSTMENT FOR CURRENT WINDOW
                legend.HeaderScale = 6.0f;
                legend.RowScale = 5.0f;
                legend.PadL = 18f;
                legend.PadT = 16f;
                legend.LineGap = 36f;
                legend.ColGap = 22f;
                legend.MarginX = 8f;
                legend.MarginFromBottom = 24f;
                legend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("D-Pad", "scroll rows"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(SpellIconPickerWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            // If DFU swapped the panel instance, our old legend is invalid
            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            // If DFU cleared components, our legend may be detached
            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }
        }

        // =========================
        // Reflection helpers
        // =========================
        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}