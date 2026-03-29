using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private sealed class InventoryGoldHandler : IInputMessageBoxAssistHandler
        {
            public bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                return owner.IsInventoryGoldPopup(menuWindow);
            }

            public void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
            }

            public void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                bool isAssisting =
                    (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickLeftPressed ||
                     cm.Action1 || cm.Action2 || cm.Legend);

                if (isAssisting)
                {
                    if (cm.DPadUpPressed)
                        owner.BeginGoldHold(menuWindow, 1);

                    if (cm.DPadDownPressed)
                        owner.BeginGoldHold(menuWindow, -1);

                    if (cm.DPadRightPressed)
                        owner.IncreaseIncrement(menuWindow, cm);

                    if (cm.DPadLeftPressed)
                        owner.DecreaseIncrement(menuWindow, cm);

                    if (cm.RStickLeftPressed)
                        owner.BackspaceGoldAmount(menuWindow);

                    owner.UpdateGoldHold(menuWindow, cm);

                    if (cm.Action1Pressed)
                        owner.SubmitInputBox(menuWindow);

                    if (cm.Action2Pressed)
                        owner.SetGoldAmount(menuWindow, 0);

                    if (cm.LegendPressed)
                    {
                        bool show = !owner.GetLegendVisible();
                        owner.SetLegendVisible(show);

                        if (show)
                            owner.RefreshGoldLegend(menuWindow, cm);
                        else
                            owner.DestroyLegend();
                    }
                }
                else
                {
                    owner.EndGoldHold();
                }
            }

            public void OnClose(InputMessageBoxAssist owner, ControllerManager cm)
            {
                owner.EndGoldHold();
            }
        }
    }
}
