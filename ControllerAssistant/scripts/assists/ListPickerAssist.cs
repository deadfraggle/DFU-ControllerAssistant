using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class ListPickerAssist : IMenuAssist
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
        private DaggerfallListPickerWindow activeWindow;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallListPickerWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallListPickerWindow menuWindow = top as DaggerfallListPickerWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(activeWindow, cm);
                    activeWindow = null;
                    wasOpen = false;
                }
                return;
            }

            // New picker instance replaced previous picker instance.
            if (wasOpen && !object.ReferenceEquals(menuWindow, activeWindow))
            {
                OnClosed(activeWindow, cm);
                activeWindow = menuWindow;
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }
            else if (!wasOpen)
            {
                wasOpen = true;
                activeWindow = menuWindow;
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
        private void OnTickOpen(DaggerfallListPickerWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            EnsureListBoxFocus(menuWindow);

            if (menuWindow != null && menuWindow.ListBox != null)
                menuWindow.ListBox.AlwaysAcceptKeyboardInput = true;

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            ControllerManager.StickDir8 dir =
                    cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                    ? cm.RStickDir8Pressed
                    : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                switch (dir)
                {
                    case ControllerManager.StickDir8.N:
                        SelectPrevious(menuWindow);
                        break;

                    case ControllerManager.StickDir8.S:
                        SelectNext(menuWindow);
                        break;
                }
            }

            bool isAssisting = (cm.Legend || cm.Action1Released);
            //     (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickV != 0 || cm.RStickH != 0 ||
            //      cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                if (cm.Action1Released)
                {
                    ActivateSelected(menuWindow);
                }

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
                return;
            }

        }

        // =========================
        // Assist action helpers
        // =========================

        private void SelectPrevious(DaggerfallListPickerWindow menuWindow)
        {
            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, false);
        }

        private void SelectNext(DaggerfallListPickerWindow menuWindow)
        {
            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, false);
        }

        private void ActivateSelected(DaggerfallListPickerWindow menuWindow)
        {
            DaggerfallUI.Instance.OnKeyPress(KeyCode.Return, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.Return, false);
        }

        private void EnsureListBoxFocus(DaggerfallListPickerWindow menuWindow)
        {
            if (menuWindow == null || menuWindow.ListBox == null)
                return;

            if (!object.ReferenceEquals(menuWindow.FocusControl, menuWindow.ListBox))
                menuWindow.SetFocus(menuWindow.ListBox);
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallListPickerWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            if (menuWindow != null && menuWindow.ListBox != null)
            {
                menuWindow.ListBox.AlwaysAcceptKeyboardInput = true;

                if (menuWindow.ListBox.Count > 0 && menuWindow.ListBox.SelectedIndex < 0)
                    menuWindow.ListBox.SelectedIndex = 0;
            }
        }

        private void OnClosed(DaggerfallListPickerWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow != null && menuWindow.ListBox != null)
                menuWindow.ListBox.AlwaysAcceptKeyboardInput = false;

            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallListPickerWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallListPickerWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallListPickerWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("Right Stick", "Select"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Activate"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallListPickerWindow menuWindow)
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
