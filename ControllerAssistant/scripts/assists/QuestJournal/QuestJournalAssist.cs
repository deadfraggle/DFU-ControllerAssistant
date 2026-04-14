using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class QuestJournalAssist : IMenuAssist
    {
        private static readonly BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiEntryLineMap;
        private FieldInfo fiCurrentMessageIndex;
        private FieldInfo fiMainPanel;
        private FieldInfo fiParentPanel;

        private MethodInfo miDialogClick;
        private MethodInfo miUpArrowClick;
        private MethodInfo miDownArrowClick;
        private MethodInfo miHandleClick;

        private LegendOverlay legend;
        private TextLabel questMarker;
        private Panel questMarkerParent;

        private bool legendVisible = false;

        private int selectedQuestLine = 0;
        private List<int> visibleQuestEntryIndices = new List<int>();
        private List<int> visibleQuestEntryLines = new List<int>();
        private int selectedQuestEntry = 0;
        private Panel panelRenderWindow;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallQuestJournalWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallQuestJournalWindow menuWindow =
                top as DaggerfallQuestJournalWindow;

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
            selectedQuestLine = 0;
            selectedQuestEntry = 0;
            visibleQuestEntryIndices.Clear();
            visibleQuestEntryLines.Clear();
            panelRenderWindow = null;

            DestroyLegend();
            DestroyQuestMarker();
        }
        private void OnOpened(DaggerfallQuestJournalWindow menuWindow)
        {
            Type t = typeof(DaggerfallQuestJournalWindow);

            fiEntryLineMap = t.GetField("entryLineMap", BF);
            fiCurrentMessageIndex = t.GetField("currentMessageIndex", BF);
            fiMainPanel = t.GetField("mainPanel", BF);
            fiParentPanel = t.GetField("parentPanel", BF);

            miDialogClick = t.GetMethod("DialogButton_OnMouseClick", BF);
            miUpArrowClick = t.GetMethod("UpArrowButton_OnMouseClick", BF);
            miDownArrowClick = t.GetMethod("DownArrowButton_OnMouseClick", BF);
            miHandleClick = t.GetMethod("HandleClick", BF);

            selectedQuestLine = 0;
            selectedQuestEntry = 0;
            visibleQuestEntryIndices.Clear();
            visibleQuestEntryLines.Clear();

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }
    }
}