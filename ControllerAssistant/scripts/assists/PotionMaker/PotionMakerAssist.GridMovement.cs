using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist
    {
        private void TryMoveWithinIngredientsGrid(DaggerfallPotionMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            object scrollerObj = GetIngredientsScroller(menuWindow);
            if (scrollerObj == null)
                return;

            int visibleCount = GetIngredientsVisibleCount(scrollerObj);
            if (visibleCount <= 0)
                return;

            switch (dir)
            {
                case ControllerManager.StickDir8.N:

                    if (ingredientsGridRow > 0)
                    {
                        ingredientsGridRow--;
                    }
                    else
                    {
                        int scroll = GetScrollerScrollIndex(scrollerObj);
                        if (scroll > 0)
                            SetScrollerScrollIndex(scrollerObj, scroll - 1);

                        return;
                    }
                    break;

                case ControllerManager.StickDir8.S:

                    int southIndex = ((ingredientsGridRow + 1) * IngredientsGridColumns) + ingredientsGridColumn;

                    if (ingredientsGridRow < IngredientsGridRows - 1 && southIndex < visibleCount)
                    {
                        ingredientsGridRow++;
                    }
                    else
                    {
                        int scroll = GetScrollerScrollIndex(scrollerObj);
                        int totalRows = GetIngredientsTotalRows(scrollerObj);

                        if ((scroll + IngredientsGridRows) < totalRows)
                            SetScrollerScrollIndex(scrollerObj, scroll + 1);

                        return;
                    }
                    break;

                case ControllerManager.StickDir8.W:

                    if (ingredientsGridColumn > 0)
                    {
                        ingredientsGridColumn--;
                        buttonSelected = GetIngredientsGridButtonId(ingredientsGridColumn, ingredientsGridRow);
                    }
                    return;

                case ControllerManager.StickDir8.E:

                    if (ingredientsGridColumn < IngredientsGridColumns - 1)
                    {
                        int eastIndex = (ingredientsGridRow * IngredientsGridColumns) + (ingredientsGridColumn + 1);

                        if (eastIndex < visibleCount)
                        {
                            ingredientsGridColumn++;
                            buttonSelected = GetIngredientsGridButtonId(ingredientsGridColumn, ingredientsGridRow);
                            return;
                        }
                    }

                    buttonSelected = (ingredientsGridRow <= 1) ? RecipesButton : MixButton;
                    return;
            }

            buttonSelected = GetIngredientsGridButtonId(ingredientsGridColumn, ingredientsGridRow);
        }

        private void TryMoveWithinCauldronGrid(DaggerfallPotionMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            switch (dir)
            {
                case ControllerManager.StickDir8.N:

                    if (cauldronGridRow > 0)
                        cauldronGridRow--;
                    else
                        return;

                    break;

                case ControllerManager.StickDir8.S:

                    if (cauldronGridRow < CauldronGridRows - 1)
                        cauldronGridRow++;
                    else
                    {
                        buttonSelected = ExitButton;
                        return;
                    }

                    break;

                case ControllerManager.StickDir8.W:

                    if (cauldronGridColumn > 0)
                    {
                        cauldronGridColumn--;
                        buttonSelected = GetCauldronGridButtonId(cauldronGridColumn, cauldronGridRow);
                        return;
                    }
                    else
                    {
                        buttonSelected = MixButton;
                        return;
                    }

                    break;

                case ControllerManager.StickDir8.E:

                    if (cauldronGridColumn < CauldronGridColumns - 1)
                    {
                        cauldronGridColumn++;
                        buttonSelected = GetCauldronGridButtonId(cauldronGridColumn, cauldronGridRow);
                    }
                    return;
            }

            buttonSelected = GetCauldronGridButtonId(cauldronGridColumn, cauldronGridRow);
        }
    }
}