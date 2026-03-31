using DaggerfallConnect.Arena2;
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
    public partial class TravelMapAssist : IMenuAssist
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
        private FieldInfo fiImportedOverlays;

        private MethodInfo miStartIdentify;
        private MethodInfo miOpenRegionPanel;

        private int controllerRegion = -1;
        private int lastSelectedRegionState = -2;

        private const int regionPanelOffset = 12;
        private const int regionTextureWidth = 320;
        private const int regionTextureHeight = 160;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        private AnchorEditor editor;

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
                    // A popup over the travel map is not the same as the travel map closing.
                    if (!IsTravelMapChildWindow(top))
                    {
                        OnClosed(cm);
                        wasOpen = false;
                    }
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

            lastSelectedRegionState = -2;

            ResetRegionViewState();
            ResetOverworldButtonState();
        }

        private void OnTickOpen(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.TravelMap);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            // Anchor Editor
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow != null)
                editor.Tick(panelRenderWindow);

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            bool overworldMode = selectedRegion == -1;

            bool returnedToOverworld = lastSelectedRegionState != -1 && selectedRegion == -1;
            bool enteredRegionView = lastSelectedRegionState == -1 && selectedRegion != -1;
            lastSelectedRegionState = selectedRegion;

            if (returnedToOverworld)
            {
                ResetRegionViewState();

                if (controllerRegion >= 0)
                    HighlightControllerRegion(menuWindow, controllerRegion);
            }

            if (enteredRegionView)
            {
                ResetOverworldButtonState();
                OnOpenedRegionView(menuWindow, cm);
            }

            if (overworldMode)
            {
                TickOverworld(menuWindow, cm);
            }
            else
            {
                if (!overworldMode && inOverworldButtonMode)
                    ExitOverworldButtonMode(menuWindow);
                TickRegionView(menuWindow, cm);
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }

            if (cm.Action2Pressed)
            {
                editor.Toggle();
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

        private void OnOpened(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            lastSelectedRegionState = selectedRegion;

            if (selectedRegion == -1)
            {
                ResetRegionViewState();
                OnOpenedOverworld(menuWindow, cm);
            }
            else
            {
                ResetOverworldButtonState();
                OnOpenedRegionView(menuWindow, cm);
            }

            if (editor == null)
            {
                editor = new AnchorEditor(25f, 19f);
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTravelMapWindow closed");
        }

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
            fiImportedOverlays = CacheField(type, "importedOverlays");

            miStartIdentify = CacheMethod(type, "StartIdentify");
            miOpenRegionPanel = CacheMethod(type, "OpenRegionPanel");

            CacheRegionViewReflection(type);

            reflectionCached = true;
        }

        private void EnsureLegendUI(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

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
                    new LegendOverlay.LegendRow("Version", "12"),
                    new LegendOverlay.LegendRow("Right Stick", "change region / move buttons"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "open / activate"),
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

            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

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

        private bool IsTravelMapChildWindow(IUserInterfaceWindow top)
        {
            object current = top;
            int guard = 0;

            while (current != null && guard++ < 8)
            {
                if (current is DaggerfallTravelMapWindow)
                    return true;

                System.Type t = current.GetType();

                PropertyInfo piPrevious = t.GetProperty("PreviousWindow", BF);
                if (piPrevious != null)
                {
                    current = piPrevious.GetValue(current, null);
                    continue;
                }

                FieldInfo fiPrevious = t.GetField("previousWindow", BF);
                if (fiPrevious != null)
                {
                    current = fiPrevious.GetValue(current);
                    continue;
                }

                break;
            }

            return false;
        }


        partial void TickOverworld(DaggerfallTravelMapWindow menuWindow, ControllerManager cm);
        partial void OnOpenedOverworld(DaggerfallTravelMapWindow menuWindow, ControllerManager cm);

        partial void TickRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm);
        partial void OnOpenedRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm);
        partial void ResetRegionViewState();
        partial void CacheRegionViewReflection(System.Type type);
    }
}