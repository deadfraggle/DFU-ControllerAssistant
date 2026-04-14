using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class DefaultMenuAssist : IMenuAssist
    {
        private const bool debugMODE = true;
        private bool reflectionCached = false;

        // Open/close edge tracking (replaces MenuAssistModule state handling)
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private AnchorEditor editor;

        public bool Claims(IUserInterfaceWindow top)
        {
            // Default assist is the fallback for ordinary menu windows.
            // It should NOT claim HUD / no-menu state.
            return top != null && !(top is DaggerfallHUD);
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            IUserInterfaceWindow menuWindow = top;

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

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
            panelRenderWindow = null;
        }

        private void OnTickOpen(IUserInterfaceWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            // Anchor Editor
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (panelRenderWindow != null)
                editor.Tick(panelRenderWindow);

            if (cm.DPadLeftPressed)
                TapKey(KeyCode.LeftArrow);

            if (cm.DPadRightPressed)
                TapKey(KeyCode.RightArrow);

            if (cm.DPadUpPressed)
                TapKey(KeyCode.UpArrow);

            if (cm.DPadDownPressed)
                TapKey(KeyCode.DownArrow);

            if (cm.Action1Pressed)
                TapKey(KeyCode.Return);

            if (cm.Action2Pressed)
                editor.Toggle();

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }
        }

        private void OnOpened(IUserInterfaceWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);

            // Anchor Editor
            if (editor == null)
            {
                // Match Inventory's default selector size: 25 x 19 native-ish feel
                editor = new AnchorEditor(25f, 19f);
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DefaultMenuAssist closed");
        }

        private void EnsureInitialized(IUserInterfaceWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            // Default assist should try to attach to the active window's main panel.
            // parentPanel is the most common field name used by DFU windows.
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void EnsureLegendUI(IUserInterfaceWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad", "Keyboard arrows"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Submit"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(IUserInterfaceWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (fiPanelRenderWindow == null)
                fiPanelRenderWindow = CacheField(menuWindow.GetType(), "parentPanel");

            if (fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                panelRenderWindow = current;
                legendVisible = false;
                legend = null;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        private static void TapKey(KeyCode key)
        {
            DaggerfallUI.Instance.OnKeyPress(key, true);
            DaggerfallUI.Instance.OnKeyPress(key, false);
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing field: " + name + " on " + type.Name);
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