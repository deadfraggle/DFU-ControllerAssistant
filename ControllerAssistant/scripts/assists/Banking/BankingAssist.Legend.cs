using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist
    {
        private void EnsureLegendUI(DaggerfallBankingWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;
             
            if (mainPanel == null && fiMainPanel != null)
                mainPanel = fiMainPanel.GetValue(menuWindow) as Panel;

            if (mainPanel == null)
                return;

            DestroyLegend();

            legend = new LegendOverlay(mainPanel);
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
                new LegendOverlay.LegendRow("Version", "6"),
                new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                new LegendOverlay.LegendRow(cm.Action1Name, "Activate Selection"),
            };

            legend.Build("Legend", rows);
        }

        private void EnsureTransactionLegendUI(DaggerfallBankingWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (mainPanel == null && fiMainPanel != null)
                mainPanel = fiMainPanel.GetValue(menuWindow) as Panel;

            if (mainPanel == null)
                return;

            DestroyLegend();

            legend = new LegendOverlay(mainPanel);
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
                new LegendOverlay.LegendRow("Version", "6"),
                new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                new LegendOverlay.LegendRow("Back", "Cancel Transaction"),
            };

            legend.Build("Legend", rows);
        }

        private void RefreshLegendAttachment(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null || fiMainPanel == null)
                return;

            Panel current = fiMainPanel.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (mainPanel != current)
            {
                DestroyLegend();
                mainPanel = current;
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
        }
    }
}