using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class SpellbookAssist : MenuAssistModule<DaggerfallSpellBookWindow>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Reflection cache
        private FieldInfo fiWindowBinding;

        private MethodInfo miActionMoveSpell;
        private FieldInfo fiUpButton;
        private FieldInfo fiDownButton;

        private MethodInfo miActionSort;
        private FieldInfo fiSortButton;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private bool closeDeferred = false;

        protected override void OnTickOpen(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.CastSpell);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool isAssisting =
                (cm.DPadUpPressed || cm.DPadDownPressed ||
                 cm.RStickUpPressed || cm.RStickDownPressed ||
                 cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                if (cm.DPadUpPressed)
                    TapKey(KeyCode.UpArrow);

                if (cm.DPadDownPressed)
                    TapKey(KeyCode.DownArrow);

                if (cm.Action1Pressed)
                    TapKey(KeyCode.Return);

                if (cm.Action2Pressed)
                    ActionSort(menuWindow);

                if (cm.RStickUpPressed)
                    ActionMoveSpellUp(menuWindow);

                if (cm.RStickDownPressed)
                    ActionMoveSpellDown(menuWindow);

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

        private void ActionMoveSpellUp(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeMoveSpell(menuWindow, fiUpButton);
        }

        private void ActionMoveSpellDown(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeMoveSpell(menuWindow, fiDownButton);
        }

        private void InvokeMoveSpell(DaggerfallSpellBookWindow menuWindow, FieldInfo buttonField)
        {
            if (menuWindow == null || miActionMoveSpell == null || buttonField == null)
                return;

            object sender = buttonField.GetValue(menuWindow);
            object[] args = new object[]
            {
                sender,
                Vector2.zero,
            };

            miActionMoveSpell.Invoke(menuWindow, args);
        }

        private void ActionSort(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || miActionSort == null)
                return;

            object sender = fiSortButton != null ? fiSortButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
                sender,
                Vector2.zero,
            };

            miActionSort.Invoke(menuWindow, args);
        }

        protected override void OnOpened(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallSpellBookWindow closed");
        }

        public override void ResetState()
        {
            base.ResetState();

            closeDeferred = false;

            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        private void EnsureInitialized(DaggerfallSpellBookWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");

            miActionMoveSpell = CacheMethod(type, "SwapButton_OnMouseClick");
            fiUpButton = CacheField(type, "upButton");
            fiDownButton = CacheField(type, "downButton");

            miActionSort = CacheMethod(type, "SortButton_OnMouseClick");
            fiSortButton = CacheField(type, "sortButton");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void EnsureLegendUI(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
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
            new LegendOverlay.LegendRow("D-Pad", "Select spell"),
            new LegendOverlay.LegendRow(cm.Action1Name, "Activate"),
            new LegendOverlay.LegendRow(cm.Action2Name, "Sort"),
            new LegendOverlay.LegendRow("Right Stick Up", "Move spell up"),
            new LegendOverlay.LegendRow("Right Stick Down", "Move spell down"),
        };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
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

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing method: " + name + " on " + type.Name);
            return mi;
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