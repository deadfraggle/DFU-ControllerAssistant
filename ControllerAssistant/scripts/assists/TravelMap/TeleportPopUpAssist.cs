using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TeleportPopUpAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;

        private MethodInfo miYesButton_OnMouseClick;
        private MethodInfo miNoButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int YesButton = 0;
        private const int NoButton = 1;
        private int buttonSelected = NoButton;

        private static readonly Rect yesSelectorRect = new Rect(79f, 110f, 52f, 16f);
        private static readonly Rect noSelectorRect = new Rect(190f, 110f, 53f, 16f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallTeleportPopUp;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTeleportPopUp menuWindow = top as DaggerfallTeleportPopUp;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed();
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;
            buttonSelected = NoButton;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallTeleportPopUp menuWindow)
        {
            Type t = typeof(DaggerfallTeleportPopUp);

            fiParentPanel = t.GetField("parentPanel", BF);

            miYesButton_OnMouseClick = t.GetMethod("YesButton_OnMouseClick", BF);
            miNoButton_OnMouseClick = t.GetMethod("NoButton_OnMouseClick", BF);

            buttonSelected = NoButton;

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;

            RefreshSelectorToCurrentButton(menuWindow);
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallTeleportPopUp menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            bool moveLeft =
                cm.RStickLeftPressed || cm.RStickLeftHeldSlow;

            bool moveRight =
                cm.RStickRightPressed || cm.RStickRightHeldSlow;

            if (moveLeft)
            {
                buttonSelected = YesButton;
                RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            if (moveRight)
            {
                buttonSelected = NoButton;
                RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            if (cm.DPadLeftReleased)
            {
                ActivateYes(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                ActivateNo(menuWindow);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelectedButton(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                ToggleLegend(menuWindow, cm);
                return;
            }

            if (cm.BackPressed)
            {
                ActivateNo(menuWindow);
                return;
            }
        }

        private void ActivateSelectedButton(DaggerfallTeleportPopUp menuWindow)
        {
            if (buttonSelected == YesButton)
                ActivateYes(menuWindow);
            else
                ActivateNo(menuWindow);
        }

        private void ActivateYes(DaggerfallTeleportPopUp menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miYesButton_OnMouseClick == null)
                return;

            miYesButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateNo(DaggerfallTeleportPopUp menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miNoButton_OnMouseClick == null)
                return;

            miNoButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void RefreshSelectorToCurrentButton(DaggerfallTeleportPopUp menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            Rect targetRect = (buttonSelected == YesButton)
                ? yesSelectorRect
                : noSelectorRect;

            selectorHost.ShowAtNativeRect(
                currentPanel,
                targetRect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private Panel GetCurrentRenderPanel(DaggerfallTeleportPopUp menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallTeleportPopUp menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null || selectorHost == null)
                return;

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
            {
                selectorHost.Destroy();
                selectorHost = null;
            }
        }

        private void EnsureLegendUI(DaggerfallTeleportPopUp menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;

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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("Version", "1"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Yes"),
                    new LegendOverlay.LegendRow("D-Pad Right", "No"),
                    new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                    new LegendOverlay.LegendRow("Back", "No"),
                };

                legend.Build("Teleport", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTeleportPopUp menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return;

            Panel current = fiParentPanel.GetValue(menuWindow) as Panel;
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

        private void ToggleLegend(DaggerfallTeleportPopUp menuWindow, ControllerManager cm)
        {
            EnsureLegendUI(menuWindow, cm);

            legendVisible = !legendVisible;

            if (legend != null)
                legend.SetEnabled(legendVisible);
        }

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
        }
    }
}
