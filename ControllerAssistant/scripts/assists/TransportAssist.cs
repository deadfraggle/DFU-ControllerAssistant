using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TransportAssist : MenuAssistModule<DaggerfallTransportWindow>
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Window close binding
        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        protected override void OnTickOpen(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Transport);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            // Suppress vanilla "same key closes window" while assist input is active
            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool isAssisting =
                (cm.RStickUpPressed || cm.RStickDownPressed || cm.RStickLeftPressed || cm.RStickRightPressed ||
                cm.Action1Pressed || cm.Action2Pressed || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.RStickUpPressed)
                    SelectFoot(menuWindow);

                if (cm.RStickRightPressed)
                    SelectHorse(menuWindow);

                if (cm.RStickDownPressed)
                    SelectCart(menuWindow); 

                if (cm.RStickLeftPressed)
                    SelectShip(menuWindow);

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
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }

            // Preserve vanilla toggle-close behavior when player is not using assist controls
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

        protected override void OnOpened(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTransportWindow closed");
        }

        public override void ResetState()
        {
            base.ResetState(); // sets wasOpen = false

            closeDeferred = false;
            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        private void EnsureInitialized(DaggerfallTransportWindow menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void SelectFoot(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectHorse(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.HasHorse())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Horse;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectCart(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.HasCart())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Cart;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectShip(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.ShipAvailiable())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Ship;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void EnsureLegendUI(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>();

                rows.Add(new LegendOverlay.LegendRow("Right Stick Up", "Foot"));

                if (GameManager.Instance.TransportManager.HasHorse())
                    rows.Add(new LegendOverlay.LegendRow("Right Stick Right", "Horse"));

                if (GameManager.Instance.TransportManager.HasCart())
                    rows.Add(new LegendOverlay.LegendRow("Right Stick Down", "Cart"));

                if (GameManager.Instance.TransportManager.ShipAvailiable())
                    rows.Add(new LegendOverlay.LegendRow("Right Stick Left", "Ship"));

                legend.Build("Transport", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTransportWindow menuWindow)
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