using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using System.Reflection;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private void HandleLeftGridRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (leftItemGrid == null)
                return;

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentGridCell();

            if (cm.Action1Released)
            {
                InvokeSelectedVisibleLocalItemLeftClick(menuWindow);
                return;
            }

            if (cm.Action2Released)
            {
                InvokeSelectedVisibleLocalItemRightClick(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedVisibleLocalItemMiddleClick(menuWindow);
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                TryMoveSelectorLeft(menuWindow);
                return;
            }

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                TryMoveSelectorRight(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                TryMoveSelectorUp(menuWindow);
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                TryMoveSelectorDown(menuWindow);
                return;
            }
        }

        private void InvokeSelectedVisibleLocalItemLeftClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemLeftClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miLocalItemListScroller_OnItemLeftClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleLocalItemRightClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemRightClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miLocalItemListScroller_OnItemRightClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleLocalItemMiddleClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemMiddleClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miLocalItemListScroller_OnItemMiddleClick.Invoke(menuWindow, new object[] { item });
        }
    }
}
