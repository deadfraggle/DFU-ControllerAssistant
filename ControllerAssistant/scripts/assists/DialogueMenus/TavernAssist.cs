using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TavernAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;

        private MethodInfo miRoomButton_OnMouseClick;
        private MethodInfo miTalkButton_OnMouseClick;
        private MethodInfo miFoodButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selectorHost;

        private const int RoomButton = 0;
        private const int TalkButton = 1;
        private const int FoodButton = 2;
        private const int GoodbyeButton = 3;

        private int buttonSelected = RoomButton;

        private static readonly Rect roomSelectorRect = new Rect(99.3f, 81.3f, 122.4f, 9.3f);
        private static readonly Rect talkSelectorRect = new Rect(99.3f, 90.3f, 122.4f, 9.3f);
        private static readonly Rect foodSelectorRect = new Rect(99.3f, 99.5f, 122.4f, 9.3f);
        private static readonly Rect goodbyeSelectorRect = new Rect(99.3f, 108.6f, 122.4f, 9.3f);

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallTavernWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTavernWindow menuWindow = top as DaggerfallTavernWindow;

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
            buttonSelected = RoomButton;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallTavernWindow menuWindow)
        {
            Type t = typeof(DaggerfallTavernWindow);

            fiParentPanel = t.GetField("parentPanel", BF);

            miRoomButton_OnMouseClick = t.GetMethod("RoomButton_OnMouseClick", BF);
            miTalkButton_OnMouseClick = t.GetMethod("TalkButton_OnMouseClick", BF);
            miFoodButton_OnMouseClick = t.GetMethod("FoodButton_OnMouseClick", BF);
            miExitButton_OnMouseClick = t.GetMethod("ExitButton_OnMouseClick", BF);

            buttonSelected = RoomButton;

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallTavernWindow menuWindow, ControllerManager cm)
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
                if (buttonSelected > RoomButton)
                    buttonSelected--;

                RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            if (moveDown)
            {
                if (buttonSelected < GoodbyeButton)
                    buttonSelected++;

                RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            if (cm.DPadUpReleased)
            {
                ActivateRoom(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                ActivateTalk(menuWindow);
                return;
            }

            if (cm.DPadDownReleased)
            {
                ActivateFood(menuWindow);
                return;
            }

            if (cm.DPadLeftReleased)
            {
                ActivateGoodbye(menuWindow);
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
                ActivateGoodbye(menuWindow);
                return;
            }
        }

        private void ActivateSelectedButton(DaggerfallTavernWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case RoomButton:
                    ActivateRoom(menuWindow);
                    break;
                case TalkButton:
                    ActivateTalk(menuWindow);
                    break;
                case FoodButton:
                    ActivateFood(menuWindow);
                    break;
                default:
                    ActivateGoodbye(menuWindow);
                    break;
            }
        }

        private void ActivateRoom(DaggerfallTavernWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miRoomButton_OnMouseClick == null)
                return;

            miRoomButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateTalk(DaggerfallTavernWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miTalkButton_OnMouseClick == null)
                return;

            miTalkButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateFood(DaggerfallTavernWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miFoodButton_OnMouseClick == null)
                return;

            miFoodButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateGoodbye(DaggerfallTavernWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow == null || miExitButton_OnMouseClick == null)
                return;

            miExitButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void RefreshSelectorToCurrentButton(DaggerfallTavernWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            Rect targetRect;

            switch (buttonSelected)
            {
                case RoomButton:
                    targetRect = roomSelectorRect;
                    break;
                case TalkButton:
                    targetRect = talkSelectorRect;
                    break;
                case FoodButton:
                    targetRect = foodSelectorRect;
                    break;
                default:
                    targetRect = goodbyeSelectorRect;
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

        private Panel GetCurrentRenderPanel(DaggerfallTavernWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorAttachment(DaggerfallTavernWindow menuWindow)
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

        private void EnsureLegendUI(DaggerfallTavernWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Up", "Room"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Talk"),
                    new LegendOverlay.LegendRow("D-Pad Down", "Food & Drinks"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Goodbye"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                    new LegendOverlay.LegendRow("Back", "Goodbye"),
                };

                legend.Build("Tavern", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTavernWindow menuWindow)
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

        private void ToggleLegend(DaggerfallTavernWindow menuWindow, ControllerManager cm)
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
