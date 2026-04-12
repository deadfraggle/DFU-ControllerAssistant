using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist
    {
        private void OnTickOpen(DaggerfallPotionMakerWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (selectorHost == null)
            {
                RefreshSelectorToCurrentButton(menuWindow);
            }
            else
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                    RefreshSelectorToCurrentButton(menuWindow);
            }

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (IsIngredientsGridButton(buttonSelected))
            {
                object scrollerObj = GetIngredientsScroller(menuWindow);

                if (scrollerObj != null)
                {
                    if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
                    {
                        int scroll = GetScrollerScrollIndex(scrollerObj);
                        if (scroll > 0)
                            SetScrollerScrollIndex(scrollerObj, scroll - 1);

                        return;
                    }

                    if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
                    {
                        int scroll = GetScrollerScrollIndex(scrollerObj);
                        int totalRows = GetIngredientsTotalRows(scrollerObj);

                        if ((scroll + IngredientsGridRows) < totalRows)
                            SetScrollerScrollIndex(scrollerObj, scroll + 1);

                        return;
                    }
                }
            }

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.Action1Released)
                    ActivateSelectedButton(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        private void OnOpened(DaggerfallPotionMakerWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallPotionMakerWindow closed");
        }

        private void ActivateSelectedButton(DaggerfallPotionMakerWindow menuWindow)
        {
            if (IsIngredientsGridButton(buttonSelected))
            {
                ActivateSelectedIngredient(menuWindow);
                return;
            }

            if (IsCauldronGridButton(buttonSelected))
            {
                ActivateSelectedCauldronItem(menuWindow);
                return;
            }

            switch (buttonSelected)
            {
                case RecipesButton:
                    InvokeButtonHandler(menuWindow, miRecipesButton_OnMouseClick);
                    break;
                case MixButton:
                    InvokeButtonHandler(menuWindow, miMixButton_OnMouseClick);
                    break;
                case ExitButton:
                    InvokeButtonHandler(menuWindow, miExitButton_OnMouseClick);
                    break;
            }
        }
    }
}
