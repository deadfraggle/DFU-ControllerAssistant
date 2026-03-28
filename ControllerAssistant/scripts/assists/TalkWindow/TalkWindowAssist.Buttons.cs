using DaggerfallWorkshop.Game.UserInterface;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TalkWindowAssist
    {
        private readonly SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(3.7f, 3.2f, 107.6f, 10.9f), N = OkayButton,        S = WhereIsButton,      E = CopyToLogbookButton, W = CopyToLogbookButton }, // Tell Me About
            new SelectorButtonInfo { rect = new Rect(3.7f, 13.6f, 107.6f, 10.9f), N = TellMeAboutButton, S = LocationButton,     E = CopyToLogbookButton, W = CopyToLogbookButton }, // Where Is...
            new SelectorButtonInfo { rect = new Rect(3.7f, 25.5f, 107.6f, 10.9f), N = WhereIsButton,     S = PeopleButton,       E = CopyToLogbookButton, W = CopyToLogbookButton }, // Location...
            new SelectorButtonInfo { rect = new Rect(3.7f, 35.5f, 107.6f, 10.9f), N = LocationButton,    S = ThingsButton,       E = CopyToLogbookButton, W = CopyToLogbookButton }, // People...
            new SelectorButtonInfo { rect = new Rect(3.7f, 45.5f, 107.6f, 10.9f), N = PeopleButton,      S = WorkButton,         E = CopyToLogbookButton, W = CopyToLogbookButton }, // Things...
            new SelectorButtonInfo { rect = new Rect(3.7f, 55.6f, 107.6f, 10.9f), N = ThingsButton,      S = -1,                 E = CopyToLogbookButton, W = CopyToLogbookButton }, // Work
            new SelectorButtonInfo { rect = new Rect(3.7f, 185.7f, 107.6f, 10.9f), N = -1,               S = TellMeAboutButton,  E = GoodbyeButton,        W = GoodbyeButton       }, // Okay
            new SelectorButtonInfo { rect = new Rect(117.7f, 157.5f, 67.7f, 18.8f), N = -1,              S = GoodbyeButton,      E = TellMeAboutButton,    W = -1                  }, // Copy To Logbook
            new SelectorButtonInfo { rect = new Rect(117.7f, 182.6f, 67.7f, 10.7f), N = CopyToLogbookButton, S = -1,             E = OkayButton,           W = OkayButton          }, // Goodbye
        };

        private void TickButtonsRegion(DaggerfallTalkWindow menuWindow, ControllerManager cm)
        {
            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            bool isAssisting =
                dir != ControllerManager.StickDir8.None ||
                cm.Action1Released ||
                cm.DPadUpPressed || cm.DPadUpHeldSlow ||
                cm.DPadDownPressed || cm.DPadDownHeldSlow ||
                cm.LegendPressed;

            if (!isAssisting)
                return;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelectedButton(menuWindow);
                return;
            }

            if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
            {
                ScrollConversationUp(menuWindow);
                return;
            }

            if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
            {
                ScrollConversationDown(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;

                if (legend != null)
                    legend.SetEnabled(legendVisible);

                //editor.Toggle();
            }
        }

        private void TryMoveSelector(DaggerfallTalkWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None || menuButton == null)
                return;

            int previous = talkButtonSelected;
            SelectorButtonInfo btn = menuButton[talkButtonSelected];

            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N:
                    next = btn.N;
                    break;
                case ControllerManager.StickDir8.NE:
                    next = btn.NE;
                    break;
                case ControllerManager.StickDir8.E:
                    next = btn.E;
                    break;
                case ControllerManager.StickDir8.SE:
                    next = btn.SE;
                    break;
                case ControllerManager.StickDir8.S:
                    next = btn.S;
                    break;
                case ControllerManager.StickDir8.SW:
                    next = btn.SW;
                    break;
                case ControllerManager.StickDir8.W:
                    next = btn.W;
                    break;
                case ControllerManager.StickDir8.NW:
                    next = btn.NW;
                    break;
            }

            if (next == -1)
            {
                if (talkButtonSelected == WorkButton && dir == ControllerManager.StickDir8.S)
                {
                    EnterSelectionListRegionFromButtons(menuWindow);
                    return;
                }

                if (talkButtonSelected == CopyToLogbookButton && dir == ControllerManager.StickDir8.W)
                {
                    EnterSelectionListRegionFromButtons(menuWindow);
                    return;
                }

                if (talkButtonSelected == OkayButton && dir == ControllerManager.StickDir8.N)
                {
                    EnterSelectionListRegionFromButtons(menuWindow);
                    return;
                }

                return;
            }

            talkButtonSelected = next;

            if (talkButtonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void RefreshSelectorToCurrentButton(DaggerfallTalkWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[talkButtonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void RefreshSelectorAttachment(DaggerfallTalkWindow menuWindow)
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

        private void ActivateSelectedButton(DaggerfallTalkWindow menuWindow)
        {
            switch (talkButtonSelected)
            {
                case TellMeAboutButton:
                    ActivateTellMeAbout(menuWindow);
                    break;
                case WhereIsButton:
                    ActivateWhereIs(menuWindow);
                    break;
                case LocationButton:
                    ActivateLocation(menuWindow);
                    break;
                case PeopleButton:
                    ActivatePeople(menuWindow);
                    break;
                case ThingsButton:
                    ActivateThings(menuWindow);
                    break;
                case WorkButton:
                    ActivateWork(menuWindow);
                    break;
                case OkayButton:
                    ActivateOkay(menuWindow);
                    break;
                case CopyToLogbookButton:
                    ActivateCopyToLogbook(menuWindow);
                    break;
                case GoodbyeButton:
                    ActivateGoodbye(menuWindow);
                    break;
            }
        }

        private void ActivateTellMeAbout(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonTellMeAbout_OnMouseClick, "ActivateTellMeAbout()");
        }

        private void ActivateWhereIs(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonWhereIs_OnMouseClick, "ActivateWhereIs()");
        }

        private void ActivateLocation(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonCategoryLocation_OnMouseClick, "ActivateLocation()");
        }

        private void ActivatePeople(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonCategoryPeople_OnMouseClick, "ActivatePeople()");
        }

        private void ActivateThings(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonCategoryThings_OnMouseClick, "ActivateThings()");
        }

        private void ActivateWork(DaggerfallTalkWindow menuWindow)
        {
            InvokeTalkButton(menuWindow, miButtonCategoryWork_OnMouseClick, "ActivateWork()");
        }

        private void ActivateOkay(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonOkay_OnMouseClick == null)
                return;

            try
            {
                miButtonOkay_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ActivateOkay() failed: " + ex);
            }
        }

        private void ActivateCopyToLogbook(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonLogbook_OnMouseClick == null)
                return;

            try
            {
                miButtonLogbook_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ActivateCopyToLogbook() failed: " + ex);
            }
        }

        private void ActivateGoodbye(DaggerfallTalkWindow menuWindow)
        {
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void InvokeTalkButton(DaggerfallTalkWindow menuWindow, MethodInfo method, string methodName)
        {
            if (menuWindow == null || method == null)
                return;

            try
            {
                method.Invoke(menuWindow, new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: " + methodName + " failed: " + ex);
            }
        }

        private void CycleTone(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || fiSelectedTalkTone == null)
                return;

            try
            {
                object currentToneValue = fiSelectedTalkTone.GetValue(menuWindow);
                if (currentToneValue == null)
                    return;

                System.Type toneType = currentToneValue.GetType();
                string currentName = currentToneValue.ToString();
                string nextName = "Normal";

                switch (currentName)
                {
                    case "Normal":
                        nextName = "Blunt";
                        break;
                    case "Blunt":
                        nextName = "Polite";
                        break;
                    case "Polite":
                    default:
                        nextName = "Normal";
                        break;
                }

                object nextToneValue = System.Enum.Parse(toneType, nextName);
                fiSelectedTalkTone.SetValue(menuWindow, nextToneValue);

                if (miUpdateCheckboxes != null)
                    miUpdateCheckboxes.Invoke(menuWindow, null);

                if (miUpdateQuestion != null && fiListboxTopic != null)
                {
                    ListBox listboxTopic = fiListboxTopic.GetValue(menuWindow) as ListBox;
                    int selectedIndex = listboxTopic != null ? listboxTopic.SelectedIndex : -1;
                    miUpdateQuestion.Invoke(menuWindow, new object[] { selectedIndex });
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: CycleTone() failed: " + ex);
            }
        }

        private void EnterSelectionListRegionFromButtons(DaggerfallTalkWindow menuWindow)
        {
            currentRegion = RegionSelectionList;
            RefreshSelectorToSelectionList(menuWindow);
            Debug.Log("[ControllerAssistant] TalkWindowAssist: EnterSelectionListRegionFromButtons()");
        }

        private void EnterButtonsRegionAt(int buttonIndex, DaggerfallTalkWindow menuWindow)
        {
            talkButtonSelected = buttonIndex;
            currentRegion = RegionButtons;
            RefreshSelectorToCurrentButton(menuWindow);
        }

        private void ScrollConversationUp(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonConversationUp_OnMouseClick == null)
                return;

            try
            {
                miButtonConversationUp_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollConversationUp() failed: " + ex);
            }
        }

        private void ScrollConversationDown(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonConversationDown_OnMouseClick == null)
                return;

            try
            {
                miButtonConversationDown_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollConversationDown() failed: " + ex);
            }
        }
    }
}
