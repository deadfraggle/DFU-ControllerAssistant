using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class QuestJournalAssist
    {
        private void OnTickOpen(DaggerfallQuestJournalWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshQuestMarker(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir == ControllerManager.StickDir8.N)
            {
                miUpArrowClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
                ClampQuestSelection(menuWindow);
                return;
            }

            if (dir == ControllerManager.StickDir8.S)
            {
                miDownArrowClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
                ClampQuestSelection(menuWindow);
                return;
            }

            if (cm.DPadLeftPressed)
            {
                CycleJournal(menuWindow, false);
                return;
            }

            if (cm.DPadRightPressed)
            {
                CycleJournal(menuWindow, true);
                return;
            }

            TickActiveQuestSelection(menuWindow, cm);

            if (cm.LegendPressed)
            {
                ToggleLegend(menuWindow, cm);
                return;
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
            }
        }
        private void CycleJournal(DaggerfallQuestJournalWindow menuWindow, bool forward)
        {
            if (forward)
            {
                miDialogClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
            }
            else
            {
                for (int i = 0; i < 3; i++)
                    miDialogClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
            }

            selectedQuestLine = 0;

            DestroyQuestMarker();
        }
    }
}