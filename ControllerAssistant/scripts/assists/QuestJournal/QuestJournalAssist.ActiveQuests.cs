using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class QuestJournalAssist
    {
        private void TickActiveQuestSelection(DaggerfallQuestJournalWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow.DisplayMode != DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests)
            {
                visibleQuestEntryIndices.Clear();
                visibleQuestEntryLines.Clear();
                selectedQuestEntry = 0;
                DestroyQuestMarker();
                return;
            }

            RebuildVisibleQuestEntries(menuWindow);

            if (visibleQuestEntryIndices.Count == 0)
                return;

            if (cm.DPadUpPressed)
            {
                selectedQuestEntry--;
                ClampQuestSelection(menuWindow);
            }

            if (cm.DPadDownPressed)
            {
                selectedQuestEntry++;
                ClampQuestSelection(menuWindow);
            }

            if (cm.Action1Released)
            {
                int line = visibleQuestEntryLines[selectedQuestEntry];
                Vector2 clickPos = new Vector2(8f, line * 7f);

                miHandleClick.Invoke(menuWindow, new object[] { clickPos, false });
            }
        }

        private void RebuildVisibleQuestEntries(DaggerfallQuestJournalWindow menuWindow)
        {
            List<int> map = fiEntryLineMap.GetValue(menuWindow) as List<int>;

            visibleQuestEntryIndices.Clear();
            visibleQuestEntryLines.Clear();

            if (map == null || map.Count == 0)
                return;

            int lastEntry = -999999;

            for (int line = 0; line < map.Count; line++)
            {
                int entry = map[line];

                if (entry != lastEntry)
                {
                    visibleQuestEntryIndices.Add(entry);
                    visibleQuestEntryLines.Add(line);
                    lastEntry = entry;
                }
            }

            ClampQuestSelection(menuWindow);
        }

        private void ClampQuestSelection(DaggerfallQuestJournalWindow menuWindow)
        {
            if (visibleQuestEntryIndices.Count == 0)
            {
                selectedQuestEntry = 0;
                return;
            }

            if (selectedQuestEntry < 0)
                selectedQuestEntry = 0;

            if (selectedQuestEntry >= visibleQuestEntryIndices.Count)
                selectedQuestEntry = visibleQuestEntryIndices.Count - 1;

            selectedQuestLine = visibleQuestEntryLines[selectedQuestEntry];
        }
    }
}