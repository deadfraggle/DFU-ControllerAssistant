using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;
using System.Reflection;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TravelMapAssist
    {
        private FieldInfo fiExitButton;
        private FieldInfo fiDungeonsFilterButton;
        private FieldInfo fiTemplesFilterButton;
        private FieldInfo fiHomesFilterButton;
        private FieldInfo fiTownsFilterButton;

        private int buttonSelected = 0;
        private bool buttonsInitialized = false;

        private const int ButtonDungeons = 0;
        private const int ButtonTemples = 1;
        private const int ButtonHomes = 2;
        private const int ButtonTowns = 3;
        private const int ButtonExit = 4;

        partial void CacheRegionViewReflection(System.Type type)
        {
            fiExitButton = CacheField(type, "exitButton");
            fiDungeonsFilterButton = CacheField(type, "dungeonsFilterButton");
            fiTemplesFilterButton = CacheField(type, "templesFilterButton");
            fiHomesFilterButton = CacheField(type, "homesFilterButton");
            fiTownsFilterButton = CacheField(type, "townsFilterButton");
        }

        partial void OnOpenedRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            buttonSelected = ButtonDungeons;
            buttonsInitialized = true;
        }

        partial void TickRegionView(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            if (!buttonsInitialized)
                return;

            // Placeholder:
            // region panel mode only; bottom selector behavior to be added next.
            // This is where you will:
            // - move among the 5 buttons with Right Stick
            // - Action1 click current button
            // - N transition back to overworld selector
        }

        partial void ResetRegionViewState()
        {
            buttonSelected = 0;
            buttonsInitialized = false;
        }

        private BaseScreenComponent GetSelectedBottomButton(DaggerfallTravelMapWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case ButtonDungeons:
                    return fiDungeonsFilterButton != null ? fiDungeonsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                case ButtonTemples:
                    return fiTemplesFilterButton != null ? fiTemplesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                case ButtonHomes:
                    return fiHomesFilterButton != null ? fiHomesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                case ButtonTowns:
                    return fiTownsFilterButton != null ? fiTownsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                case ButtonExit:
                    return fiExitButton != null ? fiExitButton.GetValue(menuWindow) as BaseScreenComponent : null;
                default:
                    return null;
            }
        }
    }
}
