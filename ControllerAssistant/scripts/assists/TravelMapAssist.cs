using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TravelMapAssist : IMenuAssist //! IMPORTANT: Register this module in Runtime so it is included in the assist list
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiMouseOverRegion;
        private FieldInfo fiSelectedRegion;
        private FieldInfo fiRegionPickerBitmap;
        private FieldInfo fiIdentifyPixelBuffer;
        private FieldInfo fiIdentifyTexture;
        private FieldInfo fiIdentifyOverlayPanel;
        private FieldInfo fiIdentifyFlashColor;
        private FieldInfo fiIdentifying;
        private FieldInfo fiIdentifyChanges;

        private MethodInfo miStartIdentify;
        private MethodInfo miOpenRegionPanel;

        private int controllerRegion = -1;

        private const int regionPanelOffset = 12;
        private const int regionTextureWidth = 320;
        private const int regionTextureHeight = 160;

        private HashSet<int> validOverworldRegions = new HashSet<int>();
        private bool validRegionsBuilt = false;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallTravelMapWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTravelMapWindow menuWindow = top as DaggerfallTravelMapWindow;

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

            legendVisible = false;
            panelRenderWindow = null;
            validOverworldRegions.Clear();
            validRegionsBuilt = false;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.TravelMap);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            bool overworldMode = selectedRegion == -1;

            if (overworldMode)
            {
                if (controllerRegion >= 0)
                    KeepIdentifyAlive(menuWindow);

                ControllerManager.StickDir8 dir =
                    cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                    ? cm.RStickDir8Pressed
                    : cm.RStickDir8HeldSlow;

                if (dir == ControllerManager.StickDir8.W || dir == ControllerManager.StickDir8.NW || dir == ControllerManager.StickDir8.SW)
                {
                    CycleRegion(menuWindow, -1);
                    return;
                }

                if (dir == ControllerManager.StickDir8.E || dir == ControllerManager.StickDir8.NE || dir == ControllerManager.StickDir8.SE)
                {
                    CycleRegion(menuWindow, +1);
                    return;
                }

                if (cm.Action1Released)
                {
                    OpenCurrentControllerRegion(menuWindow);
                    return;
                }
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }

            bool isAssisting = cm.Legend;

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }
        }

        // =========================
        // Assist action helpers
        // =========================

        private int GetInitialControllerRegion(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null)
                return -1;

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            if (selectedRegion >= 0)
                return selectedRegion;

            int mouseOverRegion = fiMouseOverRegion != null ? (int)fiMouseOverRegion.GetValue(menuWindow) : -1;
            if (mouseOverRegion >= 0)
                return mouseOverRegion;

            int playerRegion = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(
                TravelTimeCalculator.GetPlayerTravelPosition().X,
                TravelTimeCalculator.GetPlayerTravelPosition().Y) - 128;

            if (playerRegion >= 0 && playerRegion < DaggerfallUnity.Instance.ContentReader.MapFileReader.RegionCount)
                return playerRegion;

            return 0;
        }

        private void CycleRegion(DaggerfallTravelMapWindow menuWindow, int delta)
        {
            int regionCount = DaggerfallUnity.Instance.ContentReader.MapFileReader.RegionCount;
            if (regionCount <= 0)
                return;

            if (controllerRegion < 0 || controllerRegion >= regionCount)
                controllerRegion = GetInitialControllerRegion(menuWindow);

            int next = controllerRegion;
            for (int i = 0; i < regionCount; i++)
            {
                next += delta;

                if (next < 0)
                    next = regionCount - 1;
                else if (next >= regionCount)
                    next = 0;

                if (IsValidRegionIndex(next))
                    break;
            }

            if (next == controllerRegion)
                return;

            controllerRegion = next;
            HighlightControllerRegion(menuWindow, controllerRegion);
        }

        private bool IsValidRegionIndex(int region)
        {
            return validOverworldRegions.Contains(region);
        }

        private void OpenCurrentControllerRegion(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null || controllerRegion < 0)
                return;

            if (miOpenRegionPanel != null)
                miOpenRegionPanel.Invoke(menuWindow, new object[] { controllerRegion });
        }

        private void HighlightControllerRegion(DaggerfallTravelMapWindow menuWindow, int region)
        {
            if (menuWindow == null || region < 0)
                return;

            if (fiMouseOverRegion != null)
                fiMouseOverRegion.SetValue(menuWindow, region);

            UpdateIdentifyTextureForRegion(menuWindow, region);

            if (miStartIdentify != null)
                miStartIdentify.Invoke(menuWindow, null);
        }

        private void UpdateIdentifyTextureForRegion(DaggerfallTravelMapWindow menuWindow, int region)
        {
            if (menuWindow == null || region < 0)
                return;

            object pickerBitmapObj = fiRegionPickerBitmap != null ? fiRegionPickerBitmap.GetValue(menuWindow) : null;
            Color32[] identifyPixelBuffer = fiIdentifyPixelBuffer != null ? fiIdentifyPixelBuffer.GetValue(menuWindow) as Color32[] : null;
            Texture2D identifyTexture = fiIdentifyTexture != null ? fiIdentifyTexture.GetValue(menuWindow) as Texture2D : null;
            Panel identifyOverlayPanel = fiIdentifyOverlayPanel != null ? fiIdentifyOverlayPanel.GetValue(menuWindow) as Panel : null;

            if (pickerBitmapObj == null || identifyPixelBuffer == null || identifyTexture == null || identifyOverlayPanel == null)
                return;

            System.Type pickerType = pickerBitmapObj.GetType();

            int width = 0;
            int height = 0;
            byte[] data = null;

            PropertyInfo piWidth = pickerType.GetProperty("Width");
            PropertyInfo piHeight = pickerType.GetProperty("Height");
            PropertyInfo piData = pickerType.GetProperty("Data");

            if (piWidth != null)
                width = (int)piWidth.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiWidth = pickerType.GetField("Width");
                if (fiWidth != null)
                    width = (int)fiWidth.GetValue(pickerBitmapObj);
            }

            if (piHeight != null)
                height = (int)piHeight.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiHeight = pickerType.GetField("Height");
                if (fiHeight != null)
                    height = (int)fiHeight.GetValue(pickerBitmapObj);
            }

            if (piData != null)
                data = piData.GetValue(pickerBitmapObj, null) as byte[];
            else
            {
                FieldInfo fiData = pickerType.GetField("Data");
                if (fiData != null)
                    data = fiData.GetValue(pickerBitmapObj) as byte[];
            }

            if (width <= 0 || height <= 0 || data == null)
            {
                Debug.Log("[TravelMapAssist] Failed to read DFBitmap Width/Height/Data.");
                return;
            }

            if (data == null)
                return;

            object flashColorObj = fiIdentifyFlashColor != null ? fiIdentifyFlashColor.GetValue(menuWindow) : null;
            Color32 identifyFlashColor = flashColorObj is Color32 ? (Color32)flashColorObj : new Color32(255, 0, 0, 255);

            System.Array.Clear(identifyPixelBuffer, 0, identifyPixelBuffer.Length);

            int pickerOverlayPanelHeightDifference = height - regionTextureHeight - regionPanelOffset + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = y * width + x;
                    int dstOffset = ((height - y - pickerOverlayPanelHeightDifference) * width) + x;

                    if (dstOffset < 0 || dstOffset >= identifyPixelBuffer.Length)
                        continue;

                    int sampleRegion = data[srcOffset] - 128;
                    if (sampleRegion == region)
                        identifyPixelBuffer[dstOffset] = identifyFlashColor;
                }
            }

            identifyTexture.SetPixels32(identifyPixelBuffer);
            identifyTexture.Apply();
            identifyOverlayPanel.BackgroundTexture = identifyTexture;
        }

        private void BuildValidOverworldRegions(DaggerfallTravelMapWindow menuWindow)
        {
            if (validRegionsBuilt)
                return;

            validOverworldRegions.Clear();

            object pickerBitmapObj = fiRegionPickerBitmap != null ? fiRegionPickerBitmap.GetValue(menuWindow) : null;
            if (pickerBitmapObj == null)
                return;

            System.Type pickerType = pickerBitmapObj.GetType();

            int width = 0;
            int height = 0;
            byte[] data = null;

            PropertyInfo piWidth = pickerType.GetProperty("Width");
            PropertyInfo piHeight = pickerType.GetProperty("Height");
            PropertyInfo piData = pickerType.GetProperty("Data");

            if (piWidth != null)
                width = (int)piWidth.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiWidth = pickerType.GetField("Width");
                if (fiWidth != null)
                    width = (int)fiWidth.GetValue(pickerBitmapObj);
            }

            if (piHeight != null)
                height = (int)piHeight.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiHeight = pickerType.GetField("Height");
                if (fiHeight != null)
                    height = (int)fiHeight.GetValue(pickerBitmapObj);
            }

            if (piData != null)
                data = piData.GetValue(pickerBitmapObj, null) as byte[];
            else
            {
                FieldInfo fiData = pickerType.GetField("Data");
                if (fiData != null)
                    data = fiData.GetValue(pickerBitmapObj) as byte[];
            }

            if (width <= 0 || height <= 0 || data == null)
                return;

            for (int i = 0; i < data.Length; i++)
            {
                int region = data[i] - 128;
                if (region >= 0)
                    validOverworldRegions.Add(region);
            }

            validRegionsBuilt = true;
        }

        private void KeepIdentifyAlive(DaggerfallTravelMapWindow menuWindow)
        {
            if (fiIdentifying != null)
                fiIdentifying.SetValue(menuWindow, true);

            if (fiIdentifyChanges != null)
                fiIdentifyChanges.SetValue(menuWindow, 0f);
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            controllerRegion = GetInitialControllerRegion(menuWindow);
            if (controllerRegion >= 0)
                HighlightControllerRegion(menuWindow, controllerRegion);

            BuildValidOverworldRegions(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTravelMapWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallTravelMapWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiMouseOverRegion = CacheField(type, "mouseOverRegion");
            fiSelectedRegion = CacheField(type, "selectedRegion");
            fiRegionPickerBitmap = CacheField(type, "regionPickerBitmap");
            fiIdentifyPixelBuffer = CacheField(type, "identifyPixelBuffer");
            fiIdentifyTexture = CacheField(type, "identifyTexture");
            fiIdentifyOverlayPanel = CacheField(type, "identifyOverlayPanel");
            fiIdentifyFlashColor = CacheField(type, "identifyFlashColor");
            fiIdentifying = CacheField(type, "identifying");
            fiIdentifyChanges = CacheField(type, "identifyChanges");

            miStartIdentify = CacheMethod(type, "StartIdentify");
            miOpenRegionPanel = CacheMethod(type, "OpenRegionPanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("Version", "3"),
                    new LegendOverlay.LegendRow("Right Stick", "change region"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "open region"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTravelMapWindow menuWindow)
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