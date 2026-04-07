using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class CharacterSheetAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
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

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallCharacterSheetWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallCharacterSheetWindow menuWindow = top as DaggerfallCharacterSheetWindow;

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
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.CharacterSheet);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);


            bool isAssisting =
                (cm.Action2Pressed || cm.LegendPressed);

            if (isAssisting)
            {

                if (cm.Action2Pressed)
                {
                    //Invoke a level up
                }

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }


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

        //! EXAMPLE ACTIONS
        // private void ActionLeft(DaggerfallCharacterSheetWindow menuWindow)
        // {
        //     miActionMoveLeft?.Invoke(menuWindow, null);
        // }

        // private void ActionRight(DaggerfallCharacterSheetWindow menuWindow)
        // {
        //     miActionMoveRight?.Invoke(menuWindow, null);
        // }

        // private void Action1(DaggerfallCharacterSheetWindow menuWindow)
        // {
        // }

        // private void Action2(DaggerfallCharacterSheetWindow menuWindow)
        // {
        // }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallCharacterSheetWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");

            //! REPLACE WITH ACTUAL METHOD/FIELD NAMES NEEDED BY THIS WINDOW
            // miActionMoveLeft = CacheMethod(type, "ActionMoveLeft");
            // miActionMoveRight = CacheMethod(type, "ActionMoveRight");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("Version", "1"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Invoke level up"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallCharacterSheetWindow menuWindow)
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
