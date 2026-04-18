using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class RestAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiWhileButton;
        private FieldInfo fiHealedButton;
        private FieldInfo fiLoiterButton;
        private FieldInfo fiStopButton;

        private MethodInfo miWhileButton_OnMouseClick;
        private MethodInfo miHealedButton_OnMouseClick;
        private MethodInfo miLoiterButton_OnMouseClick;
        private MethodInfo miStopButton_OnMouseClick;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Window close binding
        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        // Button & selector setup

        private DefaultSelectorBoxHost selectorHost;

        const int ForAWhileButton = 0;
        const int UntilFullyHealedButton = 1;
        const int LoiterAWhileButton = 2;
        const int StopButton = 3;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(86.6f, 62.7f, 48.6f, 24.7f), E = UntilFullyHealedButton, W = LoiterAWhileButton }, // ForAWhileButton
            new SelectorButtonInfo { rect = new Rect(135.6f, 62.7f, 48.6f, 24.7f), E = LoiterAWhileButton, W = ForAWhileButton }, // UntilFullyHealedButton
            new SelectorButtonInfo { rect = new Rect(184.6f, 62.7f, 48.6f, 24.7f), E = ForAWhileButton, W = UntilFullyHealedButton }, // LoiterAWhileButton
            new SelectorButtonInfo { rect = new Rect(140.4f, 75.5f, 40.9f, 10.9f) }, // StopButton
        };

        public int buttonSelected = ForAWhileButton;

        //private AnchorEditor editor;

        private void ActivateSelectedButton(DaggerfallRestWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case ForAWhileButton:
                    ClickForAWhile(menuWindow);
                    break;

                case UntilFullyHealedButton:
                    ClickUntilFullyHealed(menuWindow);
                    break;

                case LoiterAWhileButton:
                    ClickLoiterAWhileWhile(menuWindow);
                    break;

                case StopButton:
                    ClickStop(menuWindow);
                    break;
            }
        }

        private void TryMoveSelector(DaggerfallRestWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            var btn = menuButton[buttonSelected];

            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.NE: next = btn.NE; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.SE: next = btn.SE; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.SW: next = btn.SW; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
                case ControllerManager.StickDir8.NW: next = btn.NW; break;
            }

            if (next > -1)
                buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }
        private Panel GetCurrentRenderPanel(DaggerfallRestWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallRestWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallRestWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
                selectorHost.Destroy();
        }


        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallRestWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallRestWindow menuWindow = top as DaggerfallRestWindow;

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
            DestroySelectorBox();

            legendVisible = false;
            panelRenderWindow = null;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallRestWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Rest);

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            bool isRestActive =
                GameManager.Instance != null &&
                GameManager.Instance.PlayerEntity != null &&
                GameManager.Instance.PlayerEntity.CurrentRestMode != DaggerfallRestWindow.RestModes.Selection;

            if (isRestActive)
            {
                if (buttonSelected != StopButton)
                {
                    buttonSelected = StopButton;
                    RefreshSelectorToCurrentButton(menuWindow);
                }

                if (cm.Action1Released)
                {
                    ClickStop(menuWindow);
                    return;
                }

                return;
            }

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            // Suppress vanilla "same key closes window" while assist input is active
            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);


            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed ||
                cm.DPadUpPressed || cm.DPadLeftReleased || cm.DPadUpReleased || cm.DPadRightReleased);

            if (isAssisting)
            {
                if (cm.DPadLeftReleased)
                {
                    ClickForAWhile(menuWindow);
                }

                if (cm.DPadUpReleased)
                {
                    ClickUntilFullyHealed(menuWindow);
                }

                if (cm.DPadRightReleased)
                {
                    ClickLoiterAWhileWhile(menuWindow);
                }

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(menuWindow);
                }

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                    //editor.Toggle();
                }
            }


            if (cm.BackPressed)
            {
                DestroyLegend();
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

        // =========================
        // Assist helpers
        // =========================

        private void ClickForAWhile(DaggerfallRestWindow menuWindow)
        {
            if (menuWindow == null || miWhileButton_OnMouseClick == null)
                return;

            BaseScreenComponent sender = null;
            if (fiWhileButton != null)
                sender = fiWhileButton.GetValue(menuWindow) as BaseScreenComponent;

            miWhileButton_OnMouseClick.Invoke(menuWindow, new object[] { sender, Vector2.zero });
        }

        private void ClickUntilFullyHealed(DaggerfallRestWindow menuWindow)
        {
            if (menuWindow == null || miHealedButton_OnMouseClick == null)
                return;

            BaseScreenComponent sender = null;
            if (fiHealedButton != null)
                sender = fiHealedButton.GetValue(menuWindow) as BaseScreenComponent;

            miHealedButton_OnMouseClick.Invoke(menuWindow, new object[] { sender, Vector2.zero });
        }

        private void ClickLoiterAWhileWhile(DaggerfallRestWindow menuWindow)
        {
            if (menuWindow == null || miLoiterButton_OnMouseClick == null)
                return;

            BaseScreenComponent sender = null;
            if (fiLoiterButton != null)
                sender = fiLoiterButton.GetValue(menuWindow) as BaseScreenComponent;

            miLoiterButton_OnMouseClick.Invoke(menuWindow, new object[] { sender, Vector2.zero });
        }
        private void ClickStop(DaggerfallRestWindow menuWindow)
        {
            if (menuWindow == null || miStopButton_OnMouseClick == null)
                return;

            BaseScreenComponent sender = null;
            if (fiStopButton != null)
                sender = fiStopButton.GetValue(menuWindow) as BaseScreenComponent;

            miStopButton_OnMouseClick.Invoke(menuWindow, new object[] { sender, Vector2.zero });
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallRestWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);

            // Never reopen on the countdown Stop selector.
            // Start each fresh Rest menu on the normal button row.
            if (buttonSelected == StopButton || buttonSelected < ForAWhileButton || buttonSelected > StopButton)
                buttonSelected = ForAWhileButton;

            RefreshSelectorToCurrentButton(menuWindow);

            //// Anchor Editor
            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallRestWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallRestWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiWindowBinding = CacheField(type, "toggleClosedBinding");

            fiWhileButton = CacheField(type, "whileButton");
            fiHealedButton = CacheField(type, "healedButton");
            fiLoiterButton = CacheField(type, "loiterButton");
            fiStopButton = CacheField(type, "stopButton");

            miWhileButton_OnMouseClick = CacheMethod(type, "WhileButton_OnMouseClick");
            miHealedButton_OnMouseClick = CacheMethod(type, "HealedButton_OnMouseClick");
            miLoiterButton_OnMouseClick = CacheMethod(type, "LoiterButton_OnMouseClick");
            miStopButton_OnMouseClick = CacheMethod(type, "StopButton_OnMouseClick");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallRestWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Left", "For A While"),
                    new LegendOverlay.LegendRow("D-Pad Up", "Until Fully Healed"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Loiter A While"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallRestWindow menuWindow)
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

    }
}