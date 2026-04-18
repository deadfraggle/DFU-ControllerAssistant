using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class GuildServiceAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;
        private FieldInfo fiJoinButton;
        private FieldInfo fiServiceLabel;

        private MethodInfo miJoinButton_OnMouseClick;
        private MethodInfo miTalkButton_OnMouseClick;
        private MethodInfo miServiceButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int JoinButton = 0;
        private const int TalkButton = 1;
        private const int ServiceButton = 2;
        private const int ExitButton = 3;

        private int buttonSelected = ServiceButton;

        private static readonly Rect joinSelectorRect = new Rect(99.1f, 78.0f, 122.4f, 9.5f);
        private static readonly Rect talkSelectorRect = new Rect(99.1f, 87.5f, 122.4f, 9.5f);
        private static readonly Rect serviceSelectorRect = new Rect(99.1f, 96.6f, 122.4f, 9.5f);
        private static readonly Rect exitSelectorRect = new Rect(138.8f, 107.5f, 43.6f, 16.4f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallGuildServicePopupWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallGuildServicePopupWindow menuWindow = top as DaggerfallGuildServicePopupWindow;

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

        private void OnOpened(DaggerfallGuildServicePopupWindow menuWindow)
        {
            Type t = typeof(DaggerfallGuildServicePopupWindow);

            fiParentPanel = t.GetField("parentPanel", BF);
            fiJoinButton = t.GetField("joinButton", BF);
            fiServiceLabel = t.GetField("serviceLabel", BF);

            miJoinButton_OnMouseClick = t.GetMethod("JoinButton_OnMouseClick", BF);
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

        private void OnTickOpen(DaggerfallGuildServicePopupWindow menuWindow, ControllerManager cm)
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
                ActivateJoin(menuWindow);
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

        private void TryMoveUp(DaggerfallGuildServicePopupWindow menuWindow)
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

                case TalkButton:
                    if (IsJoinEnabled(menuWindow))
                        buttonSelected = JoinButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void TryMoveDown(DaggerfallGuildServicePopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case JoinButton:
                    buttonSelected = TalkButton;
                    break;

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

        private void ActivateSelectedButton(DaggerfallGuildServicePopupWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case JoinButton:
                    ActivateJoin(menuWindow);
                    break;
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

        private void ActivateJoin(DaggerfallGuildServicePopupWindow menuWindow)
        {
            if (!IsJoinEnabled(menuWindow))
                return;

            DestroyLegend();

            if (menuWindow == null || miJoinButton_OnMouseClick == null)
                return;

            miJoinButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateTalk(DaggerfallGuildServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miTalkButton_OnMouseClick == null)
                return;

            miTalkButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateService(DaggerfallGuildServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miServiceButton_OnMouseClick == null)
                return;

            miServiceButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateExit(DaggerfallGuildServicePopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miExitButton_OnMouseClick == null)
                return;

            miExitButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private bool IsJoinEnabled(DaggerfallGuildServicePopupWindow menuWindow)
        {
            if (menuWindow == null || fiJoinButton == null)
                return false;

            Button joinButton = fiJoinButton.GetValue(menuWindow) as Button;
            if (joinButton == null)
                return false;

            return joinButton.Parent != null;
        }

        private string GetServiceText(DaggerfallGuildServicePopupWindow menuWindow)
        {
            if (menuWindow == null || fiServiceLabel == null)
                return "Service";

            TextLabel label = fiServiceLabel.GetValue(menuWindow) as TextLabel;
            if (label == null || string.IsNullOrEmpty(label.Text))
                return "Service";

            return label.Text;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallGuildServicePopupWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            if (buttonSelected == JoinButton && !IsJoinEnabled(menuWindow))
                buttonSelected = TalkButton;

            Rect targetRect;

            switch (buttonSelected)
            {
                case JoinButton:
                    targetRect = joinSelectorRect;
                    break;
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

        private Panel GetCurrentRenderPanel(DaggerfallGuildServicePopupWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallGuildServicePopupWindow menuWindow)
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

        private void EnsureLegendUI(DaggerfallGuildServicePopupWindow menuWindow, ControllerManager cm)
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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>();

                if (IsJoinEnabled(menuWindow))
                    rows.Add(new LegendOverlay.LegendRow("D-Pad Up", "Join Guild"));

                rows.Add(new LegendOverlay.LegendRow("D-Pad Right", "Talk"));
                rows.Add(new LegendOverlay.LegendRow("D-Pad Down", GetServiceText(menuWindow)));
                rows.Add(new LegendOverlay.LegendRow("D-Pad Left", "Exit"));
                rows.Add(new LegendOverlay.LegendRow("Right Stick Up/Down", "Move Selector"));
                rows.Add(new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"));
                rows.Add(new LegendOverlay.LegendRow("Back", "Exit"));

                legend.Build("Guild Service", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallGuildServicePopupWindow menuWindow)
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

        private void ToggleLegend(DaggerfallGuildServicePopupWindow menuWindow, ControllerManager cm)
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