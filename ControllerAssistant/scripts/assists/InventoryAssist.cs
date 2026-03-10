using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class InventoryAssist : MenuAssistModule<DaggerfallInventoryWindow>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;  //prevents re-caching Reflection methods

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Used in EnsureInitialized()
        // Cache for reflection so we don’t re-query every press
        private MethodInfo miWagonButtonClick;
        private FieldInfo fiWagonButton;
        private MethodInfo miSelectTabPage;
        private FieldInfo fiSelectedTabPage;
        private MethodInfo miSelectActionMode;
        private FieldInfo fiSelectedActionMode;
        private MethodInfo miGoldButtonClick;
        private FieldInfo fiGoldButton;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;
        //private KeyCode closeKey = KeyCode.None;

        // =========================
        // Core tick / main behavior
        // =========================
        protected override void OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            // Current vanilla binding for this window's open/close action
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Inventory);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            // Read current controller state
            bool isAssisting =
                (cm.DPadLeftPressed || cm.DPadRightPressed || cm.DPadUpPressed || cm.DPadDownPressed ||
                 cm.RStickDownPressed || cm.RStickUpPressed ||
                 cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                if (cm.DPadLeftPressed)
                    CycleTab(menuWindow, -1);

                if (cm.DPadRightPressed)
                    CycleTab(menuWindow, +1);

                if (cm.DPadUpPressed)
                    CycleActionMode(menuWindow, -1);

                if (cm.DPadDownPressed)
                    CycleActionMode(menuWindow, +1);

                if (cm.RStickUpPressed)
                    ToggleWagon(menuWindow);

                if (cm.RStickDownPressed)
                    OpenGoldPopup(menuWindow);

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
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }
                menuWindow.CloseWindow();
                return;
            }
        }
        // =========================
        // Assist action helpers
        // =========================

        private void ToggleWagon(DaggerfallInventoryWindow menuWindow)
        {
            if (miWagonButtonClick == null || menuWindow == null)
                return;

            object wagonButton = fiWagonButton != null ? fiWagonButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
            wagonButton,      // sender
            Vector2.zero      // click position
            };

            miWagonButtonClick.Invoke(menuWindow, args);
        }
        private void CycleTab(DaggerfallInventoryWindow menuWindow, int direction)
        {
            if (menuWindow == null || miSelectTabPage == null || fiSelectedTabPage == null)
                return;

            object currentValue = fiSelectedTabPage.GetValue(menuWindow);
            if (currentValue == null)
                return;

            int current = (int)currentValue;
            int count = 4;

            int next = (current + direction + count) % count;
            object nextEnum = Enum.ToObject(currentValue.GetType(), next);

            miSelectTabPage.Invoke(menuWindow, new object[] { nextEnum });
        }
        private void CycleActionMode(DaggerfallInventoryWindow menuWindow, int direction)
        {
            if (menuWindow == null || miSelectActionMode == null || fiSelectedActionMode == null)
                return;

            object currentValue = fiSelectedActionMode.GetValue(menuWindow);
            if (currentValue == null)
                return;

            int current = (int)currentValue;

            // Visible/actionable button modes only:
            // 0 = Info
            // 1 = Equip
            // 2 = Remove
            // 3 = Use
            // 4 = Select (skip)
            int[] validModes = new int[] { 0, 1, 2, 3 };

            int currentIndex = 0;
            for (int i = 0; i < validModes.Length; i++)
            {
                if (validModes[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + direction + validModes.Length) % validModes.Length;
            int nextValue = validModes[nextIndex];

            object nextEnum = Enum.ToObject(currentValue.GetType(), nextValue);
            miSelectActionMode.Invoke(menuWindow, new object[] { nextEnum });

            if (debugMODE) DaggerfallUI.AddHUDText("ActionMode -> " + nextEnum.ToString());
        }
        private void OpenGoldPopup(DaggerfallInventoryWindow menuWindow)
        {
            if (miGoldButtonClick == null || menuWindow == null)
                return;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            object goldButton = fiGoldButton != null ? fiGoldButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
            goldButton,
            Vector2.zero
            };

            miGoldButtonClick.Invoke(menuWindow, args);

            if (debugMODE) DaggerfallUI.AddHUDText("Gold popup opened");
        }

        // =========================
        // Lifecycle hooks
        // =========================
        protected override void OnOpened(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
        }
        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallInventoryWindow closed");
        }
        public override void ResetState()
        {
            base.ResetState(); // sets wasOpen = false

            closeDeferred = false;
            //closeKey = KeyCode.None;

            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        // =========================
        // Per-window/per-open setup
        // =========================

        // cache reflection handles once (expensive + stable)
        private void EnsureInitialized(DaggerfallInventoryWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");

            miWagonButtonClick = CacheMethod(type, "WagonButton_OnMouseClick");
            fiWagonButton = CacheField(type, "wagonButton");

            miSelectTabPage = CacheMethod(type, "SelectTabPage");
            fiSelectedTabPage = CacheField(type, "selectedTabPage");

            miSelectActionMode = CacheMethod(type, "SelectActionMode");
            fiSelectedActionMode = CacheField(type, "selectedActionMode");

            miGoldButtonClick = CacheMethod(type, "GoldButton_OnMouseClick");
            fiGoldButton = CacheField(type, "goldButton");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Up Dn", "Buttons"),
                    new LegendOverlay.LegendRow("D-Pad LR", "Tabs"),
                    new LegendOverlay.LegendRow("Right Stick Up", "Wagon"),
                    new LegendOverlay.LegendRow("Right Stick Dn", "Gold"),
                    //new LegendOverlay.LegendRow(cm.Action1Name, "Reset"),
                    //new LegendOverlay.LegendRow(cm.Action2Name, "Center"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallInventoryWindow menuWindow)
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


