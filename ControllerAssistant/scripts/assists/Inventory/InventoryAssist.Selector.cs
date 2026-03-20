using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private List<DaggerfallUnityItem> GetRemoteScrollerItems(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null)
                return null;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return null;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return null;

            return piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
        }

        private List<DaggerfallUnityItem> GetLocalScrollerItems(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null)
                return null;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
                return null;

            PropertyInfo piItems = scrollerObj.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piItems == null)
                return null;

            return piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
        }

        private void RefreshSelectorAttachment(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                if (debugMODE)
                    Debug.Log("[ControllerAssistant][InventoryAssist] Selector attachment changed to a new panel. Rebuilding selector and grids.");

                if (selectorBox != null)
                {
                    selectorBox.Destroy();
                    selectorBox = null;
                }

                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();
                DestroyClothingExpandLabel();
                DestroyClothingTargetList();
                DestroyGearExpandLabel();

                panelRenderWindow = current;

                // Grids are panel-scaled, so force a rebuild for the new panel
                leftItemGrid = null;
                rightItemGrid = null;

                if (currentRegion != REGION_PAPERDOLL && currentRegion != REGION_CLOTHING)
                {
                    EnsureInventoryGrids(menuWindow);
                    EnsureSelectorBox(menuWindow);
                    RefreshSelectorToCurrentRegion(menuWindow);
                }

                return;
            }

            if (selectorBox != null && !selectorBox.IsAttached())
                selectorBox = null;
        }

        private void EnsureSelectorBox(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (selectorBox == null)
            {
                selectorBox = new SelectorBoxOverlay(panelRenderWindow);

                Vector2 nativeSize = GetSelectorNativeSizeForCurrentRegion();

                float selectorWidth = nativeSize.x * inventoryUiScale;
                float selectorHeight = nativeSize.y * inventoryUiScale;
                float borderThickness = Mathf.Max(2f, inventoryUiScale * 0.5f);

                selectorBox.BuildCenteredBox(
                    boxWidth: selectorWidth,
                    boxHeight: selectorHeight,
                    borderThickness: borderThickness,
                    borderColor: new Color(0.1f, 1f, 1f, 1f)
                );
            }
        }

        private InventoryGrid GetCurrentGrid()
        {
            switch (currentRegion)
            {
                case REGION_LEFT_GRID:
                    return leftItemGrid;
                case REGION_RIGHT_GRID:
                    return rightItemGrid;
                default:
                    return null;
            }
        }

        private void RefreshSelectorToCurrentGridCell()
        {
            if (selectorBox == null)
                return;

            InventoryGrid grid = GetCurrentGrid();
            if (grid == null)
                return;

            int col = Mathf.Clamp(selectedColumn, 0, grid.Columns - 1);
            int row = Mathf.Clamp(selectedRow, 0, grid.Rows - 1);

            Rect cell = grid.GetCellRect(col, row);
            selectorBox.SetPosition(new Vector2(cell.x, cell.y));
        }

        private bool IsExtendedGridRow(int row)
        {
            // visual rows 6,7,8 = zero-based rows 5,6,7
            return row >= 5;
        }

        private void RememberGridRowIfExtended()
        {
            if (IsExtendedGridRow(selectedRow))
                gridRowMemory = selectedRow;
            else
                gridRowMemory = -1;
        }

        private void ClearGridRowMemory()
        {
            gridRowMemory = -1;
        }

        private bool HasValidInventoryGrids()
        {
            return leftItemGrid != null &&
                   rightItemGrid != null &&
                   leftItemGrid.CellWidth > 0f &&
                   leftItemGrid.CellHeight > 0f &&
                   rightItemGrid.CellWidth > 0f &&
                   rightItemGrid.CellHeight > 0f;
        }

        private bool AdvanceSelectorLeftState(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_RIGHT_GRID)
            {
                if (selectedColumn > 0)
                {
                    selectedColumn--;
                    return true;
                }

                RouteRightGridToButtons(menuWindow);
                return true;
            }

            if (currentRegion == REGION_LEFT_GRID)
            {
                if (selectedColumn > 0)
                {
                    selectedColumn--;
                    return true;
                }

                currentRegion = REGION_PAPERDOLL;
                paperDollSelectedIndex = 0;
                DestroySelectorBox();
                EnsurePaperDollIndicator(menuWindow);
                EnsurePaperDollTargetList(menuWindow);
                EnsureClothingExpandLabel(menuWindow);
                return true;
            }

            return false;
        }

        private bool AdvanceSelectorRightState(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_LEFT_GRID)
            {
                if (selectedColumn < 1)
                {
                    selectedColumn++;
                    return true;
                }

                RouteLeftGridToButtons(menuWindow);
                return true;
            }

            if (currentRegion == REGION_RIGHT_GRID)
            {
                if (selectedColumn < 1)
                {
                    selectedColumn++;
                    return true;
                }

                currentRegion = REGION_SPECIAL_ITEMS;

                switch (selectedRow)
                {
                    case 0:
                        specialItemSelectedIndex = SPECIAL_BRACELET0;
                        break;
                    case 1:
                        specialItemSelectedIndex = SPECIAL_RING0;
                        break;
                    case 2:
                        specialItemSelectedIndex = SPECIAL_RING0;
                        break;
                    case 3:
                        specialItemSelectedIndex = SPECIAL_BRACER0;
                        break;
                    case 4:
                        specialItemSelectedIndex = SPECIAL_BRACER0;
                        break;
                    case 5:
                        specialItemSelectedIndex = SPECIAL_MARK0;
                        break;
                    case 6:
                        specialItemSelectedIndex = SPECIAL_CRYSTAL0;
                        break;
                    case 7:
                        specialItemSelectedIndex = SPECIAL_CRYSTAL0;
                        break;
                    default:
                        specialItemSelectedIndex = SPECIAL_AMULET0;
                        break;
                }

                return true;
            }

            return false;
        }

        private bool AdvanceSelectorUpState(DaggerfallInventoryWindow menuWindow)
        {
            InventoryGrid grid = GetCurrentGrid();
            if (grid == null)
                return false;

            if (selectedRow > 0)
            {
                selectedRow--;
                return true;
            }

            int scrollIndex = currentRegion == REGION_RIGHT_GRID
                ? GetRemoteItemScrollIndex(menuWindow)
                : GetLocalItemScrollIndex(menuWindow);

            if (scrollIndex > 0)
            {
                if (currentRegion == REGION_RIGHT_GRID)
                    SetRemoteItemScrollIndex(menuWindow, scrollIndex - 1);
                else
                    SetLocalItemScrollIndex(menuWindow, scrollIndex - 1);

                return true;
            }

            return false;
        }

        private bool AdvanceSelectorDownState(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_RIGHT_GRID)
            {
                if (rightItemGrid == null)
                    return false;

                if (selectedRow < rightItemGrid.Rows - 1)
                {
                    selectedRow++;
                    return true;
                }
                else
                {
                    List<DaggerfallUnityItem> items = GetRemoteScrollerItems(menuWindow);
                    if (items != null)
                    {
                        int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
                        int nextRowFirstIndex = ((scrollIndex + selectedRow + 1) * rightItemGrid.Columns);

                        if (nextRowFirstIndex < items.Count)
                        {
                            SetRemoteItemScrollIndex(menuWindow, scrollIndex + 1);
                            return true;
                        }
                    }
                }
            }
            else
            {
                if (leftItemGrid == null)
                    return false;

                if (selectedRow < leftItemGrid.Rows - 1)
                {
                    selectedRow++;
                    return true;
                }
                else
                {
                    List<DaggerfallUnityItem> items = GetLocalScrollerItems(menuWindow);
                    if (items != null)
                    {
                        int scrollIndex = GetLocalItemScrollIndex(menuWindow);
                        int nextRowFirstIndex = ((scrollIndex + selectedRow + 1) * leftItemGrid.Columns);

                        if (nextRowFirstIndex < items.Count)
                        {
                            SetLocalItemScrollIndex(menuWindow, scrollIndex + 1);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void ApplySelectorStateChange(DaggerfallInventoryWindow menuWindow, int previousRegion)
        {
            if (currentRegion == REGION_PAPERDOLL || currentRegion == REGION_CLOTHING)
            {
                DestroySelectorBox();
                return;
            }

            if (currentRegion != previousRegion)
                RebuildSelectorForCurrentRegion(menuWindow);
            else
                RefreshSelectorToCurrentRegion(menuWindow);
        }

        private bool TryMoveSelectorLeft(DaggerfallInventoryWindow menuWindow)
        {
            int previousRegion = currentRegion;
            bool moved = AdvanceSelectorLeftState(menuWindow);
            if (moved)
                ApplySelectorStateChange(menuWindow, previousRegion);
            return moved;
        }

        private bool TryMoveSelectorRight(DaggerfallInventoryWindow menuWindow)
        {
            int previousRegion = currentRegion;
            bool moved = AdvanceSelectorRightState(menuWindow);
            if (moved)
                ApplySelectorStateChange(menuWindow, previousRegion);
            return moved;
        }

        private bool TryMoveSelectorUp(DaggerfallInventoryWindow menuWindow)
        {
            int previousRegion = currentRegion;
            bool moved = AdvanceSelectorUpState(menuWindow);
            if (moved)
                ApplySelectorStateChange(menuWindow, previousRegion);
            return moved;
        }

        private bool TryMoveSelectorDown(DaggerfallInventoryWindow menuWindow)
        {
            int previousRegion = currentRegion;
            bool moved = AdvanceSelectorDownState(menuWindow);
            if (moved)
                ApplySelectorStateChange(menuWindow, previousRegion);
            return moved;
        }

        private int GetLocalItemScrollIndex(DaggerfallInventoryWindow menuWindow)
        {
            object scrollerObj = GetLocalItemScroller(menuWindow);
            if (scrollerObj == null)
                return 0;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo fiScrollBar = scrollerType.GetField("itemListScrollBar", flags);
            if (fiScrollBar == null)
                return 0;

            object scrollBar = fiScrollBar.GetValue(scrollerObj);
            if (scrollBar == null)
                return 0;

            Type sbType = scrollBar.GetType();
            PropertyInfo piScrollIndex = sbType.GetProperty("ScrollIndex", flags);
            if (piScrollIndex == null || !piScrollIndex.CanRead)
                return 0;

            object value = piScrollIndex.GetValue(scrollBar, null);
            if (value == null)
                return 0;

            return (int)value;
        }

        private bool SetLocalItemScrollIndex(DaggerfallInventoryWindow menuWindow, int newScrollIndex)
        {
            object scrollerObj = GetLocalItemScroller(menuWindow);
            if (scrollerObj == null)
                return false;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo fiScrollBar = scrollerType.GetField("itemListScrollBar", flags);
            if (fiScrollBar == null)
                return false;

            object scrollBar = fiScrollBar.GetValue(scrollerObj);
            if (scrollBar == null)
                return false;

            Type sbType = scrollBar.GetType();

            PropertyInfo piScrollIndex = sbType.GetProperty("ScrollIndex", flags);
            if (piScrollIndex == null || !piScrollIndex.CanWrite)
                return false;

            PropertyInfo piTotalUnits = sbType.GetProperty("TotalUnits", flags);
            PropertyInfo piDisplayUnits = sbType.GetProperty("DisplayUnits", flags);

            int totalUnits = 0;
            int displayUnits = 0;

            if (piTotalUnits != null)
            {
                object value = piTotalUnits.GetValue(scrollBar, null);
                if (value != null)
                    totalUnits = (int)value;
            }

            if (piDisplayUnits != null)
            {
                object value = piDisplayUnits.GetValue(scrollBar, null);
                if (value != null)
                    displayUnits = (int)value;
            }

            int maxScrollIndex = Mathf.Max(0, totalUnits - displayUnits);
            newScrollIndex = Mathf.Clamp(newScrollIndex, 0, maxScrollIndex);

            piScrollIndex.SetValue(scrollBar, newScrollIndex, null);
            return true;
        }

        private int GetRemoteItemScrollIndex(DaggerfallInventoryWindow menuWindow)
        {
            object scrollerObj = GetRemoteItemScroller(menuWindow);
            if (scrollerObj == null)
                return 0;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo fiScrollBar = scrollerType.GetField("itemListScrollBar", flags);
            if (fiScrollBar == null)
                return 0;

            object scrollBar = fiScrollBar.GetValue(scrollerObj);
            if (scrollBar == null)
                return 0;

            Type sbType = scrollBar.GetType();
            PropertyInfo piScrollIndex = sbType.GetProperty("ScrollIndex", flags);
            if (piScrollIndex == null || !piScrollIndex.CanRead)
                return 0;

            object value = piScrollIndex.GetValue(scrollBar, null);
            if (value == null)
                return 0;

            return (int)value;
        }

        private bool SetRemoteItemScrollIndex(DaggerfallInventoryWindow menuWindow, int newScrollIndex)
        {
            object scrollerObj = GetRemoteItemScroller(menuWindow);
            if (scrollerObj == null)
                return false;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo fiScrollBar = scrollerType.GetField("itemListScrollBar", flags);
            if (fiScrollBar == null)
                return false;

            object scrollBar = fiScrollBar.GetValue(scrollerObj);
            if (scrollBar == null)
                return false;

            Type sbType = scrollBar.GetType();

            PropertyInfo piScrollIndex = sbType.GetProperty("ScrollIndex", flags);
            if (piScrollIndex == null || !piScrollIndex.CanWrite)
                return false;

            PropertyInfo piTotalUnits = sbType.GetProperty("TotalUnits", flags);
            PropertyInfo piDisplayUnits = sbType.GetProperty("DisplayUnits", flags);

            int totalUnits = 0;
            int displayUnits = 0;

            if (piTotalUnits != null)
            {
                object value = piTotalUnits.GetValue(scrollBar, null);
                if (value != null)
                    totalUnits = (int)value;
            }

            if (piDisplayUnits != null)
            {
                object value = piDisplayUnits.GetValue(scrollBar, null);
                if (value != null)
                    displayUnits = (int)value;
            }

            int maxScrollIndex = Mathf.Max(0, totalUnits - displayUnits);
            newScrollIndex = Mathf.Clamp(newScrollIndex, 0, maxScrollIndex);

            piScrollIndex.SetValue(scrollBar, newScrollIndex, null);
            return true;
        }

        private object GetLocalItemScroller(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null)
                return null;

            return fiLocalItemListScroller.GetValue(menuWindow);
        }

        private object GetRemoteItemScroller(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null)
                return null;

            return fiRemoteItemListScroller.GetValue(menuWindow);
        }

        private void SaveResumeSelectorState()
        {
            resumeSelectorMode = true;
            resumeRegion = currentRegion;
            resumeColumn = selectedColumn;
            resumeRow = selectedRow;
            resumeButtonIndex = buttonSelectedIndex;
            resumePaperDollIndex = paperDollSelectedIndex;
        }

        private Vector2 GetScaledNativePoint(Vector2 nativePoint)
        {
            if (panelRenderWindow == null)
                return nativePoint;

            float nativeWidth = 320f;
            float nativeHeight = 200f;

            float parentWidth = panelRenderWindow.Size.x;
            float parentHeight = panelRenderWindow.Size.y;

            float scaleX = parentWidth / nativeWidth;
            float scaleY = parentHeight / nativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            float scaledNativeWidth = nativeWidth * scale;
            float scaledNativeHeight = nativeHeight * scale;

            float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
            float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

            return new Vector2(
                offsetX + (nativePoint.x * scale),
                offsetY + (nativePoint.y * scale)
            );
        }

        private Vector2 GetSelectorNativeSizeForCurrentRegion()
        {
            switch (currentRegion)
            {
                case REGION_BUTTONS:
                    return new Vector2(32f, 15f);

                case REGION_SPECIAL_ITEMS:
                    return new Vector2(22.6f, 21.5f);

                default:
                    return new Vector2(25f, 19f);
            }
        }

        private void RebuildSelectorForCurrentRegion(DaggerfallInventoryWindow menuWindow)
        {
            DestroySelectorBox();
            EnsureSelectorBox(menuWindow);
            RefreshSelectorToCurrentRegion(menuWindow);
        }

        private void SwitchRegion(DaggerfallInventoryWindow menuWindow, int newRegion)
        {
            if (currentRegion == newRegion)
            {
                if (newRegion == REGION_PAPERDOLL || newRegion == REGION_CLOTHING)
                    DestroySelectorBox();
                else
                    RefreshSelectorToCurrentRegion(menuWindow);

                return;
            }

            currentRegion = newRegion;

            if (newRegion == REGION_PAPERDOLL || newRegion == REGION_CLOTHING)
            {
                DestroySelectorBox();
                return;
            }

            RebuildSelectorForCurrentRegion(menuWindow);
        }

        private void SwitchRegion(DaggerfallInventoryWindow menuWindow, int newRegion, int newColumn, int newRow)
        {
            selectedColumn = newColumn;
            selectedRow = newRow;
            SwitchRegion(menuWindow, newRegion);
        }

        private void SwitchRegionToButtons(DaggerfallInventoryWindow menuWindow, int newButtonIndex)
        {
            if (IsTradeWindow(menuWindow))
                buttonSelectedIndex = ClampToValidTradeButtonIndex(menuWindow, newButtonIndex, +1);
            else
                buttonSelectedIndex = Mathf.Clamp(newButtonIndex, 0, buttonAnchorsNative.Length - 1);

            SwitchRegion(menuWindow, REGION_BUTTONS);
        }

        private void RefreshSelectorToCurrentRegion(DaggerfallInventoryWindow menuWindow = null)
        {
            if (selectorBox == null)
                return;

            if (currentRegion == REGION_BUTTONS)
            {
                Vector2[] anchors = GetActiveButtonAnchors(menuWindow);
                int index = Mathf.Clamp(buttonSelectedIndex, 0, anchors.Length - 1);
                Vector2 pos = GetScaledNativePoint(anchors[index]);
                selectorBox.SetPosition(pos);
                return;
            }

            if (currentRegion == REGION_SPECIAL_ITEMS)
            {
                Vector2 pos = GetScaledNativePoint(specialItemsAnchorsNative[specialItemSelectedIndex]);
                selectorBox.SetPosition(pos);
                return;
            }

            RefreshSelectorToCurrentGridCell();
        }

        private Vector2 NativeInventoryPointToOverlay(Vector2 nativePoint)
        {
            if (panelRenderWindow == null)
                return Vector2.zero;

            float nativeWidth = 320f;
            float nativeHeight = 200f;

            float parentWidth = panelRenderWindow.Size.x;
            float parentHeight = panelRenderWindow.Size.y;

            float scaleX = parentWidth / nativeWidth;
            float scaleY = parentHeight / nativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);
            inventoryUiScale = scale;

            float scaledNativeWidth = nativeWidth * scale;
            float scaledNativeHeight = nativeHeight * scale;

            float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
            float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

            return new Vector2(
                offsetX + nativePoint.x * scale,
                offsetY + nativePoint.y * scale
            );
        }

        private Rect NativeInventoryRectToOverlayRect(float nativeX, float nativeY, float nativeW, float nativeH)
        {
            if (panelRenderWindow == null)
                return new Rect(0, 0, 0, 0);

            float nativeWidth = 320f;
            float nativeHeight = 200f;

            float parentWidth = panelRenderWindow.Size.x;
            float parentHeight = panelRenderWindow.Size.y;

            float scaleX = parentWidth / nativeWidth;
            float scaleY = parentHeight / nativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);
            inventoryUiScale = scale;

            float scaledNativeWidth = nativeWidth * scale;
            float scaledNativeHeight = nativeHeight * scale;

            float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
            float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

            return new Rect(
                offsetX + nativeX * scale,
                offsetY + nativeY * scale,
                nativeW * scale,
                nativeH * scale
            );
        }
    }
}
