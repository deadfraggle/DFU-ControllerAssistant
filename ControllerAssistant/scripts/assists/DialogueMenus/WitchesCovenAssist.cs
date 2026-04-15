using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class WitchesCovenAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;

        private MethodInfo miTalkButton_OnMouseClick;
        private MethodInfo miSummonButton_OnMouseClick;
        private MethodInfo miQuestButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int TalkButton = 0;
        private const int SummonButton = 1;
        private const int QuestButton = 2;
        private const int ExitButton = 3;

        private int buttonSelected = SummonButton;

        private static readonly Rect talkSelectorRect = new Rect(99.1f, 78.0f, 122.7f, 9.6f);
        private static readonly Rect summonSelectorRect = new Rect(99.1f, 87.4f, 122.7f, 9.6f);
        private static readonly Rect questSelectorRect = new Rect(99.1f, 96.6f, 122.7f, 9.6f);
        private static readonly Rect exitSelectorRect = new Rect(139.0f, 107.8f, 43.2f, 15.4f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallWitchesCovenPopupWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallWitchesCovenPopupWindow menuWindow = top as DaggerfallWitchesCovenPopupWindow;

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
            buttonSelected = SummonButton;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            Type t = typeof(DaggerfallWitchesCovenPopupWindow);

            fiParentPanel = t.GetField("parentPanel", BF);

            miTalkButton_OnMouseClick = t.GetMethod("TalkButton_OnMouseClick", BF);
            miSummonButton_OnMouseClick = t.GetMethod("SummonButton_OnMouseClick", BF);
            miQuestButton_OnMouseClick = t.GetMethod("QuestButton_OnMouseClick", BF);
            miExitButton_OnMouseClick = t.GetMethod("ExitButton_OnMouseClick", BF);

            buttonSelected = SummonButton;

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallWitchesCovenPopupWindow menuWindow, ControllerManager cm)
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
                ActivateTalk(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                ActivateSummon(menuWindow);
                return;
            }

            if (cm.DPadDownReleased)
            {
                ActivateQuest(menuWindow);
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

        private void TryMoveUp(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case ExitButton:
                    buttonSelected = QuestButton;
                    break;

                case QuestButton:
                    buttonSelected = SummonButton;
                    break;

                case SummonButton:
                    buttonSelected = TalkButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void TryMoveDown(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            int previous = buttonSelected;

            switch (buttonSelected)
            {
                case TalkButton:
                    buttonSelected = SummonButton;
                    break;

                case SummonButton:
                    buttonSelected = QuestButton;
                    break;

                case QuestButton:
                    buttonSelected = ExitButton;
                    break;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void ActivateSelectedButton(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case TalkButton:
                    ActivateTalk(menuWindow);
                    break;
                case SummonButton:
                    ActivateSummon(menuWindow);
                    break;
                case QuestButton:
                    ActivateQuest(menuWindow);
                    break;
                default:
                    ActivateExit(menuWindow);
                    break;
            }
        }

        private void ActivateTalk(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miTalkButton_OnMouseClick == null)
                return;

            miTalkButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateSummon(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miSummonButton_OnMouseClick == null)
                return;

            miSummonButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateQuest(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miQuestButton_OnMouseClick == null)
                return;

            miQuestButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateExit(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miExitButton_OnMouseClick == null)
                return;

            miExitButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void RefreshSelectorToCurrentButton(DaggerfallWitchesCovenPopupWindow menuWindow)
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
                case SummonButton:
                    targetRect = summonSelectorRect;
                    break;
                case QuestButton:
                    targetRect = questSelectorRect;
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

        private Panel GetCurrentRenderPanel(DaggerfallWitchesCovenPopupWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallWitchesCovenPopupWindow menuWindow)
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

        private void EnsureLegendUI(DaggerfallWitchesCovenPopupWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Up", "Talk"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Daedra Summoning"),
                    new LegendOverlay.LegendRow("D-Pad Down", "Quest"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Exit"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                    new LegendOverlay.LegendRow("Back", "Exit"),
                };

                legend.Build("Witches Coven", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallWitchesCovenPopupWindow menuWindow)
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

        private void ToggleLegend(DaggerfallWitchesCovenPopupWindow menuWindow, ControllerManager cm)
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
