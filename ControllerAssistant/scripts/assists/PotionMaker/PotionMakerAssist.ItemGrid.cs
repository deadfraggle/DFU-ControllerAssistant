using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist
    {
        private bool IsIngredientsGridButton(int id)
        {
            return id >= IngredientsGridBase && id < IngredientsGridBase + (IngredientsGridColumns * IngredientsGridRows);
        }

        private bool IsCauldronGridButton(int id)
        {
            return id >= CauldronGridBase && id < CauldronGridBase + (CauldronGridColumns * CauldronGridRows);
        }

        private int GetIngredientsGridButtonId(int column, int row)
        {
            return IngredientsGridBase + (row * IngredientsGridColumns) + column;
        }

        private int GetCauldronGridButtonId(int column, int row)
        {
            return CauldronGridBase + (row * CauldronGridColumns) + column;
        }

        private bool TryDecodeIngredientsGridButtonId(int id, out int column, out int row)
        {
            column = 0;
            row = 0;

            if (!IsIngredientsGridButton(id))
                return false;

            int index = id - IngredientsGridBase;
            column = index % IngredientsGridColumns;
            row = index / IngredientsGridColumns;
            return true;
        }

        private bool TryDecodeCauldronGridButtonId(int id, out int column, out int row)
        {
            column = 0;
            row = 0;

            if (!IsCauldronGridButton(id))
                return false;

            int index = id - CauldronGridBase;
            column = index % CauldronGridColumns;
            row = index / CauldronGridColumns;
            return true;
        }

        private object GetIngredientsScroller(DaggerfallPotionMakerWindow menuWindow)
        {
            if (menuWindow == null || fiIngredientsListScroller == null)
                return null;

            return fiIngredientsListScroller.GetValue(menuWindow);
        }

        private object GetCauldronScroller(DaggerfallPotionMakerWindow menuWindow)
        {
            if (menuWindow == null || fiCauldronListScroller == null)
                return null;

            return fiCauldronListScroller.GetValue(menuWindow);
        }

        private void RefreshScrollerInternals(object scrollerObj)
        {
            if (scrollerObj == null)
                return;

            System.Type scrollerType = scrollerObj.GetType();

            fiItemScroller_ItemButtons = scrollerType.GetField("itemButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            fiItemScroller_ScrollBar = scrollerType.GetField("itemListScrollBar", BindingFlags.Instance | BindingFlags.NonPublic);
            fiItemScroller_Items = scrollerType.GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
            miItemScroller_GetScrollIndex = scrollerType.GetMethod("GetScrollIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            piItemScroller_Items = scrollerType.GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            piScrollBar_ScrollIndex = null;

            if (fiItemScroller_ScrollBar != null)
            {
                object scrollBarObj = fiItemScroller_ScrollBar.GetValue(scrollerObj);
                if (scrollBarObj != null)
                {
                    System.Type scrollBarType = scrollBarObj.GetType();
                    piScrollBar_ScrollIndex = scrollBarType.GetProperty("ScrollIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
        }

        private int GetScrollerScrollIndex(object scrollerObj)
        {
            if (scrollerObj == null)
                return 0;

            if (fiItemScroller_ScrollBar != null && piScrollBar_ScrollIndex != null)
            {
                object scrollBarObj = fiItemScroller_ScrollBar.GetValue(scrollerObj);
                if (scrollBarObj != null)
                {
                    object value = piScrollBar_ScrollIndex.GetValue(scrollBarObj, null);
                    if (value != null)
                        return (int)value;
                }
            }

            if (miItemScroller_GetScrollIndex != null)
            {
                object value = miItemScroller_GetScrollIndex.Invoke(scrollerObj, null);
                if (value != null)
                    return (int)value;
            }

            return 0;
        }

        private bool SetScrollerScrollIndex(object scrollerObj, int newScrollIndex)
        {
            if (scrollerObj == null || fiItemScroller_ScrollBar == null || piScrollBar_ScrollIndex == null)
                return false;

            object scrollBarObj = fiItemScroller_ScrollBar.GetValue(scrollerObj);
            if (scrollBarObj == null)
                return false;

            if (!piScrollBar_ScrollIndex.CanWrite)
                return false;

            piScrollBar_ScrollIndex.SetValue(scrollBarObj, newScrollIndex, null);
            return true;
        }

        private List<DaggerfallUnityItem> GetScrollerItems(object scrollerObj)
        {
            if (scrollerObj == null)
                return null;

            if (piItemScroller_Items != null)
            {
                object value = piItemScroller_Items.GetValue(scrollerObj, null);
                List<DaggerfallUnityItem> itemsFromProperty = value as List<DaggerfallUnityItem>;
                if (itemsFromProperty != null)
                    return itemsFromProperty;
            }

            if (fiItemScroller_Items != null)
                return fiItemScroller_Items.GetValue(scrollerObj) as List<DaggerfallUnityItem>;

            return null;
        }

        private int GetIngredientsVisibleCount(object scrollerObj)
        {
            if (scrollerObj == null)
                return 0;

            RefreshScrollerInternals(scrollerObj);

            Button[] buttons = null;
            if (fiItemScroller_ItemButtons != null)
                buttons = fiItemScroller_ItemButtons.GetValue(scrollerObj) as Button[];

            int fallbackVisible = (buttons != null && buttons.Length > 0) ? buttons.Length : (IngredientsGridColumns * IngredientsGridRows);

            List<DaggerfallUnityItem> items = GetScrollerItems(scrollerObj);
            if (items == null || items.Count == 0)
                return fallbackVisible;

            int scrollRows = GetScrollerScrollIndex(scrollerObj);
            int firstVisibleIndex = scrollRows * IngredientsGridColumns;
            int remaining = items.Count - firstVisibleIndex;

            if (remaining <= 0)
                return 0;

            return Mathf.Min(fallbackVisible, remaining);
        }

        private int GetIngredientsTotalRows(object scrollerObj)
        {
            List<DaggerfallUnityItem> items = GetScrollerItems(scrollerObj);
            if (items == null || items.Count == 0)
                return 0;

            return (items.Count + IngredientsGridColumns - 1) / IngredientsGridColumns;
        }

        private Rect GetIngredientsGridNativeRect(DaggerfallPotionMakerWindow menuWindow, int column, int row)
        {
            Rect listRect = new Rect(11f, 30f, 140f, 142f);

            if (fiIngredientsListScrollerRect != null && fiIngredientsListRect != null)
            {
                object scrollerRectValue = fiIngredientsListScrollerRect.GetValue(menuWindow);
                object listRectValue = fiIngredientsListRect.GetValue(menuWindow);

                if (scrollerRectValue is Rect && listRectValue is Rect)
                {
                    Rect scrollerRect = (Rect)scrollerRectValue;
                    Rect innerListRect = (Rect)listRectValue;
                    listRect = new Rect(scrollerRect.x + innerListRect.x, scrollerRect.y + innerListRect.y, innerListRect.width, innerListRect.height);
                }
            }

            float x = listRect.x + (column * 56f);
            float y = listRect.y + (row * 38f);

            return new Rect(x, y, 28f, 28f);
        }

        private Rect GetCauldronGridNativeRect(DaggerfallPotionMakerWindow menuWindow, int column, int row)
        {
            Rect listRect = new Rect(221f, 30f, 84f, 142f);

            if (fiCauldronListScrollerRect != null && fiCauldronListRect != null)
            {
                object scrollerRectValue = fiCauldronListScrollerRect.GetValue(menuWindow);
                object listRectValue = fiCauldronListRect.GetValue(menuWindow);

                if (scrollerRectValue is Rect && listRectValue is Rect)
                {
                    Rect scrollerRect = (Rect)scrollerRectValue;
                    Rect innerListRect = (Rect)listRectValue;
                    listRect = new Rect(scrollerRect.x + innerListRect.x, scrollerRect.y + innerListRect.y, innerListRect.width, innerListRect.height);
                }
            }

            float x = listRect.x + (column * 56f);
            float y = listRect.y + (row * 38f);

            return new Rect(x, y, 28f, 28f);
        }

        private Button GetVisibleScrollerButton(object scrollerObj, int localIndex)
        {
            if (scrollerObj == null)
                return null;

            RefreshScrollerInternals(scrollerObj);

            if (fiItemScroller_ItemButtons == null)
                return null;

            Button[] buttons = fiItemScroller_ItemButtons.GetValue(scrollerObj) as Button[];
            if (buttons == null || buttons.Length == 0)
                return null;

            if (localIndex < 0 || localIndex >= buttons.Length)
                return null;

            return buttons[localIndex];
        }

        private void ActivateSelectedIngredient(DaggerfallPotionMakerWindow menuWindow)
        {
            object scrollerObj = GetIngredientsScroller(menuWindow);
            if (scrollerObj == null)
                return;

            int localIndex = (ingredientsGridRow * IngredientsGridColumns) + ingredientsGridColumn;
            Button button = GetVisibleScrollerButton(scrollerObj, localIndex);
            if (button == null)
                return;

            button.TriggerMouseClick();
        }

        private void ActivateSelectedCauldronItem(DaggerfallPotionMakerWindow menuWindow)
        {
            object scrollerObj = GetCauldronScroller(menuWindow);
            if (scrollerObj == null)
                return;

            int localIndex = (cauldronGridRow * CauldronGridColumns) + cauldronGridColumn;
            Button button = GetVisibleScrollerButton(scrollerObj, localIndex);
            if (button == null)
                return;

            button.TriggerMouseClick();
        }
    }
}