using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private bool IsItemGridButton(int id)
        {
            return id >= 1000 && id < 1016;
        }

        private int GetGridButtonId(int column, int row)
        {
            return 1000 + (row * ItemGridColumns) + column;
        }

        private bool TryDecodeGridButtonId(int id, out int column, out int row)
        {
            column = 0;
            row = 0;

            if (!IsItemGridButton(id))
                return false;

            int index = id - 1000;
            column = index % ItemGridColumns;
            row = index / ItemGridColumns;
            return true;
        }

        private Rect GetItemGridNativeRect(DaggerfallItemMakerWindow menuWindow, int column, int row)
        {
            Rect scrollerRect = new Rect(253f, 49f, 60f, 148f);

            if (fiItemListScrollerRect != null)
            {
                object rectValue = fiItemListScrollerRect.GetValue(menuWindow);
                if (rectValue is Rect)
                    scrollerRect = (Rect)rectValue;
            }

            // Enhanced layout inside ItemListScroller:
            // x starts after scrollbar area
            // buttons are 25x19
            float x = scrollerRect.x + 9f + (column * 25f);
            float y = scrollerRect.y + (row * 19f);

            return new Rect(x, y, 25f, 19f);
        }

        private int GetItemScrollerScrollIndex(object itemScrollerObj)
        {
            if (itemScrollerObj == null)
                return 0;

            if (fiItemScroller_ScrollBar != null && piScrollBar_ScrollIndex != null)
            {
                object scrollBarObj = fiItemScroller_ScrollBar.GetValue(itemScrollerObj);
                if (scrollBarObj != null)
                {
                    object value = piScrollBar_ScrollIndex.GetValue(scrollBarObj, null);
                    if (value != null)
                        return (int)value;
                }
            }

            if (miItemScroller_GetScrollIndex != null)
            {
                object value = miItemScroller_GetScrollIndex.Invoke(itemScrollerObj, null);
                if (value != null)
                    return (int)value;
            }

            return 0;
        }

        private List<DaggerfallUnityItem> GetItemScrollerItems(object itemScrollerObj)
        {
            if (itemScrollerObj == null)
                return null;

            if (piItemScroller_Items != null)
            {
                object value = piItemScroller_Items.GetValue(itemScrollerObj, null);
                List<DaggerfallUnityItem> itemsFromProperty = value as List<DaggerfallUnityItem>;
                if (itemsFromProperty != null)
                    return itemsFromProperty;
            }

            if (fiItemScroller_Items != null)
            {
                return fiItemScroller_Items.GetValue(itemScrollerObj) as List<DaggerfallUnityItem>;
            }

            return null;
        }

        private int GetItemScrollerTotalRows(object itemScrollerObj)
        {
            List<DaggerfallUnityItem> items = GetItemScrollerItems(itemScrollerObj);
            if (items == null || items.Count == 0)
                return 0;

            return (items.Count + ItemGridColumns - 1) / ItemGridColumns;
        }

        private bool SetItemScrollerScrollIndex(object itemScrollerObj, int newScrollIndex)
        {
            if (itemScrollerObj == null || fiItemScroller_ScrollBar == null || piScrollBar_ScrollIndex == null)
                return false;

            object scrollBarObj = fiItemScroller_ScrollBar.GetValue(itemScrollerObj);
            if (scrollBarObj == null)
                return false;

            if (!piScrollBar_ScrollIndex.CanWrite)
                return false;

            piScrollBar_ScrollIndex.SetValue(scrollBarObj, newScrollIndex, null);
            return true;
        }

        private bool CanScrollItemGridUp(object itemScrollerObj)
        {
            return GetItemScrollerScrollIndex(itemScrollerObj) > 0;
        }

        private bool CanScrollItemGridDown(object itemScrollerObj)
        {
            int scrollRows = GetItemScrollerScrollIndex(itemScrollerObj);
            int totalRows = GetItemScrollerTotalRows(itemScrollerObj);

            return (scrollRows + ItemGridRows) < totalRows;
        }

        private int GetVisibleItemCountForCurrentScroll(object itemScrollerObj)
        {
            if (itemScrollerObj == null)
                return 0;

            Button[] buttons = null;
            if (fiItemScroller_ItemButtons != null)
                buttons = fiItemScroller_ItemButtons.GetValue(itemScrollerObj) as Button[];

            int fallbackVisible = (buttons != null && buttons.Length > 0) ? buttons.Length : 16;

            List<DaggerfallUnityItem> items = GetItemScrollerItems(itemScrollerObj);
            if (items == null || items.Count == 0)
                return fallbackVisible;

            int scrollRows = GetItemScrollerScrollIndex(itemScrollerObj);
            int firstVisibleIndex = scrollRows * ItemGridColumns;
            int remaining = items.Count - firstVisibleIndex;

            if (remaining <= 0)
                return fallbackVisible;

            return Mathf.Min(fallbackVisible, remaining);
        }

        private void ActivateSelectedGridItem(DaggerfallItemMakerWindow menuWindow)
        {
            RefreshItemScrollerInternals(menuWindow);

            Button button = GetVisibleGridButton(menuWindow, itemGridColumn, itemGridRow);
            if (button == null)
                return;

            button.TriggerMouseClick();
        }

        private void ScrollItemGridByRows(DaggerfallItemMakerWindow menuWindow, int deltaRows)
        {
            if (menuWindow == null || fiItemsListScroller == null || deltaRows == 0)
                return;

            object itemScrollerObj = fiItemsListScroller.GetValue(menuWindow);
            if (itemScrollerObj == null)
                return;

            int currentScroll = GetItemScrollerScrollIndex(itemScrollerObj);
            int totalRows = GetItemScrollerTotalRows(itemScrollerObj);

            if (totalRows <= 0)
                return;

            int maxScroll = totalRows - ItemGridRows;
            if (maxScroll < 0)
                maxScroll = 0;

            int newScroll = currentScroll + deltaRows;
            if (newScroll < 0)
                newScroll = 0;
            if (newScroll > maxScroll)
                newScroll = maxScroll;

            if (newScroll == currentScroll)
                return;

            SetItemScrollerScrollIndex(itemScrollerObj, newScroll);
        }

        private void TryMoveWithinItemGrid(DaggerfallItemMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (menuWindow == null || fiItemsListScroller == null)
                return;

            object itemScrollerObj = fiItemsListScroller.GetValue(menuWindow);
            if (itemScrollerObj == null)
                return;

            int visibleCount = GetVisibleItemCountForCurrentScroll(itemScrollerObj);
            if (visibleCount <= 0)
                return;

            int oldCol = itemGridColumn;
            int oldRow = itemGridRow;
            int oldScroll = GetItemScrollerScrollIndex(itemScrollerObj);

            switch (dir)
            {
                case ControllerManager.StickDir8.N:
                    if (itemGridRow > 0)
                    {
                        itemGridRow--;
                    }
                    else
                    {
                        // At top visible row: scroll up if possible, otherwise stop.
                        if (CanScrollItemGridUp(itemScrollerObj))
                        {
                            SetItemScrollerScrollIndex(itemScrollerObj, oldScroll - 1);
                        }
                        return;
                    }
                    break;

                case ControllerManager.StickDir8.S:
                    {
                        int nextVisibleIndex = ((itemGridRow + 1) * ItemGridColumns) + itemGridColumn;

                        if (itemGridRow < ItemGridRows - 1 && nextVisibleIndex < visibleCount)
                        {
                            itemGridRow++;
                        }
                        else
                        {
                            // At bottom visible row (or no next visible slot): scroll down if possible, otherwise stop.
                            if (CanScrollItemGridDown(itemScrollerObj))
                            {
                                SetItemScrollerScrollIndex(itemScrollerObj, oldScroll + 1);
                            }
                            return;
                        }
                    }
                    break;

                case ControllerManager.StickDir8.W:
                    if (itemGridColumn > 0)
                    {
                        itemGridColumn--;
                    }
                    else
                    {
                        if (itemGridRow == 0)
                            buttonSelected = WeaponsButton;
                        else if (itemGridRow <= 4)
                            buttonSelected = ItemPanel;
                        else
                            buttonSelected = ExitButton;

                        return;
                    }
                    break;

                case ControllerManager.StickDir8.E:
                    {
                        int nextVisibleIndex = (itemGridRow * ItemGridColumns) + (itemGridColumn + 1);

                        if (itemGridColumn < ItemGridColumns - 1 && nextVisibleIndex < visibleCount)
                            itemGridColumn++;
                        else
                            return;
                    }
                    break;

                default:
                    return;
            }

            int newIndex = itemGridRow * ItemGridColumns + itemGridColumn;
            if (newIndex >= visibleCount)
            {
                itemGridColumn = oldCol;
                itemGridRow = oldRow;
                return;
            }

            buttonSelected = GetGridButtonId(itemGridColumn, itemGridRow);
        }

        private bool TryGetGridEntryAnchor(int gridId, out int column, out int row)
        {
            column = 0;
            row = 0;

            switch (gridId)
            {
                case Grid00: // from Weapons / top tabs
                    column = 0;
                    row = 0;
                    return true;

                case Grid03: // from ItemPanel
                    column = 0;
                    row = 1;
                    return true;

                case Grid01: // from Enchant
                    column = 0;
                    row = 3;
                    return true;

                case Grid07: // from Exit
                    column = 0;
                    row = 7;
                    return true;

                default:
                    return false;
            }
        }

        private Button GetVisibleGridButton(DaggerfallItemMakerWindow menuWindow, int column, int row)
        {
            if (menuWindow == null || fiItemsListScroller == null)
                return null;

            object itemScrollerObj = fiItemsListScroller.GetValue(menuWindow);
            if (itemScrollerObj == null)
                return null;

            if (fiItemScroller_ItemButtons == null)
                return null;

            Button[] buttons = fiItemScroller_ItemButtons.GetValue(itemScrollerObj) as Button[];
            if (buttons == null || buttons.Length == 0)
                return null;

            int localIndex = row * ItemGridColumns + column;
            if (localIndex < 0 || localIndex >= buttons.Length)
                return null;

            return buttons[localIndex];
        }

        private bool IsGridEntryAnchor(int id)
        {
            return id == Grid00 || id == Grid01 || id == Grid03 || id == Grid07;
        }
    }
}
