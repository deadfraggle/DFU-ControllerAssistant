using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist
    {
        private void TryMoveSelector(DaggerfallPotionMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            if (dir == ControllerManager.StickDir8.NE ||
                dir == ControllerManager.StickDir8.SE ||
                dir == ControllerManager.StickDir8.SW ||
                dir == ControllerManager.StickDir8.NW)
                return;

            int previous = buttonSelected;

            if (IsIngredientsGridButton(buttonSelected))
            {
                TryMoveWithinIngredientsGrid(menuWindow, dir);

                if (buttonSelected != previous)
                    RefreshSelectorToCurrentButton(menuWindow);

                return;
            }

            if (IsCauldronGridButton(buttonSelected))
            {
                TryMoveWithinCauldronGrid(menuWindow, dir);

                if (buttonSelected != previous)
                    RefreshSelectorToCurrentButton(menuWindow);

                return;
            }

            SelectorButtonInfo btn = menuButton[buttonSelected];
            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.NE: next = btn.NE; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.SE: next = btn.SE; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.SW: next = btn.SW; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
                case ControllerManager.StickDir8.NW: next = btn.NW; break;
            }

            if (next == IngredientsGridAnchor)
            {
                ingredientsGridColumn = 2;
                ingredientsGridRow = (buttonSelected == RecipesButton) ? 0 : 2;
                buttonSelected = GetIngredientsGridButtonId(ingredientsGridColumn, ingredientsGridRow);
            }
            else if (next == CauldronGridAnchor)
            {
                cauldronGridColumn = 0;
                cauldronGridRow = (buttonSelected == RecipesButton) ? 0 : 1;
                buttonSelected = GetCauldronGridButtonId(cauldronGridColumn, cauldronGridRow);
            }
            else if (next > -1)
            {
                buttonSelected = next;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private Panel GetCurrentRenderPanel(DaggerfallPotionMakerWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return null;

            return fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallPotionMakerWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            parentPanel = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            if (IsIngredientsGridButton(buttonSelected))
            {
                int col, row;
                if (TryDecodeIngredientsGridButtonId(buttonSelected, out col, out row))
                {
                    selectorHost.ShowAtNativeRect(
                        currentPanel,
                        GetIngredientsGridNativeRect(menuWindow, col, row),
                        new Color(0.1f, 1f, 1f, 1f)
                    );
                    return;
                }
            }

            if (IsCauldronGridButton(buttonSelected))
            {
                int col, row;
                if (TryDecodeCauldronGridButtonId(buttonSelected, out col, out row))
                {
                    selectorHost.ShowAtNativeRect(
                        currentPanel,
                        GetCauldronGridNativeRect(menuWindow, col, row),
                        new Color(0.1f, 1f, 1f, 1f)
                    );
                    return;
                }
            }

            if (buttonSelected < 0 || buttonSelected >= menuButton.Length)
                buttonSelected = MixButton;

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallPotionMakerWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            parentPanel = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
                selectorHost.Destroy();
        }
    }
}