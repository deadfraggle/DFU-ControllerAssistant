using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private void EnsureLegendUI(DaggerfallItemMakerWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

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
                    new LegendOverlay.LegendRow("Version", "23"),
                    new LegendOverlay.LegendRow("D-Pad", "move scroll bars"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
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
        }
    }
}
