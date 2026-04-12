using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist
    {
        private void EnsureInitialized(DaggerfallPotionMakerWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            if (cachedReflectionType == type)
                return;

            fiParentPanel = CacheField(type, "parentPanel");

            fiRecipesButton = CacheField(type, "recipesButton");
            fiMixButton = CacheField(type, "mixButton");
            fiExitButton = CacheField(type, "exitButton");
            fiIngredientsListScroller = CacheField(type, "ingredientsListScroller");
            fiCauldronListScroller = CacheField(type, "cauldronListScroller");
            fiIngredientsListScrollerRect = CacheField(type, "ingredientsListScrollerRect");
            fiCauldronListScrollerRect = CacheField(type, "cauldronListScrollerRect");
            fiIngredientsListRect = CacheField(type, "ingredientsListRect");
            fiCauldronListRect = CacheField(type, "cauldronListRect");

            miRecipesButton_OnMouseClick = CacheMethod(type, "RecipesButton_OnMouseClick");
            miMixButton_OnMouseClick = CacheMethod(type, "MixButton_OnMouseClick");
            miExitButton_OnMouseClick = CacheMethod(type, "ExitButton_OnMouseClick");
            miIngredientsListScroller_OnItemClick = CacheMethod(type, "IngredientsListScroller_OnItemClick");
            miCauldronListScroller_OnItemClick = CacheMethod(type, "CauldronListScroller_OnItemClick");

            cachedReflectionType = type;
        }

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void InvokeButtonHandler(DaggerfallPotionMakerWindow menuWindow, MethodInfo mi)
        {
            if (menuWindow == null || mi == null)
                return;

            mi.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }
    }
}
