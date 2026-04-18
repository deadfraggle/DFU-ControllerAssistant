using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SpellbookAssist
    {
        private void TickBuying(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            RefreshSelectorToCurrentRegion(menuWindow);

            if (selectorIndex == BuySpellList)
            {
                if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
                {
                    if (CanMoveListUp(menuWindow))
                        SelectListUp();
                }

                if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
                {
                    if (CanMoveListDown(menuWindow))
                        SelectListDown();
                    else
                        MoveSelector(menuButton[BuySpellList].S);
                }

                if (cm.RStickRightPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, BuySpellList, menuButton[BuySpellList].E));

                if (cm.RStickLeftPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, BuySpellList, menuButton[BuySpellList].W));
            }
            else
            {
                if (cm.RStickUpPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].N));

                if (cm.RStickDownPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].S));

                if (cm.RStickRightPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].E));

                if (cm.RStickLeftPressed)
                    MoveSelector(ResolveBuyingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].W));
            }

            if (cm.Action1Released)
                ActivateBuyingRegion(menuWindow);

            RefreshSelectorToCurrentRegion(menuWindow);
        }

        private void ActivateBuyingRegion(DaggerfallSpellBookWindow menuWindow)
        {
            switch (selectorIndex)
            {
                case BuySpellList:
                case BuyButton:
                    ActivateBuy(menuWindow);
                    break;
                case ExitButton:
                    ActivateExit(menuWindow);
                    break;
                case BuyEffect1:
                    ActivateEffectPanel(menuWindow, 0);
                    break;
                case BuyEffect2:
                    ActivateEffectPanel(menuWindow, 1);
                    break;
                case BuyEffect3:
                    ActivateEffectPanel(menuWindow, 2);
                    break;
            }
        }
    }
}
