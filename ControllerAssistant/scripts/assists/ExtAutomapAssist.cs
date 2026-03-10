using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class ExtAutomapAssist : MenuAssistModule<DaggerfallExteriorAutomapWindow>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;  //prevents re-caching Reflection methods

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Cache for reflection so we don’t re-query every press
        private MethodInfo miActionMoveLeft;
        private MethodInfo miActionMoveRight;
        private MethodInfo miActionMoveForward;
        private MethodInfo miActionMoveBackward;
        private MethodInfo miActionMoveUpstairs;
        private MethodInfo miActionMoveDownstairs;
        private MethodInfo miActionResetView;
        private MethodInfo miActionRotateLeft;
        private MethodInfo miActionRotateRight;
        private MethodInfo miActionCenterMapOnPlayer;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        // =========================
        // Core tick / main behavior
        // =========================
        protected override void OnTickOpen(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
        {
            // Current vanilla binding for this window's open/close action
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.AutoMap);

            // Everything from your old Tick() AFTER the cast succeeds
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            // Read current controller state

            bool isAssisting =
                (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickV != 0 || cm.RStickH != 0 ||
                 cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                if (cm.DPadH == 1) PanMapLeft(menuWindow);
                if (cm.DPadH == -1) PanMapRight(menuWindow);

                if (cm.DPadV == 1) PanMapDown(menuWindow);
                if (cm.DPadV == -1) PanMapUp(menuWindow);

                if (cm.RStickV == 1) ZoomMapOut(menuWindow);
                if (cm.RStickV == -1) ZoomMapIn(menuWindow);

                if (cm.RStickH == 1) RotateMapClockwise(menuWindow);
                if (cm.RStickH == -1) RotateMapCounterClockwise(menuWindow);

                if (cm.Action1Pressed) ResetMapView(menuWindow);
                if (cm.Action2Pressed) CenterMapOnPlayer(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            if (cm.BackPressed)
            {
                menuWindow.CloseWindow();
                return;
            }

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;
                menuWindow.CloseWindow();
                return;
            }
        }
        // =========================
        // Assist action helpers
        // =========================
        private void PanMapLeft(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveRight?.Invoke(menuWindow, null);
        }

        private void PanMapRight(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveLeft?.Invoke(menuWindow, null);
        }

        private void PanMapDown(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveForward?.Invoke(menuWindow, null);
        }


        private void PanMapUp(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveBackward?.Invoke(menuWindow, null);
        }


        private void ZoomMapOut(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveUpstairs?.Invoke(menuWindow, null);
        }


        private void ZoomMapIn(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionMoveDownstairs?.Invoke(menuWindow, null);
        }

        private void RotateMapClockwise(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionRotateRight?.Invoke(menuWindow, null);
        }

        private void RotateMapCounterClockwise(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionRotateLeft?.Invoke(menuWindow, null);
        }

        private void ResetMapView(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionResetView?.Invoke(menuWindow, null);
        }

        private void CenterMapOnPlayer(DaggerfallExteriorAutomapWindow menuWindow)
        {
            miActionCenterMapOnPlayer?.Invoke(menuWindow, null);
        }

        // =========================
        // Lifecycle hooks
        // =========================
        protected override void OnOpened(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
            //BeginSession();
        }
        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("Automap closed");
        }
        public override void ResetState()
        {
            base.ResetState(); // sets wasOpen = false

            closeDeferred = false;

            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        // =========================
        // Per-window/per-open setup
        // =========================

        // cache reflection handles once (expensive + stable)
        private void EnsureInitialized(DaggerfallExteriorAutomapWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "automapBinding");

            miActionMoveLeft = CacheMethod(type, "ActionMoveLeft");
            miActionMoveRight = CacheMethod(type, "ActionMoveRight");
            miActionMoveForward = CacheMethod(type, "ActionMoveForward");
            miActionMoveBackward = CacheMethod(type, "ActionMoveBackward");
            miActionMoveUpstairs = CacheMethod(type, "ActionMoveUpstairs");
            miActionMoveDownstairs = CacheMethod(type, "ActionMoveDownstairs");
            miActionRotateLeft = CacheMethod(type, "ActionRotateLeft");
            miActionRotateRight = CacheMethod(type, "ActionRotateRight");
            miActionResetView = CacheMethod(type, "ActionResetView");
            miActionCenterMapOnPlayer = CacheMethod(type, "ActionFocusPlayerPosition");

            fiPanelRenderWindow = CacheField(type, "panelRenderAutomap");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            // Grab the render panel once per open
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                // tuned sizing
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
                    new LegendOverlay.LegendRow("D-Pad", "Pan"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Zoom"),
                    new LegendOverlay.LegendRow("Right Stick Left/Right", "Rotate"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Reset"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Center"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallExteriorAutomapWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            // If DFU swapped the panel instance, our old legend is invalid
            if (panelRenderWindow != current)
            {
                panelRenderWindow = current;
                legendVisible = false;
                legend = null;
                return;
            }

            // If DFU cleared components, our legend may be detached
            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
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

        void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name); //or Debug.Log(m.ToString());

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }

    }
}


