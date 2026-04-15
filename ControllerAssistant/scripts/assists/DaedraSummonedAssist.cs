using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Utility;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class DaedraSummonedAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;

        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiLastChunk;
        private FieldInfo fiAnswerGiven;
        private FieldInfo fiDaedraQuest;
        private MethodInfo miHandleAnswer;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallDaedraSummonedWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallDaedraSummonedWindow menuWindow = top as DaggerfallDaedraSummonedWindow;

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
            DestroyLegend();
            panelRenderWindow = null;
        }

        private void OnOpened(DaggerfallDaedraSummonedWindow menuWindow)
        {
            Type t = typeof(DaggerfallDaedraSummonedWindow);

            fiParentPanel = t.GetField("parentPanel", BF);
            fiLastChunk = t.GetField("lastChunk", BF);
            fiAnswerGiven = t.GetField("answerGiven", BF);
            fiDaedraQuest = t.GetField("daedraQuest", BF);
            miHandleAnswer = t.GetMethod("HandleAnswer", BF);

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallDaedraSummonedWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (cm.DPadLeftReleased)
            {
                AnswerYes(menuWindow);
                return;
            }

            if (cm.DPadRightReleased)
            {
                AnswerNo(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                ToggleLegend(menuWindow, cm);
                return;
            }
        }

        private void EnsureLegendUI(DaggerfallDaedraSummonedWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Left", "Yes"),
                    new LegendOverlay.LegendRow("D-Pad Right", "No"),
                };

                legend.Build("Daedra", rows);
            }
        }

        private void ToggleLegend(DaggerfallDaedraSummonedWindow menuWindow, ControllerManager cm)
        {
            EnsureLegendUI(menuWindow, cm);

            legendVisible = !legendVisible;

            if (legend != null)
                legend.SetEnabled(legendVisible);
        }

        private void RefreshLegendAttachment(DaggerfallDaedraSummonedWindow menuWindow)
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

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
        }
        private bool IsAwaitingAnswer(DaggerfallDaedraSummonedWindow menuWindow)
        {
            if (menuWindow == null || fiLastChunk == null || fiAnswerGiven == null)
                return false;

            bool lastChunk = (bool)fiLastChunk.GetValue(menuWindow);
            bool answerGiven = (bool)fiAnswerGiven.GetValue(menuWindow);

            return lastChunk && !answerGiven;
        }

        private void AnswerYes(DaggerfallDaedraSummonedWindow menuWindow)
        {
            if (!IsAwaitingAnswer(menuWindow))
                return;

            if (miHandleAnswer == null || fiDaedraQuest == null)
                return;

            object daedraQuest = fiDaedraQuest.GetValue(menuWindow);
            if (daedraQuest == null)
                return;

            System.Type questMessagesType =
                typeof(DaggerfallWorkshop.Game.Questing.QuestMachine.QuestMessages);

            object acceptValue = System.Enum.Parse(questMessagesType, "AcceptQuest");

            miHandleAnswer.Invoke(menuWindow, new object[] { acceptValue });
            DaggerfallWorkshop.Game.Questing.QuestMachine.Instance.StartQuest(
                daedraQuest as DaggerfallWorkshop.Game.Questing.Quest);
        }

        private void AnswerNo(DaggerfallDaedraSummonedWindow menuWindow)
        {
            if (!IsAwaitingAnswer(menuWindow))
                return;

            if (miHandleAnswer == null)
                return;

            System.Type questMessagesType =
                typeof(DaggerfallWorkshop.Game.Questing.QuestMachine.QuestMessages);

            object refuseValue = System.Enum.Parse(questMessagesType, "RefuseQuest");

            miHandleAnswer.Invoke(menuWindow, new object[] { refuseValue });

            DaggerfallWorkshop.Utility.GameObjectHelper.CreateFoeSpawner(
                true,
                DaggerfallQuestPopupWindow.daedricFoes[UnityEngine.Random.Range(0, 5)],
                UnityEngine.Random.Range(3, 6),
                8,
                64);
        }
    }
}