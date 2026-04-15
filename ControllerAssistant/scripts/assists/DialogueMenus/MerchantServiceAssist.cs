using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class MerchantServiceAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;
        private FieldInfo fiServiceLabel;

        private MethodInfo miTalkButton_OnMouseClick;
        private MethodInfo miServiceButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int TalkButton = 0;
        private const int ServiceButton = 1;
        private const int ExitButton = 2;

        private int buttonSelected = ServiceButton;

        private static readonly Rect talkSelectorRect = new Rect(98.6f, 82.2f, 123.0f, 9.5f);
        private static readonly Rect serviceSelectorRect = new Rect(98.6f, 91.5f, 123.0f, 9.5f);
        private static readonly Rect exitSelectorRect = new Rect(138.8f, 102.6f, 43.6f, 15.7f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallMerchantServicePopupWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallMerchantServicePopupWindow menuWindow = top as DaggerfallMerchantServicePopupWindow;

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
            buttonSelected = ServiceButton;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            Type t = typeof(DaggerfallMerchantServicePopupWindow);

            fiParentPanel = t.GetField("parentPanel", BF);
            fiServiceLabel = t.GetField("serviceLabel", BF);

            miTalkButton_OnMouseClick = t.GetMethod("TalkButton_OnMouseClick", BF);
            miServiceButton_OnMouseClick = t.GetMethod("ServiceButton_OnMouseClick", BF);
            miExitButton_OnMouseClick = t.GetMethod("ExitButton_OnMouseClick", BF);

            buttonSelected = ServiceButton;

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallMerchantServicePopupWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            bool moveUp =
                cm.RStickUpPressed || cm.RStickUpHeldSlow;

            bool moveDown =
                cm.RStickDownPressed || cm.RStickDownHeldSlow;

            if (moveUp)
            {
                TryMoveUp(menuWindow);
                return;
            }

            if (moveDown)
            {
                TryMoveDown(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                ActivateTalk(menuWindow);
                return;
            }

            if (cm.DPadDownReleased)
            {
                ActivateService(menuWindow);
                return;
            }

            if (cm.DPadLeftReleased)
            {
                ActivateExit(menuWindow);
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
                ActivateExit(menuWindow);
                return;
            }
        }

        private void TryMoveUp(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case ExitButton:
                    buttonSelected = ServiceButton;
                    break;

                case ServiceButton:
                    buttonSelected = TalkButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void TryMoveDown(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case TalkButton:
                    buttonSelected = ServiceButton;
                    break;

                case ServiceButton:
                    buttonSelected = ExitButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void ActivateSelectedButton(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case TalkButton:
                    ActivateTalk(menuWindow);
                    break;
                case ServiceButton:
                    ActivateService(menuWindow);
                    break;
                default:
                    ActivateExit(menuWindow);
                    break;
            }
        }

        private void ActivateTalk(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miTalkButton_OnMouseClick == null)
                return;

            miTalkButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateService(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miServiceButton_OnMouseClick == null)
                return;

            miServiceButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateExit(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miExitButton_OnMouseClick == null)
                return;

            miExitButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private string GetServiceText(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            if (menuWindow == null || fiServiceLabel == null)
                return "Service";

            TextLabel label = fiServiceLabel.GetValue(menuWindow) as TextLabel;
            if (label == null || string.IsNullOrEmpty(label.Text))
                return "Service";

            return label.Text;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            Rect targetRect;

            switch (buttonSelected)
            {
                case TalkButton:
                    targetRect = talkSelectorRect;
                    break;
                case ServiceButton:
                    targetRect = serviceSelectorRect;
                    break;
                default:
                    targetRect = exitSelectorRect;
                    break;
            }

            float borderThickness = 2f;

            if (currentPanel.Size.y > 0f)
            {
                float scaleY = currentPanel.Size.y / 200f;
                borderThickness = Mathf.Max(2f, scaleY * 0.5f);
            }

            selectorHost.ShowAtNativeRect(
                currentPanel,
                targetRect,
                borderThickness,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private Panel GetCurrentRenderPanel(DaggerfallMerchantServicePopupWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallMerchantServicePopupWindow menuWindow)
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

        private void EnsureLegendUI(DaggerfallMerchantServicePopupWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Right", "Talk"),
                    new LegendOverlay.LegendRow("D-Pad Down", GetServiceText(menuWindow)),
                    new LegendOverlay.LegendRow("D-Pad Left", "Exit"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                    new LegendOverlay.LegendRow("Back", "Exit"),
                };

                legend.Build("Merchant Service", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallMerchantServicePopupWindow menuWindow)
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

        private void ToggleLegend(DaggerfallMerchantServicePopupWindow menuWindow, ControllerManager cm)
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
