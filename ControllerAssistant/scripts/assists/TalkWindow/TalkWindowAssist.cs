using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TalkWindowAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool reflectionCached = false;
        private bool wasOpen = false;
        private bool closeDeferred = false;

        // Shared reflection/UI
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private MethodInfo miButtonConversationUp_OnMouseClick;
        private MethodInfo miButtonConversationDown_OnMouseClick;
        private MethodInfo miButtonTopicUp_OnMouseClick;
        private MethodInfo miButtonTopicDown_OnMouseClick;
        private MethodInfo miButtonTopicLeft_OnMouseClick;
        private MethodInfo miButtonTopicRight_OnMouseClick;
        private MethodInfo miButtonLogbook_OnMouseClick;
        private MethodInfo miButtonTellMeAbout_OnMouseClick;
        private MethodInfo miButtonWhereIs_OnMouseClick;
        private MethodInfo miButtonCategoryLocation_OnMouseClick;
        private MethodInfo miButtonCategoryPeople_OnMouseClick;
        private MethodInfo miButtonCategoryThings_OnMouseClick;
        private MethodInfo miButtonCategoryWork_OnMouseClick;

        private FieldInfo fiSelectedTalkTone;
        private FieldInfo fiListboxTopic;

        private MethodInfo miUpdateCheckboxes;
        private MethodInfo miUpdateQuestion;
        private MethodInfo miSelectTopicFromTopicList;
        private MethodInfo miButtonOkay_OnMouseClick;



        // Selector
        private DefaultSelectorBoxHost selectorHost;
        private bool selectorInitializedThisOpen = false;
        private int selectorInitStableTicks = 0;
        private float selectorInitLastWidth = -1;
        private float selectorInitLastHeight = -1;

        // Region constants/state
        private const int RegionButtons = 0;
        private const int RegionSelectionList = 1;
        private int currentRegion = RegionButtons;

        // Shared selector state
        private int talkButtonSelected = 0;

        // Button constants
        private const int TellMeAboutButton = 0;
        private const int WhereIsButton = 1;
        private const int LocationButton = 2;
        private const int PeopleButton = 3;
        private const int ThingsButton = 4;
        private const int WorkButton = 5;
        private const int OkayButton = 6;
        private const int CopyToLogbookButton = 7;
        private const int GoodbyeButton = 8;

        private static readonly Rect selectionListRect = new Rect(3.5f, 68.6f, 108.0f, 117.6f);

        //private AnchorEditor editor;


        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallTalkWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTalkWindow menuWindow = top as DaggerfallTalkWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        private void OnTickOpen(DaggerfallTalkWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            // One-time delayed selector attach for windows whose panel is not ready in OnOpened().
            if (!selectorInitializedThisOpen)
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                {
                    float w = currentPanel.Rectangle.width;
                    float h = currentPanel.Rectangle.height;

                    if (w > 0 && h > 0)
                    {
                        if (w == selectorInitLastWidth && h == selectorInitLastHeight)
                            selectorInitStableTicks++;
                        else
                            selectorInitStableTicks = 1;

                        selectorInitLastWidth = w;
                        selectorInitLastHeight = h;

                        if (selectorInitStableTicks >= 2)
                        {
                            RefreshSelectorToCurrentButton(menuWindow);
                            selectorInitializedThisOpen = true;
                        }
                    }
                }
            }

            if (cm.Action2Pressed)
            {
                CycleTone(menuWindow);
                return;
            }

            if (currentRegion == RegionButtons)
                TickButtonsRegion(menuWindow, cm);
            else
                TickSelectionListRegion(menuWindow, cm);


            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        private void OnOpened(DaggerfallTalkWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            selectorInitializedThisOpen = false;
            currentRegion = RegionButtons;

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;
            currentRegion = RegionButtons;

            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTalkWindow closed");
        }

        public void ResetState()
        {
            wasOpen = false;
            closeDeferred = false;
            selectorInitializedThisOpen = false;
            currentRegion = RegionButtons;

            DestroyLegend();
            DestroySelectorBox();

            panelRenderWindow = null;

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;
            currentRegion = RegionButtons;
        }

        private void EnsureInitialized(DaggerfallTalkWindow menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            System.Type type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            miButtonConversationUp_OnMouseClick = CacheMethod(type, "ButtonConversationUp_OnMouseClick");
            miButtonConversationDown_OnMouseClick = CacheMethod(type, "ButtonConversationDown_OnMouseClick");

            fiSelectedTalkTone = CacheField(type, "selectedTalkTone");
            fiListboxTopic = CacheField(type, "listboxTopic");

            miUpdateCheckboxes = CacheMethod(type, "UpdateCheckboxes");
            miUpdateQuestion = CacheMethod(type, "UpdateQuestion");
            miSelectTopicFromTopicList = CacheMethod(type, "SelectTopicFromTopicList");
            miButtonOkay_OnMouseClick = CacheMethod(type, "ButtonOkay_OnMouseClick");
            miButtonLogbook_OnMouseClick = CacheMethod(type, "ButtonLogbook_OnMouseClick");

            miButtonTellMeAbout_OnMouseClick = CacheMethod(type, "ButtonTellMeAbout_OnMouseClick");
            miButtonWhereIs_OnMouseClick = CacheMethod(type, "ButtonWhereIs_OnMouseClick");
            miButtonCategoryLocation_OnMouseClick = CacheMethod(type, "ButtonCategoryLocation_OnMouseClick");
            miButtonCategoryPeople_OnMouseClick = CacheMethod(type, "ButtonCategoryPeople_OnMouseClick");
            miButtonCategoryThings_OnMouseClick = CacheMethod(type, "ButtonCategoryThings_OnMouseClick");
            miButtonCategoryWork_OnMouseClick = CacheMethod(type, "ButtonCategoryWork_OnMouseClick");

            miButtonTopicUp_OnMouseClick = CacheMethod(type, "ButtonTopicUp_OnMouseClick");
            miButtonTopicDown_OnMouseClick = CacheMethod(type, "ButtonTopicDown_OnMouseClick");
            miButtonTopicLeft_OnMouseClick = CacheMethod(type, "ButtonTopicLeft_OnMouseClick");
            miButtonTopicRight_OnMouseClick = CacheMethod(type, "ButtonTopicRight_OnMouseClick");

            reflectionCached = true;
        }

        private void EnsureLegendUI(DaggerfallTalkWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

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
                    new LegendOverlay.LegendRow("D-Pad", "move scroll bars"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector / list"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "change tone"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTalkWindow menuWindow)
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

            legendVisible = false;
        }

        private Panel GetCurrentRenderPanel(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            System.Type type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (MethodInfo m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (FieldInfo f in type.GetFields(BF))
                Debug.Log(f.Name);
        }

    }
}