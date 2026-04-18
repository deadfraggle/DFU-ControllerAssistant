using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class QuestJournalAssist
    {
        private void RefreshQuestMarker(DaggerfallQuestJournalWindow menuWindow)
        {
            if (menuWindow.DisplayMode != DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests)
            {
                DestroyQuestMarker();
                return;
            }

            Panel mainPanel = fiMainPanel.GetValue(menuWindow) as Panel;
            if (mainPanel == null)
                return;

            if (questMarker == null)
            {
                questMarker = new TextLabel();
                questMarker.Text = ">>>";
                questMarker.TextScale = 0.8f;
                mainPanel.Components.Add(questMarker);
                questMarkerParent = mainPanel;
            }
            else if (questMarkerParent != mainPanel)
            {
                DestroyQuestMarker();

                questMarker = new TextLabel();
                questMarker.Text = ">>>";
                questMarker.TextScale = 0.8f;
                mainPanel.Components.Add(questMarker);
                questMarkerParent = mainPanel;
            }

            questMarker.Position = new Vector2(10, 38 + (selectedQuestLine * 7));
        }

        private void DestroyQuestMarker()
        {
            if (questMarker != null && questMarkerParent != null)
                questMarkerParent.Components.Remove(questMarker);

            questMarker = null;
            questMarkerParent = null;
        }

        private void EnsureLegendUI(DaggerfallQuestJournalWindow menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Left/Right", "Cycle Category"),
                    new LegendOverlay.LegendRow("Right Stick", "Scroll"),
                    new LegendOverlay.LegendRow("D-Pad Up/Down", "Select Quest"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Travel To Quest"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallQuestJournalWindow menuWindow)
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

        private void ToggleLegend(DaggerfallQuestJournalWindow menuWindow, ControllerManager cm)
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