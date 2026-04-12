using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private void EnsureInitialized(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            if (cachedReflectionType == type)
                return;

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            fiItemsListScroller = CacheField(type, "itemsListScroller");
            fiPowersList = CacheField(type, "powersList");
            fiSideEffectsList = CacheField(type, "sideEffectsList");
            fiSelectedItem = CacheField(type, "selectedItem");
            fiSelectedTabPage = CacheField(type, "selectedTabPage");

            fiItemListScrollerRect = CacheField(type, "itemListScrollerRect");
            fiSelectedItemRect = CacheField(type, "selectedItemRect");
            fiPowersListRect = CacheField(type, "powersListRect");
            fiSideEffectsListRect = CacheField(type, "sideEffectsListRect");

            miWeaponsAndArmor_OnMouseClick = CacheMethod(type, "WeaponsAndArmor_OnMouseClick");
            miMagicItems_OnMouseClick = CacheMethod(type, "MagicItems_OnMouseClick");
            miClothingAndMisc_OnMouseClick = CacheMethod(type, "ClothingAndMisc_OnMouseClick");
            miIngredients_OnMouseClick = CacheMethod(type, "Ingredients_OnMouseClick");
            miPowersButton_OnMouseClick = CacheMethod(type, "PowersButton_OnMouseClick");
            miSideEffectsButton_OnMouseClick = CacheMethod(type, "SideEffectsButton_OnMouseClick");
            miEnchantButton_OnMouseClick = CacheMethod(type, "EnchantButton_OnMouseClick");
            miSelectedItemButton_OnMouseClick = CacheMethod(type, "SelectedItemButton_OnMouseClick");
            miExitButton_OnMouseClick = CacheMethod(type, "ExitButton_OnMouseClick");
            miNameItemButon_OnMouseClick = CacheMethod(type, "NameItemButon_OnMouseClick");
            miItemListScroller_OnItemClick = CacheMethod(type, "ItemListScroller_OnItemClick");

            cachedReflectionType = type;
        }

        private void RefreshItemScrollerInternals(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null || fiItemsListScroller == null)
                return;

            object itemScrollerObj = fiItemsListScroller.GetValue(menuWindow);
            if (itemScrollerObj == null)
                return;

            System.Type scrollerType = itemScrollerObj.GetType();

            fiItemScroller_ItemButtons = scrollerType.GetField("itemButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            fiItemScroller_ScrollBar = scrollerType.GetField("itemListScrollBar", BindingFlags.Instance | BindingFlags.NonPublic);
            fiItemScroller_Items = scrollerType.GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
            miItemScroller_GetScrollIndex = scrollerType.GetMethod("GetScrollIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            piItemScroller_Items = scrollerType.GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            piScrollBar_ScrollIndex = null;

            if (fiItemScroller_ScrollBar != null)
            {
                object scrollBarObj = fiItemScroller_ScrollBar.GetValue(itemScrollerObj);
                if (scrollBarObj != null)
                {
                    System.Type scrollBarType = scrollBarObj.GetType();
                    piScrollBar_ScrollIndex = scrollBarType.GetProperty("ScrollIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
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

        private void InvokeButtonHandler(DaggerfallItemMakerWindow menuWindow, MethodInfo mi)
        {
            if (menuWindow == null || mi == null)
                return;

            mi.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }
    }
}
