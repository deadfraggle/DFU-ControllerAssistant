using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TalkWindowAssist
    {
        private void TickSelectionListRegion(DaggerfallTalkWindow menuWindow, ControllerManager cm)
        {
            bool moveUp = cm.RStickUpPressed || cm.RStickUpHeldSlow;
            bool moveDown = cm.RStickDownPressed || cm.RStickDownHeldSlow;
            bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
            bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

            bool isAssisting =
                moveUp ||
                moveDown ||
                moveLeft ||
                moveRight ||
                cm.Action1Pressed ||
                cm.DPadUpPressed || cm.DPadUpHeldSlow ||
                cm.DPadDownPressed || cm.DPadDownHeldSlow ||
                cm.DPadLeftPressed || cm.DPadLeftHeldSlow ||
                cm.DPadRightPressed || cm.DPadRightHeldSlow ||
                cm.LegendPressed;

            if (!isAssisting)
                return;

            if (moveUp)
            {
                SelectionListMoveUp(menuWindow);
                return;
            }

            if (moveDown)
            {
                SelectionListMoveDown(menuWindow);
                return;
            }

            if (moveLeft)
            {
                ExitSelectionListToTellMeAbout(menuWindow);
                return;
            }

            if (moveRight)
            {
                ExitSelectionListToCopyToLogbook(menuWindow);
                return;
            }

            if (cm.Action1Pressed)
            {
                ActivateSelectionListEntry(menuWindow);
                return;
            }

            if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
            {
                ScrollSelectionListVerticalUp(menuWindow);
                return;
            }

            if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
            {
                ScrollSelectionListVerticalDown(menuWindow);
                return;
            }

            if (cm.DPadLeftPressed || cm.DPadLeftHeldSlow)
            {
                ScrollSelectionListHorizontalLeft(menuWindow);
                return;
            }

            if (cm.DPadRightPressed || cm.DPadRightHeldSlow)
            {
                ScrollSelectionListHorizontalRight(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;

                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }
        }

        private void RefreshSelectorToSelectionList(DaggerfallTalkWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                selectionListRect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void SelectionListMoveUp(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || fiListboxTopic == null)
                return;

            try
            {
                ListBox listboxTopic = fiListboxTopic.GetValue(menuWindow) as ListBox;
                if (listboxTopic == null)
                    return;

                if (listboxTopic.Count == 0)
                {
                    ExitSelectionListToWork(menuWindow);
                    return;
                }

                int selectedIndex = listboxTopic.SelectedIndex;

                if (selectedIndex < 0)
                    selectedIndex = 0;

                if (selectedIndex <= 0)
                {
                    ExitSelectionListToWork(menuWindow);
                    return;
                }

                selectedIndex--;

                listboxTopic.SelectIndex(selectedIndex);

                if (miUpdateQuestion != null)
                    miUpdateQuestion.Invoke(menuWindow, new object[] { selectedIndex });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: SelectionListMoveUp() failed: " + ex);
            }
        }

        private void SelectionListMoveDown(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || fiListboxTopic == null)
                return;

            try
            {
                ListBox listboxTopic = fiListboxTopic.GetValue(menuWindow) as ListBox;
                if (listboxTopic == null)
                    return;

                if (listboxTopic.Count == 0)
                {
                    ExitSelectionListToOkay(menuWindow);
                    return;
                }

                int selectedIndex = listboxTopic.SelectedIndex;

                if (selectedIndex < 0)
                    selectedIndex = 0;

                if (selectedIndex >= listboxTopic.Count - 1)
                {
                    ExitSelectionListToOkay(menuWindow);
                    return;
                }

                selectedIndex++;

                listboxTopic.SelectIndex(selectedIndex);

                if (miUpdateQuestion != null)
                    miUpdateQuestion.Invoke(menuWindow, new object[] { selectedIndex });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: SelectionListMoveDown() failed: " + ex);
            }
        }

        private void ScrollTopicListUp(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonTopicUp_OnMouseClick == null)
                return;

            try
            {
                miButtonTopicUp_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollTopicListUp() failed: " + ex);
            }
        }

        private void ScrollTopicListDown(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonTopicDown_OnMouseClick == null)
                return;

            try
            {
                miButtonTopicDown_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollTopicListDown() failed: " + ex);
            }
        }

        private void ScrollTopicListLeft(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonTopicLeft_OnMouseClick == null)
                return;

            try
            {
                miButtonTopicLeft_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollTopicListLeft() failed: " + ex);
            }
        }

        private void ScrollTopicListRight(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || miButtonTopicRight_OnMouseClick == null)
                return;

            try
            {
                miButtonTopicRight_OnMouseClick.Invoke(
                    menuWindow,
                    new object[] { null, Vector2.zero });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ScrollTopicListRight() failed: " + ex);
            }
        }

        private void ActivateSelectionListEntry(DaggerfallTalkWindow menuWindow)
        {
            if (menuWindow == null || fiListboxTopic == null || miSelectTopicFromTopicList == null)
                return;

            try
            {
                ListBox listboxTopic = fiListboxTopic.GetValue(menuWindow) as ListBox;
                if (listboxTopic == null)
                    return;

                int selectedIndex = listboxTopic.SelectedIndex;

                // If nothing is selected yet, try first item for testing
                if (selectedIndex < 0 && listboxTopic.Count > 0)
                    selectedIndex = 0;

                if (selectedIndex < 0)
                    return;

                miSelectTopicFromTopicList.Invoke(menuWindow, new object[] { selectedIndex, false });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TalkWindowAssist: ActivateSelectionListEntry() failed: " + ex);
            }
        }

        private void ExitSelectionListToTellMeAbout(DaggerfallTalkWindow menuWindow)
        {
            EnterButtonsRegionAt(TellMeAboutButton, menuWindow);
        }

        private void ExitSelectionListToCopyToLogbook(DaggerfallTalkWindow menuWindow)
        {
            EnterButtonsRegionAt(CopyToLogbookButton, menuWindow);
        }

        private void ExitSelectionListToWork(DaggerfallTalkWindow menuWindow)
        {
            EnterButtonsRegionAt(WorkButton, menuWindow);
        }

        private void ExitSelectionListToOkay(DaggerfallTalkWindow menuWindow)
        {
            EnterButtonsRegionAt(OkayButton, menuWindow);
        }

        private void ScrollSelectionListVerticalUp(DaggerfallTalkWindow menuWindow)
        {
            ScrollTopicListUp(menuWindow);
        }

        private void ScrollSelectionListVerticalDown(DaggerfallTalkWindow menuWindow)
        {
            ScrollTopicListDown(menuWindow);
        }

        private void ScrollSelectionListHorizontalLeft(DaggerfallTalkWindow menuWindow)
        {
            ScrollTopicListLeft(menuWindow);
        }

        private void ScrollSelectionListHorizontalRight(DaggerfallTalkWindow menuWindow)
        {
            ScrollTopicListRight(menuWindow);
        }
    }
}
