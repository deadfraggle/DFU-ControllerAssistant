using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using System.Reflection;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private void HandleRightGridRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (rightItemGrid == null)
                return;

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentGridCell();

            if (cm.Action1Released)
            {
                InvokeSelectedVisibleRemoteItemLeftClick(menuWindow);
                return;
            }

            if (cm.Action2Released)
            {
                InvokeSelectedVisibleRemoteItemRightClick(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedVisibleRemoteItemMiddleClick(menuWindow);
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

        private void InvokeSelectedVisibleRemoteItemLeftClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemLeftClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miRemoteItemListScroller_OnItemLeftClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleRemoteItemRightClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemRightClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miRemoteItemListScroller_OnItemRightClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleRemoteItemMiddleClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemMiddleClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return;

            List<DaggerfallUnityItem> items = piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
            if (items == null)
                return;

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
                return;

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
                return;

            SaveResumeSelectorState();
            miRemoteItemListScroller_OnItemMiddleClick.Invoke(menuWindow, new object[] { item });
        }
    }
}
