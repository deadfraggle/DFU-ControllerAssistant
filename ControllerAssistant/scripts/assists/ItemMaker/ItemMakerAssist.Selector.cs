using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private void TryMoveSelector(DaggerfallItemMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;

            if (IsItemGridButton(buttonSelected))
            {
                TryMoveWithinItemGrid(menuWindow, dir);
                if (buttonSelected != previous)
                    RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            if (buttonSelected == PowersPanel || buttonSelected == SideEffectsPanel)
            {
                if (dir == ControllerManager.StickDir8.N)
                {
                    TryMoveWithinEnchantmentPanel(menuWindow, -1);
                    return;
                }

                if (dir == ControllerManager.StickDir8.S)
                {
                    TryMoveWithinEnchantmentPanel(menuWindow, +1);
                    return;
                }
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

            if (IsGridEntryAnchor(next))
            {
                int col, row;
                if (TryGetGridEntryAnchor(next, out col, out row))
                {
                    itemGridColumn = col;
                    itemGridRow = row;
                    buttonSelected = GetGridButtonId(col, row);
                }
            }
            else if (next > -1)
            {
                buttonSelected = next;
            }

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private Panel GetCurrentRenderPanel(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallItemMakerWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            if (IsItemGridButton(buttonSelected))
            {
                int col, row;
                if (TryDecodeGridButtonId(buttonSelected, out col, out row))
                {
                    selectorHost.ShowAtNativeRect(
                        currentPanel,
                        GetItemGridNativeRect(menuWindow, col, row),
                        new Color(0.1f, 1f, 1f, 1f)
                    );
                    return;
                }
            }

            if (buttonSelected < 0 || buttonSelected >= menuButton.Length)
                buttonSelected = WeaponsButton;

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallItemMakerWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

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
