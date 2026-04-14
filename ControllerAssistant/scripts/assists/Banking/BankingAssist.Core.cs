using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist
    {
        private void OnTickOpen(DaggerfallBankingWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (IsTransactionInputActive(menuWindow))
            {
                SuppressBackBindingForTransaction();
                RefreshNumberpadAttachment(menuWindow);

                if (legend != null && legend.IsBuilt)
                    legend.PositionBottomLeft();

                TickNumberpadMode(menuWindow, cm);
                return;
            }
            else
            {
                RestoreBackBindingIfReady();
            }

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
                    bool show = !legendVisible;
                    legendVisible = show;

                    if (show)
                        EnsureLegendUI(menuWindow, cm);
                    else
                        DestroyLegend();

                    return;
                }
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        private void OnOpened(DaggerfallBankingWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
            closeDeferred = false;
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallBankingWindow closed");
        }

        private void ActivateSelectedButton(DaggerfallBankingWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case DepoGoldButton: InvokeButtonHandler(menuWindow, miDepoGoldButton_OnMouseClick); break;
                case DrawGoldButton: InvokeButtonHandler(menuWindow, miDrawGoldButton_OnMouseClick); break;
                case DepoLOCButton: InvokeButtonHandler(menuWindow, miDepoLOCButton_OnMouseClick); break;
                case DrawLOCButton: InvokeButtonHandler(menuWindow, miDrawLOCButton_OnMouseClick); break;
                case LoanRepayButton: InvokeButtonHandler(menuWindow, miLoanRepayButton_OnMouseClick); break;
                case LoanBorrowButton: InvokeButtonHandler(menuWindow, miLoanBorrowButton_OnMouseClick); break;
                case BuyHouseButton:
                    TriggerReflectedButtonClick(menuWindow, fiBuyHouseButton);
                    break;
                case SellHouseButton: InvokeButtonHandler(menuWindow, miSellHouseButton_OnMouseClick); break;
                case BuyShipButton:
                    TriggerReflectedButtonClick(menuWindow, fiBuyShipButton);
                    break;
                case SellShipButton: InvokeButtonHandler(menuWindow, miSellShipButton_OnMouseClick); break;
                case ExitButton: InvokeButtonHandler(menuWindow, miExitButton_OnMouseClick); break;
            }
        }
    }
}