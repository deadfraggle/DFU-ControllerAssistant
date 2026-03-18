using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class PauseAssist : IMenuAssist //! IMPORTANT: Register this module in Runtime so it is included in the assist list
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;  // prevents re-caching Reflection members
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Used in EnsureInitialized()
        // Cache for reflection so we don’t re-query every press
        //! EXAMPLES: declare only what the target window actually needs
        // private MethodInfo miActionMoveLeft;
        // private MethodInfo miActionMoveRight;
        // private MethodInfo miActionMoveForward;
        // private MethodInfo miActionMoveBackward;
        // private MethodInfo miActionMoveUpstairs;
        // private MethodInfo miActionMoveDownstairs;
        // private MethodInfo miActionResetView;
        // private MethodInfo miActionRotateLeft;
        // private MethodInfo miActionRotateRight;
        // private MethodInfo miActionCenterMapOnPlayer;

        private FieldInfo fiOptionsPanel;
        private bool pausePanelMovedThisOpen = false;

        // Diagnostic tuning
        private const float pausePanelYOffset = -50f;   // negative = move up

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;
        //private AnchorEditor editor;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallPauseOptionsWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallPauseOptionsWindow menuWindow = top as DaggerfallPauseOptionsWindow;

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

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
        {
            //! REPLACE Inventory WITH THE CORRECT VANILLA BINDING FOR THIS WINDOW
            // KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Inventory);

            RefreshLegendAttachment(menuWindow);

            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomRight();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            //! ADJUST THIS TO FIT THE CURRENT WINDOW
            bool isAssisting = (cm.Legend || cm.Action1);
            //     (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickV != 0 || cm.RStickH != 0 ||
            //      cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                //     if (cm.DPadH == 1) ActionLeft(menuWindow);
                //     if (cm.DPadH == -1) ActionRight(menuWindow);

                //     if (cm.DPadV == 1) ActionDown(menuWindow);
                //     if (cm.DPadV == -1) ActionUp(menuWindow);

                //     if (cm.RStickV == 1) ActionStickDown(menuWindow);
                //     if (cm.RStickV == -1) ActionStickUp(menuWindow);

                //if (cm.Action1Pressed)
                //    editor.Toggle();

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
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                //menuWindow.CloseWindow();
                return;
            }

            //! UNCOMMENT AFTER windowBinding / isAssisting ARE SET
            // if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            // {
            //     closeDeferred = true;
            // }

            // if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            // {
            //     closeDeferred = false;

            //     if (legend != null)
            //     {
            //         legend.Destroy();
            //         legend = null;
            //     }

            //     menuWindow.CloseWindow();
            //     return;
            // }
        }

        // =========================
        // Assist action helpers
        // =========================

        //! EXAMPLE ACTIONS
        // private void ActionLeft(REPLACE_WITH_WINDOW_TYPE menuWindow)
        // {
        //     miActionMoveLeft?.Invoke(menuWindow, null);
        // }

        // private void ActionRight(REPLACE_WITH_WINDOW_TYPE menuWindow)
        // {
        //     miActionMoveRight?.Invoke(menuWindow, null);
        // }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
            TryMovePausePanelUp(menuWindow);

            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallPauseOptionsWindow closed");
        }

        public void ResetState()
        {
            wasOpen = false;

            closeDeferred = false;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
            panelRenderWindow = null;
            //pausePanelMovedThisOpen = false;
        }

        // =========================
        // Per-window/per-open setup
        // =========================

        // cache reflection handles once (expensive + stable)
        private void EnsureInitialized(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            //! REPLACE WITH TARGET WINDOW’S CLOSE-BINDING FIELD NAME
            // fiWindowBinding = CacheField(type, "toggleClosedBinding");

            //! REPLACE WITH ACTUAL METHOD/FIELD NAMES NEEDED BY THIS WINDOW
            // miActionMoveLeft = CacheMethod(type, "ActionMoveLeft");
            // miActionMoveRight = CacheMethod(type, "ActionMoveRight");

            //! REPLACE WITH TARGET WINDOW’S RENDER PANEL FIELD NAME - DON'T PICK mainPanel, IT'S ALMOST ALWAYS parentPanel
            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiOptionsPanel = CacheField(type, "optionsPanel");

            reflectionCached = true;
        }
        private void TryMovePausePanelUp(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (pausePanelMovedThisOpen)
                return;

            if (menuWindow == null || fiOptionsPanel == null)
                return;

            Panel optionsPanel = fiOptionsPanel.GetValue(menuWindow) as Panel;
            if (optionsPanel == null)
                return;

            Vector2 pos = optionsPanel.Position;
            //optionsPanel.Position = new Vector2(pos.x, pos.y + pausePanelYOffset);
            optionsPanel.Position = new Vector2(pos.x, 0f);

            pausePanelMovedThisOpen = true;

            if (debugMODE)
                Debug.Log("[ControllerAssistant] Pause optionsPanel moved from " + pos + " to " + optionsPanel.Position);
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
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
                    //new LegendOverlay.LegendRow("D-Pad", "Action"),
                    //new LegendOverlay.LegendRow("Right Stick", "Action"),
                    //new LegendOverlay.LegendRow(cm.Action1Name, "Action1"),
                    //new LegendOverlay.LegendRow(cm.Action2Name, "Action2"),
                    new LegendOverlay.LegendRow("NUMPAD8", "move up"),
                    new LegendOverlay.LegendRow("NUMPAD2", "move down"),
                    new LegendOverlay.LegendRow("NUMPAD4", "move left"),
                    new LegendOverlay.LegendRow("NUMPAD6", "move right"),
                    new LegendOverlay.LegendRow("NUMPAD7", "width -"),
                    new LegendOverlay.LegendRow("NUMPAD9", "width +"),
                    new LegendOverlay.LegendRow("NUMPAD1", "height -"),
                    new LegendOverlay.LegendRow("NUMPAD3", "height +"),
                    new LegendOverlay.LegendRow("NUMPAD5", "dump X/Y/W/H"),
                    new LegendOverlay.LegendRow("NUMPAD0", "re-center"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallPauseOptionsWindow menuWindow)
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