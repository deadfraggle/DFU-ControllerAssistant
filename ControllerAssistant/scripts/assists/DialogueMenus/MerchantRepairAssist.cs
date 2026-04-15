using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class MerchantRepairAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;

        private MethodInfo miRepairButton_OnMouseClick;
        private MethodInfo miTalkButton_OnMouseClick;
        private MethodInfo miSellButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int RepairButton = 0;
        private const int TalkButton = 1;
        private const int SellButton = 2;
        private const int ExitButton = 3;

        private int buttonSelected = RepairButton;

        private static readonly Rect repairSelectorRect = new Rect(99.1f, 78.0f, 122.7f, 9.6f);
        private static readonly Rect talkSelectorRect = new Rect(99.1f, 87.4f, 122.7f, 9.6f);
        private static readonly Rect sellSelectorRect = new Rect(99.1f, 96.6f, 122.7f, 9.6f);
        private static readonly Rect exitSelectorRect = new Rect(139.0f, 107.8f, 43.2f, 15.4f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallMerchantRepairPopupWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallMerchantRepairPopupWindow menuWindow = top as DaggerfallMerchantRepairPopupWindow;

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
            buttonSelected = RepairButton;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            Type t = typeof(DaggerfallMerchantRepairPopupWindow);

            fiParentPanel = t.GetField("parentPanel", BF);

            miRepairButton_OnMouseClick = t.GetMethod("RepairButton_OnMouseClick", BF);
            miTalkButton_OnMouseClick = t.GetMethod("TalkButton_OnMouseClick", BF);
            miSellButton_OnMouseClick = t.GetMethod("SellButton_OnMouseClick", BF);
            miExitButton_OnMouseClick = t.GetMethod("ExitButton_OnMouseClick", BF);

            buttonSelected = RepairButton;

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallMerchantRepairPopupWindow menuWindow, ControllerManager cm)
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

            if (cm.DPadUpReleased)
            {
                ActivateRepair(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                ActivateTalk(menuWindow);
                return;
            }

            if (cm.DPadDownReleased)
            {
                ActivateSell(menuWindow);
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

        private void TryMoveUp(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case ExitButton:
                    buttonSelected = SellButton;
                    break;

                case SellButton:
                    buttonSelected = TalkButton;
                    break;

                case TalkButton:
                    buttonSelected = RepairButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void TryMoveDown(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case RepairButton:
                    buttonSelected = TalkButton;
                    break;

                case TalkButton:
                    buttonSelected = SellButton;
                    break;

                case SellButton:
                    buttonSelected = ExitButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void ActivateSelectedButton(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case RepairButton:
                    ActivateRepair(menuWindow);
                    break;
                case TalkButton:
                    ActivateTalk(menuWindow);
                    break;
                case SellButton:
                    ActivateSell(menuWindow);
                    break;
                default:
                    ActivateExit(menuWindow);
                    break;
            }
        }

        private void ActivateRepair(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miRepairButton_OnMouseClick == null)
                return;

            miRepairButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateTalk(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miTalkButton_OnMouseClick == null)
                return;

            miTalkButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateSell(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miSellButton_OnMouseClick == null)
                return;

            miSellButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateExit(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miExitButton_OnMouseClick == null)
                return;

            miExitButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void RefreshSelectorToCurrentButton(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            Rect targetRect;

            switch (buttonSelected)
            {
                case RepairButton:
                    targetRect = repairSelectorRect;
                    break;
                case TalkButton:
                    targetRect = talkSelectorRect;
                    break;
                case SellButton:
                    targetRect = sellSelectorRect;
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

        private Panel GetCurrentRenderPanel(DaggerfallMerchantRepairPopupWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallMerchantRepairPopupWindow menuWindow)
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

        private void EnsureLegendUI(DaggerfallMerchantRepairPopupWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Up", "Repair"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Talk"),
                    new LegendOverlay.LegendRow("D-Pad Down", "Sell"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Exit"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                    new LegendOverlay.LegendRow("Back", "Exit"),
                };

                legend.Build("Merchant Repair", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallMerchantRepairPopupWindow menuWindow)
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

        private void ToggleLegend(DaggerfallMerchantRepairPopupWindow menuWindow, ControllerManager cm)
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