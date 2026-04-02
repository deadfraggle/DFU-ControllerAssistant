using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SpellbookAssist
    {
        private void TickCasting(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            RefreshSelectorToCurrentRegion(menuWindow);

            if (cm.DPadUpPressed)
                ActionMoveSpellUp(menuWindow);

            if (cm.DPadDownPressed)
                ActionMoveSpellDown(menuWindow);

            if (cm.Action2Released)
                ActionSort(menuWindow);

            if (selectorIndex == SpellList)
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
                        MoveSelector(menuButton[SpellList].S);
                }

                if (cm.RStickRightPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, SpellList, menuButton[SpellList].E));

                if (cm.RStickLeftPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, SpellList, menuButton[SpellList].W));
            }
            else
            {
                if (cm.RStickUpPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].N));

                if (cm.RStickDownPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].S));

                if (cm.RStickRightPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].E));

                if (cm.RStickLeftPressed)
                    MoveSelector(ResolveCastingTarget(menuWindow, selectorIndex, menuButton[selectorIndex].W));
            }

            if (cm.Action1Released)
                ActivateCastingRegion(menuWindow);

            RefreshSelectorToCurrentRegion(menuWindow);
        }

        private void ActivateCastingRegion(DaggerfallSpellBookWindow menuWindow)
        {
            switch (selectorIndex)
            {
                case SpellList:
                    TapKey(KeyCode.Return);
                    break;
                case DeleteButton:
                    ActivateDelete(menuWindow);
                    break;
                case UpButton:
                    ActionMoveSpellUp(menuWindow);
                    break;
                case SortButton:
                    ActionSort(menuWindow);
                    break;
                case DownButton:
                    ActionMoveSpellDown(menuWindow);
                    break;
                case ExitusButton:
                    ActivateExit(menuWindow);
                    break;
                case IconButton:
                    ActivateIconPicker(menuWindow);
                    break;
                case Effect1:
                    ActivateEffectPanel(menuWindow, 0);
                    break;
                case Effect2:
                    ActivateEffectPanel(menuWindow, 1);
                    break;
                case Effect3:
                    ActivateEffectPanel(menuWindow, 2);
                    break;
            }
        }
    }
}