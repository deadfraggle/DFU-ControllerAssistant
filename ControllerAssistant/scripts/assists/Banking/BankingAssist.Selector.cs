using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist
    {
        private void TryMoveSelector(DaggerfallBankingWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            if (dir == ControllerManager.StickDir8.NE ||
                dir == ControllerManager.StickDir8.SE ||
                dir == ControllerManager.StickDir8.SW ||
                dir == ControllerManager.StickDir8.NW)
                return;

            SelectorButtonInfo btn = menuButton[buttonSelected];
            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
            }

            if (next > -1)
            {
                buttonSelected = next;
                RefreshSelectorToCurrentButton(menuWindow);
            }
        }

        private Panel GetCurrentRenderPanel(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null || fiMainPanel == null)
                return null;

            return fiMainPanel.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallBankingWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            mainPanel = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            if (buttonSelected < 0 || buttonSelected >= menuButton.Length)
                buttonSelected = DepoGoldButton;

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallBankingWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            mainPanel = currentPanel;

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
