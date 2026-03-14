using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class InventoryAssist : MenuAssistModule<DaggerfallInventoryWindow>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;  //prevents re-caching Reflection methods

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Selector Mode
        private bool selectorMode = false;
        private SelectorBoxOverlay selectorBox = null;
        private InventoryGrid leftItemGrid;
        private float inventoryUiScale = 1f;
        private int selectedColumn = 0;
        private int selectedRow = 0;
        private FieldInfo fiLocalItemListScroller;
        private FieldInfo fiLocalItems;
        private MethodInfo miLocalItemListScroller_OnItemLeftClick;
        private MethodInfo miLocalItemListScroller_OnItemRightClick;
        private MethodInfo miLocalItemListScroller_OnItemMiddleClick;
        private bool resumeSelectorMode = false;
        private int resumeRegion = REGION_LEFT_GRID;
        private int resumeColumn = 0;
        private int resumeRow = 0;

        private float selectorRepeatDelay = 0.40f;
        private float selectorRepeatInterval = 0.12f;

        private float selectorHoldTimer = 0f;
        private float selectorRepeatTimer = 0f;

        private int selectorHeldX = 0; // -1 left, +1 right
        private int selectorHeldY = 0; // -1 up, +1 down

        private DiamondIndicatorOverlay paperDollIndicator = null;
        private int paperDollSelectedIndex = 0;

        private readonly Vector2[] paperDollAnchorsNative = new Vector2[]
        {
            new Vector2(121f, 28f),   // Head
            new Vector2(71f,  54f),   // RightArm
            new Vector2(137f, 54f),   // LeftArm
            new Vector2(63f, 74f),   // Chest

            new Vector2(57f, 106f),  // RightHand (holding) - shared hand/shield cluster for now
            new Vector2(57f, 106f),  // LeftHand (holding)
            new Vector2(57f, 106f),  // Hands (wearing)

            new Vector2(68f, 136f),  // Legs
            new Vector2(72f, 184f),  // Feet
        };

        private PaperDollTargetListOverlay paperDollTargetList = null;

        private readonly string[] paperDollTargetNames = new string[]
        {
            "Head",
            "Right Arm",
            "Left Arm",
            "Chest",
            "Right Hand",
            "Left Hand",
            "Hands",
            "Legs",
            "Feet",
        };

        private InventoryGrid rightItemGrid;

        private FieldInfo fiRemoteItemListScroller;
        private FieldInfo fiRemoteItems;
        private FieldInfo fiRemoteItemListScrollerRect;

        private MethodInfo miRemoteItemListScroller_OnItemLeftClick;
        private MethodInfo miRemoteItemListScroller_OnItemRightClick;
        private MethodInfo miRemoteItemListScroller_OnItemMiddleClick;

        private const int REGION_LEFT_GRID = 0;
        private const int REGION_RIGHT_GRID = 1;
        private const int REGION_PAPERDOLL = 2;

        private int currentRegion = REGION_LEFT_GRID;

        // Used in EnsureInitialized()
        // Cache for reflection so we don’t re-query every press
        private MethodInfo miWagonButtonClick;
        private FieldInfo fiWagonButton;
        private MethodInfo miSelectTabPage;
        private FieldInfo fiSelectedTabPage;
        private MethodInfo miSelectActionMode;
        private FieldInfo fiSelectedActionMode;
        private MethodInfo miGoldButtonClick;
        private FieldInfo fiGoldButton;

        private FieldInfo fiLocalItemListScrollerRect;
        private PropertyInfo piNativePanel;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        // =========================
        // Core tick / main behavior
        // =========================
        protected override void OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            // Current vanilla binding for this window's open/close action
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Inventory);

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (selectorMode)
                EnsureSelectorVisualState(menuWindow);

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            // Toggle selector mode first so the button is edge-triggered and clean
            if (cm.Action1Pressed)
            {
                ToggleSelectorMode(menuWindow);
                return;
            }

            // Read current controller state
            bool isAssisting =
                (cm.DPadLeftPressed || cm.DPadRightPressed || cm.DPadUpPressed || cm.DPadDownPressed ||
                 // cm.RStickDownPressed || cm.RStickUpPressed || cm.RStickRightPressed || cm.RStickLeftPressed ||
                 cm.RStickH != 0 || cm.RStickV != 0 ||
                 cm.Action1 || cm.Action2 || cm.Legend);

            // NORMAL MODE
            if (isAssisting && !selectorMode)
            {
                if (cm.DPadLeftPressed)
                    CycleTab(menuWindow, -1);

                if (cm.DPadRightPressed)
                    CycleTab(menuWindow, +1);

                if (cm.DPadUpPressed)
                    CycleActionMode(menuWindow, -1);

                if (cm.DPadDownPressed)
                    CycleActionMode(menuWindow, +1);

                if (cm.RStickUpPressed)
                    ToggleWagon(menuWindow);

                if (cm.RStickDownPressed)
                    OpenGoldPopup(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            // SELECTOR MODE
            if (isAssisting && selectorMode)
            {
                if (leftItemGrid != null)
                {
                    if (cm.Action2Pressed)
                    {
                        currentRegion = REGION_PAPERDOLL;
                        paperDollSelectedIndex = 0;

                        RemoveSelectorBoxVisual();
                        DestroyPaperDollIndicator();
                        DestroyPaperDollTargetList();

                        EnsurePaperDollIndicator(menuWindow);
                        EnsurePaperDollTargetList(menuWindow);

                        if (debugMODE)
                            DaggerfallUI.AddHUDText("Paper doll test: Head");

                        return;
                    }

                    if (cm.DPadLeftPressed)
                    {
                        if (currentRegion == REGION_LEFT_GRID)
                            InvokeSelectedVisibleLocalItemLeftClick(menuWindow);
                        else
                            InvokeSelectedVisibleRemoteItemLeftClick(menuWindow);
                        return;
                    }

                    if (cm.DPadRightPressed)
                    {
                        if (currentRegion == REGION_LEFT_GRID)
                            InvokeSelectedVisibleLocalItemRightClick(menuWindow);
                        else
                            InvokeSelectedVisibleRemoteItemRightClick(menuWindow);
                        return;
                    }

                    if (cm.DPadUpPressed)
                    {
                        if (currentRegion == REGION_LEFT_GRID)
                            InvokeSelectedVisibleLocalItemMiddleClick(menuWindow);
                        else
                            InvokeSelectedVisibleRemoteItemMiddleClick(menuWindow);
                        return;
                    }

                    if (currentRegion == REGION_PAPERDOLL)
                    {
                        if (cm.RStickLeftPressed)
                            return;

                        if (cm.RStickRightPressed)
                        {
                            MoveSelectorRight(menuWindow);
                            return;
                        }

                        if (cm.RStickUpHeldSlow)
                        {
                            if (paperDollSelectedIndex > 0)
                            {
                                paperDollSelectedIndex--;
                                RefreshPaperDollIndicatorPosition();
                                EnsurePaperDollTargetList(menuWindow);
                            }
                            return;
                        }

                        if (cm.RStickDownHeldSlow)
                        {
                            if (paperDollSelectedIndex < paperDollAnchorsNative.Length - 1)
                            {
                                paperDollSelectedIndex++;
                                RefreshPaperDollIndicatorPosition();
                                EnsurePaperDollTargetList(menuWindow);
                            }
                            return;
                        }
                    }

                    if (cm.RStickLeftPressed)
                    {
                        if (MoveSelectorLeft(menuWindow))
                            RefreshSelectorToCurrentGridCell();

                        BeginSelectorHold(-1, 0);
                    }

                    if (cm.RStickRightPressed)
                    {
                        if (MoveSelectorRight(menuWindow))
                            RefreshSelectorToCurrentGridCell();

                        BeginSelectorHold(1, 0);
                    }

                    if (cm.RStickUpPressed)
                    {
                        if (MoveSelectorUp(menuWindow))
                            RefreshSelectorToCurrentGridCell();

                        BeginSelectorHold(0, 1);
                    }

                    if (cm.RStickDownPressed)
                    {
                        if (MoveSelectorDown(menuWindow))
                            RefreshSelectorToCurrentGridCell();

                        BeginSelectorHold(0, -1);
                    }

                    UpdateSelectorHold(menuWindow, cm);

                    if (cm.LegendPressed)
                    {
                        LogScrollBarDiagnostics(menuWindow);
                    }
                }
            }

            if (cm.BackPressed)
            {
                DestroySelectorBox();
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                menuWindow.CloseWindow();
                return;
            }

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;

                DestroySelectorBox();
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                menuWindow.CloseWindow();
                return;
            }
        }

        // =========================
        // Assist action helpers
        // =========================

        private void ToggleSelectorMode(DaggerfallInventoryWindow menuWindow)
        {
            if (!selectorMode)
            {
                EnsureSelectorBox(menuWindow);
                selectorMode = (selectorBox != null);
                currentRegion = REGION_LEFT_GRID;
                selectedColumn = 0;
                selectedRow = 0;
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();
                paperDollSelectedIndex = 0;

                if (selectorMode)
                    LogInventoryLayoutDiagnostics(menuWindow);

                if (selectorMode && selectorBox != null && leftItemGrid != null)
                {
                    Rect cell = leftItemGrid.GetCellRect(selectedColumn, selectedRow);
                    selectorBox.SetPosition(new Vector2(cell.x, cell.y));
                }

                if (debugMODE && selectorMode)
                    DaggerfallUI.AddHUDText("Selector mode ON");
            }
            else
            {
                DestroySelectorBox();
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                if (debugMODE)
                    DaggerfallUI.AddHUDText("Selector mode OFF");
            }
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
                float selectorNativeWidth = 25f;
                float selectorNativeHeight = 19f;
                float selectorWidth = selectorNativeWidth * inventoryUiScale;
                float selectorHeight = selectorNativeHeight * inventoryUiScale;
                float borderThickness = Mathf.Max(2f, inventoryUiScale * 0.5f);

                selectorBox.BuildCenteredBox(
                    boxWidth: selectorWidth,
                    boxHeight: selectorHeight,
                    borderThickness: borderThickness,
                    borderColor: new Color(0.1f, 1f, 1f, 1f)
                );
            }
        }

        private void DestroySelectorBox()
        {
            selectorMode = false;
            RemoveSelectorBoxVisual();
        }

        private void RemoveSelectorBoxVisual()
        {
            EndSelectorHold();

            if (selectorBox != null)
            {
                selectorBox.Destroy();
                selectorBox = null;
            }
        }

        private void RefreshSelectorAttachment(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            // If DFU swapped the panel instance, destroy old overlays before dropping references
            if (panelRenderWindow != current)
            {
                if (selectorBox != null)
                {
                    selectorBox.Destroy();
                    selectorBox = null;
                }

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                panelRenderWindow = current;
                legendVisible = false;

                // DO NOT clear selectorMode here
                return;
            }

            // If DFU cleared components, our selector may be detached
            if (selectorBox != null && !selectorBox.IsAttached())
            {
                EndSelectorHold();
                selectorBox = null;
            }
        }

        private void EnsureSelectorVisualState(DaggerfallInventoryWindow menuWindow)
        {
            if (!selectorMode)
                return;

            if (currentRegion == REGION_PAPERDOLL)
            {
                EnsurePaperDollIndicator(menuWindow);
                EnsurePaperDollTargetList(menuWindow);
                return;
            }

            InventoryGrid grid = GetCurrentGrid();
            if (grid == null)
                return;

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentGridCell();
        }

        private void InvokeSelectedVisibleLocalItemLeftClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemLeftClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] localItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] localItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            Debug.Log(string.Format(
                "[CA] LeftClick selector col={0} row={1} scrollIndex={2} computedIndex={3} itemCount={4}",
                selectedColumn, selectedRow, scrollIndex, visibleIndex, items.Count));

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];

            Debug.Log(string.Format(
                "[CA] LeftClick target item long=\"{0}\" short=\"{1}\"",
                item.LongName, item.shortName));

            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miLocalItemListScroller_OnItemLeftClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleRemoteItemLeftClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemLeftClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] RemoteItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            Debug.Log(string.Format(
                "[CA] LeftClick selector col={0} row={1} scrollIndex={2} computedIndex={3} itemCount={4}",
                selectedColumn, selectedRow, scrollIndex, visibleIndex, items.Count));

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];

            Debug.Log(string.Format(
                "[CA] LeftClick target item long=\"{0}\" short=\"{1}\"",
                item.LongName, item.shortName));

            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miRemoteItemListScroller_OnItemLeftClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleLocalItemRightClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemRightClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] localItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] localItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miLocalItemListScroller_OnItemRightClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleRemoteItemRightClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemRightClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] RemoteItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miRemoteItemListScroller_OnItemRightClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleLocalItemMiddleClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null || miLocalItemListScroller_OnItemMiddleClick == null || leftItemGrid == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] localItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] localItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetLocalItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * leftItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miLocalItemListScroller_OnItemMiddleClick.Invoke(menuWindow, new object[] { item });
        }

        private void InvokeSelectedVisibleRemoteItemMiddleClick(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null || miRemoteItemListScroller_OnItemMiddleClick == null || rightItemGrid == null)
                return;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] RemoteItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] RemoteItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            int visibleIndex = ((scrollIndex + selectedRow) * rightItemGrid.Columns) + selectedColumn;

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            SaveSelectorResumeState();
            miRemoteItemListScroller_OnItemMiddleClick.Invoke(menuWindow, new object[] { item });
        }

        private int GetLocalItemScrollIndex(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null)
                return 0;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
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
            if (piScrollIndex == null)
                return 0;

            object value = piScrollIndex.GetValue(scrollBar, null);
            if (value == null)
                return 0;

            return (int)value;
        }

        private int GetRemoteItemScrollIndex(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiRemoteItemListScroller == null)
                return 0;

            object scrollerObj = fiRemoteItemListScroller.GetValue(menuWindow);
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
            if (piScrollIndex == null)
                return 0;

            object value = piScrollIndex.GetValue(scrollBar, null);
            if (value == null)
                return 0;

            return (int)value;
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

        private List<DaggerfallUnityItem> GetLocalScrollerItems(DaggerfallInventoryWindow menuWindow)
        {
            object scrollerObj = GetLocalItemScroller(menuWindow);
            if (scrollerObj == null)
                return null;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
                return null;

            return piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
        }

        private List<DaggerfallUnityItem> GetRemoteScrollerItems(DaggerfallInventoryWindow menuWindow)
        {
            object scrollerObj = GetRemoteItemScroller(menuWindow);
            if (scrollerObj == null)
                return null;

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
                return null;

            return piItems.GetValue(scrollerObj, null) as List<DaggerfallUnityItem>;
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

        private InventoryGrid GetCurrentGrid()
        {
            return (currentRegion == REGION_LEFT_GRID) ? leftItemGrid : rightItemGrid;
        }

        private void RefreshSelectorToCurrentGridCell()
        {
            if (selectorBox == null)
                return;

            InventoryGrid grid = GetCurrentGrid();
            if (grid == null)
                return;

            Rect cell = grid.GetCellRect(selectedColumn, selectedRow);
            selectorBox.SetPosition(new Vector2(cell.x, cell.y));
        }

        private bool HasVisibleItemInCurrentRegion(DaggerfallInventoryWindow menuWindow, int column, int row)
        {
            InventoryGrid grid = GetCurrentGrid();
            if (grid == null)
                return false;

            List<DaggerfallUnityItem> items = null;
            int scrollIndex = 0;

            if (currentRegion == REGION_LEFT_GRID)
            {
                items = GetLocalScrollerItems(menuWindow);
                scrollIndex = GetLocalItemScrollIndex(menuWindow);
            }
            else
            {
                items = GetRemoteScrollerItems(menuWindow);
                scrollIndex = GetRemoteItemScrollIndex(menuWindow);
            }

            if (items == null)
                return false;

            int visibleIndex = ((scrollIndex + row) * grid.Columns) + column;
            return visibleIndex >= 0 && visibleIndex < items.Count;
        }

        private bool TryFindBestVisibleSlotInCurrentRegion(
            DaggerfallInventoryWindow menuWindow,
            int preferredColumn,
            int preferredRow,
            out int foundColumn,
            out int foundRow)
        {
            InventoryGrid grid = GetCurrentGrid();
            foundColumn = 0;
            foundRow = 0;

            if (grid == null)
                return false;

            preferredColumn = Mathf.Clamp(preferredColumn, 0, grid.Columns - 1);
            preferredRow = Mathf.Clamp(preferredRow, 0, grid.Rows - 1);

            int alternateColumn = (preferredColumn == 0) ? 1 : 0;

            // same row first: preferred column, then alternate
            if (HasVisibleItemInCurrentRegion(menuWindow, preferredColumn, preferredRow))
            {
                foundColumn = preferredColumn;
                foundRow = preferredRow;
                return true;
            }

            if (HasVisibleItemInCurrentRegion(menuWindow, alternateColumn, preferredRow))
            {
                foundColumn = alternateColumn;
                foundRow = preferredRow;
                return true;
            }

            // scan upward
            for (int row = preferredRow - 1; row >= 0; row--)
            {
                if (HasVisibleItemInCurrentRegion(menuWindow, preferredColumn, row))
                {
                    foundColumn = preferredColumn;
                    foundRow = row;
                    return true;
                }

                if (HasVisibleItemInCurrentRegion(menuWindow, alternateColumn, row))
                {
                    foundColumn = alternateColumn;
                    foundRow = row;
                    return true;
                }
            }

            // scan downward
            for (int row = preferredRow + 1; row < grid.Rows; row++)
            {
                if (HasVisibleItemInCurrentRegion(menuWindow, preferredColumn, row))
                {
                    foundColumn = preferredColumn;
                    foundRow = row;
                    return true;
                }

                if (HasVisibleItemInCurrentRegion(menuWindow, alternateColumn, row))
                {
                    foundColumn = alternateColumn;
                    foundRow = row;
                    return true;
                }
            }

            return false;
        }

        private bool TryMoveToRightGrid(DaggerfallInventoryWindow menuWindow)
        {
            currentRegion = REGION_RIGHT_GRID;

            int col, row;
            if (TryFindBestVisibleSlotInCurrentRegion(menuWindow, 0, selectedRow, out col, out row))
            {
                selectedColumn = col;
                selectedRow = row;
                return true;
            }

            currentRegion = REGION_LEFT_GRID;
            return false;
        }

        private bool TryMoveToLeftGrid(DaggerfallInventoryWindow menuWindow)
        {
            currentRegion = REGION_LEFT_GRID;

            int col, row;
            if (TryFindBestVisibleSlotInCurrentRegion(menuWindow, 1, selectedRow, out col, out row))
            {
                selectedColumn = col;
                selectedRow = row;
                return true;
            }

            currentRegion = REGION_RIGHT_GRID;
            return false;
        }

        private void SaveSelectorResumeState()
        {
            if (!selectorMode)
                return;

            resumeSelectorMode = true;
            resumeRegion = currentRegion;
            resumeColumn = selectedColumn;
            resumeRow = selectedRow;
        }

        private void BeginSelectorHold(int dx, int dy)
        {
            selectorHeldX = dx;
            selectorHeldY = dy;

            selectorHoldTimer = 0f;
            selectorRepeatTimer = 0f;
        }

        private void EndSelectorHold()
        {
            selectorHeldX = 0;
            selectorHeldY = 0;

            selectorHoldTimer = 0f;
            selectorRepeatTimer = 0f;
        }

        private void UpdateSelectorHold(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            bool stillHolding =
                (selectorHeldX == -1 && cm.RStickH == -1) ||
                (selectorHeldX == 1 && cm.RStickH == 1) ||
                (selectorHeldY == -1 && cm.RStickV == -1) ||
                (selectorHeldY == 1 && cm.RStickV == 1);

            if (!stillHolding)
            {
                EndSelectorHold();
                return;
            }

            selectorHoldTimer += Time.unscaledDeltaTime;

            if (selectorHoldTimer < selectorRepeatDelay)
                return;

            selectorRepeatTimer += Time.unscaledDeltaTime;

            while (selectorRepeatTimer >= selectorRepeatInterval)
            {
                selectorRepeatTimer -= selectorRepeatInterval;

                bool moved = false;

                if (selectorHeldX == -1)
                    moved = MoveSelectorLeft(menuWindow);
                else if (selectorHeldX == 1)
                    moved = MoveSelectorRight(menuWindow);
                else if (selectorHeldY == -1)
                    moved = MoveSelectorDown(menuWindow);
                else if (selectorHeldY == 1)
                    moved = MoveSelectorUp(menuWindow);

                if (moved)
                    RefreshSelectorToCurrentGridCell();
            }
        }

        private bool MoveSelectorLeft(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_PAPERDOLL)
                return false;

            if (currentRegion == REGION_LEFT_GRID)
            {
                if (selectedColumn > 0)
                {
                    selectedColumn--;
                    return true;
                }
                else
                {
                    currentRegion = REGION_PAPERDOLL;
                    paperDollSelectedIndex = 0;

                    RemoveSelectorBoxVisual();
                    EnsurePaperDollIndicator(menuWindow);
                    EnsurePaperDollTargetList(menuWindow);
                    return true;
                }
            }
            else
            {
                if (selectedColumn > 0)
                {
                    selectedColumn--;
                    return true;
                }
                else
                {
                    return TryMoveToLeftGrid(menuWindow);
                }
            }
        }

        private bool MoveSelectorRight(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_PAPERDOLL)
            {
                currentRegion = REGION_LEFT_GRID;
                paperDollSelectedIndex = 0;

                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                selectedColumn = 0;
                selectedRow = 0;

                EnsureSelectorBox(menuWindow);
                RefreshSelectorToCurrentGridCell();
                return true;
            }

            if (currentRegion == REGION_RIGHT_GRID)
            {
                InventoryGrid grid = GetCurrentGrid();
                if (grid == null)
                    return false;

                int oldColumn = selectedColumn;
                selectedColumn = Mathf.Min(grid.Columns - 1, selectedColumn + 1);
                return selectedColumn != oldColumn;
            }
            else
            {
                if (leftItemGrid == null)
                    return false;

                if (selectedColumn < leftItemGrid.Columns - 1)
                {
                    selectedColumn++;
                    return true;
                }
                else
                {
                    return TryMoveToRightGrid(menuWindow);
                }
            }
        }

        private bool MoveSelectorUp(DaggerfallInventoryWindow menuWindow)
        {
            if (currentRegion == REGION_RIGHT_GRID)
            {
                if (selectedRow > 0)
                {
                    selectedRow--;
                    return true;
                }
                else
                {
                    int scrollIndex = GetRemoteItemScrollIndex(menuWindow);
                    if (scrollIndex > 0)
                        return SetRemoteItemScrollIndex(menuWindow, scrollIndex - 1);
                }
            }
            else
            {
                if (selectedRow > 0)
                {
                    selectedRow--;
                    return true;
                }
                else
                {
                    int scrollIndex = GetLocalItemScrollIndex(menuWindow);
                    if (scrollIndex > 0)
                        return SetLocalItemScrollIndex(menuWindow, scrollIndex - 1);
                }
            }

            return false;
        }

        private bool MoveSelectorDown(DaggerfallInventoryWindow menuWindow)
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
                            return SetRemoteItemScrollIndex(menuWindow, scrollIndex + 1);
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
                            return SetLocalItemScrollIndex(menuWindow, scrollIndex + 1);
                    }
                }
            }

            return false;
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

        private void DestroyPaperDollIndicator()
        {
            if (paperDollIndicator != null)
            {
                paperDollIndicator.Destroy();
                paperDollIndicator = null;
            }
        }

        private void EnsurePaperDollIndicator(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (paperDollIndicator == null)
            {
                paperDollIndicator = new DiamondIndicatorOverlay(panelRenderWindow);
                float diamondRadius = Mathf.Max(6f, 7f * inventoryUiScale);
                float pointSize = Mathf.Max(4f, 5f * inventoryUiScale);

                paperDollIndicator.Build(
                    diamondRadius,
                    pointSize,
                    new Color(1f, 1f, 0f, 0.95f)
                );
            }

            RefreshPaperDollIndicatorPosition();
        }

        private void DestroyPaperDollTargetList()
        {
            if (paperDollTargetList != null)
            {
                paperDollTargetList.Destroy();
                paperDollTargetList = null;
            }
        }

        private void EnsurePaperDollTargetList(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (paperDollTargetList == null)
            {
                paperDollTargetList = new PaperDollTargetListOverlay(panelRenderWindow);
                paperDollTargetList.Build(paperDollTargetNames);
            }

            paperDollTargetList.SetSelectedIndex(paperDollSelectedIndex);

            // First-pass native placement inside inventory UI space.
            // Tune these later if needed.
            Rect listRect = NativeInventoryRectToOverlayRect(134f, 147f, 26f, 50f);
            paperDollTargetList.SetRect(listRect);
        }

        private class DiamondIndicatorOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private DiamondPointOverlay top;
            private DiamondPointOverlay right;
            private DiamondPointOverlay bottom;
            private DiamondPointOverlay left;

            public DiamondIndicatorOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(float radius, float pointSize, Color faceColor)
            {
                if (parent == null)
                    return;

                Destroy();

                float size = radius * 2f + pointSize;

                root = DaggerfallUI.AddPanel(new Rect(0, 0, size, size), parent);
                root.BackgroundColor = new Color(0, 0, 0, 0);

                float center = size * 0.5f;
                float halfPoint = pointSize * 0.5f;

                top = new DiamondPointOverlay(root);
                top.Build(new Rect(center - halfPoint, 0, pointSize, pointSize), faceColor);

                right = new DiamondPointOverlay(root);
                right.Build(new Rect(size - pointSize, center - halfPoint, pointSize, pointSize), faceColor);

                bottom = new DiamondPointOverlay(root);
                bottom.Build(new Rect(center - halfPoint, size - pointSize, pointSize, pointSize), faceColor);

                left = new DiamondPointOverlay(root);
                left.Build(new Rect(0, center - halfPoint, pointSize, pointSize), faceColor);
            }

            public void SetCenter(Vector2 center)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(
                    center.x - root.Size.x * 0.5f,
                    center.y - root.Size.y * 0.5f
                );
            }

            public void Destroy()
            {
                if (top != null) top.Destroy();
                if (right != null) right.Destroy();
                if (bottom != null) bottom.Destroy();
                if (left != null) left.Destroy();

                top = null;
                right = null;
                bottom = null;
                left = null;

                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                root = null;
            }

            private class DiamondPointOverlay
            {
                private readonly Panel parent;
                private Panel root;
                private Panel face;
                private Panel borderTop;
                private Panel borderLeft;
                private Panel borderRight;
                private Panel borderBottom;

                public DiamondPointOverlay(Panel parent)
                {
                    this.parent = parent;
                }

                public void Build(Rect rect, Color faceColor)
                {
                    if (parent == null)
                        return;

                    Destroy();

                    root = DaggerfallUI.AddPanel(rect, parent);
                    root.BackgroundColor = new Color(0, 0, 0, 0);

                    float w = rect.width;
                    float h = rect.height;

                    float thin = 1f;
                    float thick = 2f;

                    Color borderColor = new Color(0.08f, 0.16f, 0.45f, 1f);

                    face = DaggerfallUI.AddPanel(
                        new Rect(thin, thin, Mathf.Max(1f, w - thin - thick), Mathf.Max(1f, h - thin - thick)),
                        root);
                    face.BackgroundColor = faceColor;

                    borderTop = DaggerfallUI.AddPanel(
                        new Rect(0, 0, w, thin),
                        root);
                    borderTop.BackgroundColor = borderColor;

                    borderLeft = DaggerfallUI.AddPanel(
                        new Rect(0, 0, thin, h),
                        root);
                    borderLeft.BackgroundColor = borderColor;

                    borderRight = DaggerfallUI.AddPanel(
                        new Rect(w - thick, 0, thick, h),
                        root);
                    borderRight.BackgroundColor = borderColor;

                    borderBottom = DaggerfallUI.AddPanel(
                        new Rect(0, h - thick, w, thick),
                        root);
                    borderBottom.BackgroundColor = borderColor;
                }

                public void Destroy()
                {
                    if (root != null && root.Parent != null)
                    {
                        Panel parentPanel = root.Parent as Panel;
                        if (parentPanel != null)
                            parentPanel.Components.Remove(root);
                    }

                    face = null;
                    borderTop = null;
                    borderLeft = null;
                    borderRight = null;
                    borderBottom = null;
                    root = null;
                }
            }
        }

        private class PaperDollTargetListOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel[] labels;

            public PaperDollTargetListOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string[] rows)
            {
                if (parent == null || rows == null || rows.Length == 0)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                labels = new TextLabel[rows.Length];

                for (int i = 0; i < rows.Length; i++)
                {
                    TextLabel label = new TextLabel();
                    label.Text = rows[i];
                    label.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
                    label.Enabled = true;
                    root.Components.Add(label);
                    labels[i] = label;
                }
            }

            public void SetRect(Rect rect)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, rect.height);

                if (labels == null || labels.Length == 0)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                float topMargin = 14f;
                float bottomMargin = 5f;

                float usableHeight = rect.height - topMargin - bottomMargin;
                float rowHeight = usableHeight / labels.Length;

                float textScale = Mathf.Max(1.8f, rect.height / 62f);

                for (int i = 0; i < labels.Length; i++)
                {
                    float rowY = topMargin + i * rowHeight;

                    if (labels[i] != null)
                    {
                        float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);

                        labels[i].Position = new Vector2(
                            padL,
                            rowY - rowNudge
                        );

                        labels[i].TextScale = textScale;
                    }
                }
            }

            public void SetSelectedIndex(int selectedIndex)
            {
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null)
                    {
                        labels[i].TextColor = (i == selectedIndex)
                            ? new Color(1f, 0.9f, 0.2f, 1f)   // selected = yellow
                            : Color.white;                     // unselected = white
                    }
                }
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                labels = null;
                root = null;
            }
        }

        private void RefreshPaperDollIndicatorPosition()
        {
            if (paperDollIndicator == null)
                return;

            if (paperDollSelectedIndex < 0 || paperDollSelectedIndex >= paperDollAnchorsNative.Length)
                return;

            Vector2 pos = NativeInventoryPointToOverlay(paperDollAnchorsNative[paperDollSelectedIndex]);
            paperDollIndicator.SetCenter(pos);
        }

        private void ToggleWagon(DaggerfallInventoryWindow menuWindow)
        {
            if (miWagonButtonClick == null || menuWindow == null)
                return;

            object wagonButton = fiWagonButton != null ? fiWagonButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
                wagonButton,
                Vector2.zero
            };

            miWagonButtonClick.Invoke(menuWindow, args);
        }

        private void CycleTab(DaggerfallInventoryWindow menuWindow, int direction)
        {
            if (menuWindow == null || miSelectTabPage == null || fiSelectedTabPage == null)
                return;

            object currentValue = fiSelectedTabPage.GetValue(menuWindow);
            if (currentValue == null)
                return;

            int current = (int)currentValue;
            int count = 4;

            int next = (current + direction + count) % count;
            object nextEnum = Enum.ToObject(currentValue.GetType(), next);

            miSelectTabPage.Invoke(menuWindow, new object[] { nextEnum });
        }

        private void CycleActionMode(DaggerfallInventoryWindow menuWindow, int direction)
        {
            if (menuWindow == null || miSelectActionMode == null || fiSelectedActionMode == null)
                return;

            object currentValue = fiSelectedActionMode.GetValue(menuWindow);
            if (currentValue == null)
                return;

            int current = (int)currentValue;

            int[] validModes = new int[] { 0, 1, 2, 3 };

            int currentIndex = 0;
            for (int i = 0; i < validModes.Length; i++)
            {
                if (validModes[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + direction + validModes.Length) % validModes.Length;
            int nextValue = validModes[nextIndex];

            object nextEnum = Enum.ToObject(currentValue.GetType(), nextValue);
            miSelectActionMode.Invoke(menuWindow, new object[] { nextEnum });
        }

        private void OpenGoldPopup(DaggerfallInventoryWindow menuWindow)
        {
            if (miGoldButtonClick == null || menuWindow == null)
                return;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            object goldButton = fiGoldButton != null ? fiGoldButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
                goldButton,
                Vector2.zero
            };

            miGoldButtonClick.Invoke(menuWindow, args);

            if (debugMODE) DaggerfallUI.AddHUDText("Gold popup opened");
        }

        // =========================
        // Lifecycle hooks
        // =========================
        protected override void OnOpened(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
            EnsureInventoryGrids(menuWindow);

            if (resumeSelectorMode)
            {
                selectorMode = true;
                currentRegion = resumeRegion;
                selectedColumn = resumeColumn;
                selectedRow = resumeRow;

                EnsureSelectorVisualState(menuWindow);

                resumeSelectorMode = false;
            }
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallInventoryWindow closed");
        }

        public override void ResetState()
        {
            base.ResetState();

            closeDeferred = false;

            legendVisible = false;

            EndSelectorHold();

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            DestroySelectorBox();
            DestroyPaperDollIndicator();
            DestroyPaperDollTargetList();
            paperDollSelectedIndex = 0;

            if (debugMODE)
                DaggerfallUI.AddHUDText("Selector mode OFF");

            panelRenderWindow = null;
            inventoryUiScale = 1f;
            leftItemGrid = null;
            selectedColumn = 0;
            selectedRow = 0;

            rightItemGrid = null;
            currentRegion = REGION_LEFT_GRID;
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallInventoryWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");

            miWagonButtonClick = CacheMethod(type, "WagonButton_OnMouseClick");
            fiWagonButton = CacheField(type, "wagonButton");

            miSelectTabPage = CacheMethod(type, "SelectTabPage");
            fiSelectedTabPage = CacheField(type, "selectedTabPage");

            miSelectActionMode = CacheMethod(type, "SelectActionMode");
            fiSelectedActionMode = CacheField(type, "selectedActionMode");

            miGoldButtonClick = CacheMethod(type, "GoldButton_OnMouseClick");
            fiGoldButton = CacheField(type, "goldButton");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            fiLocalItemListScrollerRect = CacheField(type, "localItemListScrollerRect");
            fiLocalItemListScroller = CacheField(type, "localItemListScroller");
            fiLocalItems = CacheField(type, "localItems");
            miLocalItemListScroller_OnItemLeftClick = CacheMethod(type, "LocalItemListScroller_OnItemLeftClick");
            miLocalItemListScroller_OnItemRightClick = CacheMethod(type, "LocalItemListScroller_OnItemRightClick");
            miLocalItemListScroller_OnItemMiddleClick = CacheMethod(type, "LocalItemListScroller_OnItemMiddleClick");

            fiRemoteItemListScrollerRect = CacheField(type, "remoteItemListScrollerRect");
            fiRemoteItemListScroller = CacheField(type, "remoteItemListScroller");
            fiRemoteItems = CacheField(type, "remoteItems");
            miRemoteItemListScroller_OnItemLeftClick = CacheMethod(type, "RemoteItemListScroller_OnItemLeftClick");
            miRemoteItemListScroller_OnItemRightClick = CacheMethod(type, "RemoteItemListScroller_OnItemRightClick");
            miRemoteItemListScroller_OnItemMiddleClick = CacheMethod(type, "RemoteItemListScroller_OnItemMiddleClick");

            piNativePanel = type.GetProperty("NativePanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piNativePanel == null)
                Debug.Log("[ControllerAssistant] Missing property: NativePanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                legend.HeaderScale = 6.0f;
                legend.RowScale = 5.0f;
                legend.PadL = 18f;
                legend.PadT = 16f;
                legend.LineGap = 36f;
                legend.ColGap = 22f;
                legend.MarginX = 8f;
                legend.MarginFromBottom = 24f;
                legend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("D-Pad Up Dn", "Buttons"),
                    new LegendOverlay.LegendRow("D-Pad LR", "Tabs"),
                    new LegendOverlay.LegendRow("Right Stick Up", "Wagon"),
                    new LegendOverlay.LegendRow("Right Stick Dn", "Gold"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                panelRenderWindow = current;
                legendVisible = false;
                legend = null;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        // =========================
        // Reflection helpers
        // =========================
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

        // =========================
        // Diagnostics
        // =========================
        void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
        private void LogInventoryLayoutDiagnostics(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            Rect localRect = default(Rect);
            bool hasLocalRect = false;

            if (fiLocalItemListScrollerRect != null)
            {
                object rectObj = fiLocalItemListScrollerRect.GetValue(menuWindow);
                if (rectObj is Rect)
                {
                    localRect = (Rect)rectObj;
                    hasLocalRect = true;
                }
            }

            Debug.Log("===== ControllerAssistant Inventory Diagnostics =====");

            if (hasLocalRect)
                Debug.Log(string.Format(
                    "[CA] localItemListScrollerRect = x:{0} y:{1} w:{2} h:{3}",
                    localRect.x, localRect.y, localRect.width, localRect.height));
            else
                Debug.Log("[CA] localItemListScrollerRect = <unavailable>");

            if (panelRenderWindow != null)
                Debug.Log(string.Format(
                    "[CA] parentPanel.Size = x:{0} y:{1}",
                    panelRenderWindow.Size.x, panelRenderWindow.Size.y));
            else
                Debug.Log("[CA] parentPanel = <null>");

            Panel nativePanel = null;

            if (piNativePanel != null)
                nativePanel = piNativePanel.GetValue(menuWindow, null) as Panel;

            if (nativePanel != null)
            {
                Debug.Log(string.Format(
                    "[CA] nativePanel.Position = x:{0} y:{1}",
                    nativePanel.Position.x, nativePanel.Position.y));

                Debug.Log(string.Format(
                    "[CA] nativePanel.Size = x:{0} y:{1}",
                    nativePanel.Size.x, nativePanel.Size.y));
            }
            else
            {
                Debug.Log("[CA] nativePanel = <null>");
            }

            if (leftItemGrid != null)
                Debug.Log(string.Format(
                    "[CA] leftItemGrid = originX:{0} originY:{1} cols:{2} rows:{3} cellW:{4} cellH:{5}",
                    leftItemGrid.OriginX, leftItemGrid.OriginY,
                    leftItemGrid.Columns, leftItemGrid.Rows,
                    leftItemGrid.CellWidth, leftItemGrid.CellHeight));
            else
                Debug.Log("[CA] leftItemGrid = <null>");
        }

        private void LogLocalInventorySelectionDiagnostics(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            Debug.Log("===== ControllerAssistant Local Inventory Selection Diagnostics =====");
            Debug.Log(string.Format("[CA] selector column={0} row={1}", selectedColumn, selectedRow));

            // Dump localItems collection contents
            object localItemsObj = null;
            if (fiLocalItems != null)
                localItemsObj = fiLocalItems.GetValue(menuWindow);

            if (localItemsObj == null)
            {
                Debug.Log("[CA] localItems = <null>");
            }
            else
            {
                Debug.Log("[CA] localItems type = " + localItemsObj.GetType().FullName);

                System.Collections.IEnumerable enumerable = localItemsObj as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    int index = 0;
                    foreach (object obj in enumerable)
                    {
                        DaggerfallUnityItem item = obj as DaggerfallUnityItem;
                        if (item != null)
                        {
                            Debug.Log(string.Format(
                                "[CA] localItems[{0}] name=\"{1}\" short=\"{2}\" stack={3}",
                                index,
                                item.LongName,
                                item.shortName,
                                item.stackCount));
                        }
                        else
                        {
                            Debug.Log(string.Format("[CA] localItems[{0}] type={1}", index, obj != null ? obj.GetType().FullName : "<null>"));
                        }

                        index++;
                        if (index >= 24)   // keep log manageable for now
                            break;
                    }

                    Debug.Log(string.Format("[CA] localItems first {0} entries logged", index));
                }
                else
                {
                    Debug.Log("[CA] localItems is not IEnumerable");
                }
            }

            // Dump scroller object + its fields/properties
            object scrollerObj = null;
            if (fiLocalItemListScroller != null)
                scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);

            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            Debug.Log("[CA] localItemListScroller type = " + scrollerType.FullName);

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            Debug.Log("----- localItemListScroller FIELDS -----");
            foreach (FieldInfo fi in scrollerType.GetFields(flags))
            {
                object value = null;
                try
                {
                    value = fi.GetValue(scrollerObj);
                }
                catch (Exception ex)
                {
                    value = "<error: " + ex.GetType().Name + ">";
                }

                Debug.Log(string.Format("[CA] field {0} = {1}", fi.Name, value ?? "<null>"));
            }

            Debug.Log("----- localItemListScroller PROPERTIES -----");
            foreach (PropertyInfo pi in scrollerType.GetProperties(flags))
            {
                if (pi.GetIndexParameters().Length > 0)
                    continue;

                object value = null;
                try
                {
                    if (pi.CanRead)
                        value = pi.GetValue(scrollerObj, null);
                    else
                        value = "<write-only>";
                }
                catch (Exception ex)
                {
                    value = "<error: " + ex.GetType().Name + ">";
                }

                Debug.Log(string.Format("[CA] property {0} = {1}", pi.Name, value ?? "<null>"));
            }
        }

        private void LogSelectedVisibleLocalItem(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo piItems = scrollerType.GetProperty("Items", flags);
            if (piItems == null)
            {
                Debug.Log("[CA] localItemListScroller.Items property not found");
                return;
            }

            object itemsObj = piItems.GetValue(scrollerObj, null);
            List<DaggerfallUnityItem> items = itemsObj as List<DaggerfallUnityItem>;
            if (items == null)
            {
                Debug.Log("[CA] localItemListScroller.Items is null or not List<DaggerfallUnityItem>");
                return;
            }

            int visibleIndex = (selectedRow * leftItemGrid.Columns) + selectedColumn;

            Debug.Log("===== ControllerAssistant Selected Visible Local Item =====");
            Debug.Log(string.Format(
                "[CA] selector column={0} row={1} visibleIndex={2} itemCount={3}",
                selectedColumn, selectedRow, visibleIndex, items.Count));

            if (visibleIndex < 0 || visibleIndex >= items.Count)
            {
                Debug.Log("[CA] No item at selected visible slot.");
                return;
            }

            DaggerfallUnityItem item = items[visibleIndex];
            if (item == null)
            {
                Debug.Log("[CA] Selected item is null.");
                return;
            }

            Debug.Log(string.Format(
                "[CA] Selected item: long=\"{0}\" short=\"{1}\" stack={2}",
                item.LongName, item.shortName, item.stackCount));
        }

        private void LogScrollBarDiagnostics(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null || fiLocalItemListScroller == null)
                return;

            object scrollerObj = fiLocalItemListScroller.GetValue(menuWindow);
            if (scrollerObj == null)
            {
                Debug.Log("[CA] localItemListScroller = <null>");
                return;
            }

            Type scrollerType = scrollerObj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo fiScrollBar = scrollerType.GetField("itemListScrollBar", flags);

            if (fiScrollBar == null)
            {
                Debug.Log("[CA] itemListScrollBar field not found");
                return;
            }

            object scrollBar = fiScrollBar.GetValue(scrollerObj);

            if (scrollBar == null)
            {
                Debug.Log("[CA] itemListScrollBar = <null>");
                return;
            }

            Type sbType = scrollBar.GetType();

            Debug.Log("===== ControllerAssistant ScrollBar Diagnostics =====");
            Debug.Log("[CA] scrollBar type = " + sbType.FullName);

            Debug.Log("----- FIELDS -----");
            foreach (FieldInfo fi in sbType.GetFields(flags))
            {
                object value = null;
                try { value = fi.GetValue(scrollBar); }
                catch { value = "<error>"; }

                Debug.Log("[CA] field " + fi.Name + " = " + (value ?? "<null>"));
            }

            Debug.Log("----- PROPERTIES -----");
            foreach (PropertyInfo pi in sbType.GetProperties(flags))
            {
                if (pi.GetIndexParameters().Length > 0)
                    continue;

                object value = null;
                try { value = pi.GetValue(scrollBar, null); }
                catch { value = "<error>"; }

                Debug.Log("[CA] property " + pi.Name + " = " + (value ?? "<null>"));
            }
        }

        // =========================
        // Simple hollow selector box
        // =========================
        private class SelectorBoxOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private Panel borderTop;
            private Panel borderBottom;
            private Panel borderLeft;
            private Panel borderRight;
            private Vector2 position;

            public SelectorBoxOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public bool IsAttached()
            {
                return root != null && root.Parent == parent;
            }

            public void BuildCenteredBox(float boxWidth, float boxHeight, float borderThickness, Color borderColor)
            {
                if (parent == null)
                    return;

                Destroy();

                float x = (parent.Size.x - boxWidth) * 0.5f;
                float y = (parent.Size.y - boxHeight) * 0.5f;

                root = DaggerfallUI.AddPanel(new Rect(x, y, boxWidth, boxHeight), parent);
                root.BackgroundColor = new Color(0, 0, 0, 0);

                borderTop = DaggerfallUI.AddPanel(new Rect(0, 0, boxWidth, borderThickness), root);
                borderBottom = DaggerfallUI.AddPanel(new Rect(0, boxHeight - borderThickness, boxWidth, borderThickness), root);
                borderLeft = DaggerfallUI.AddPanel(new Rect(0, 0, borderThickness, boxHeight), root);
                borderRight = DaggerfallUI.AddPanel(new Rect(boxWidth - borderThickness, 0, borderThickness, boxHeight), root);

                borderTop.BackgroundColor = borderColor;
                borderBottom.BackgroundColor = borderColor;
                borderLeft.BackgroundColor = borderColor;
                borderRight.BackgroundColor = borderColor;

                position = new Vector2(x, y);
            }

            public void SetPosition(Vector2 newPos)
            {
                position = newPos;
                if (root != null)
                    root.Position = position;
            }

            public void MoveBy(float dx, float dy)
            {
                if (root == null || parent == null)
                    return;

                Vector2 newPos = position + new Vector2(dx, dy);

                // Clamp to parent bounds
                float maxX = parent.Size.x - root.Size.x;
                float maxY = parent.Size.y - root.Size.y;

                newPos.x = Mathf.Clamp(newPos.x, 0, maxX);
                newPos.y = Mathf.Clamp(newPos.y, 0, maxY);

                SetPosition(newPos);
            }


            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                root = null;
                borderTop = null;
                borderBottom = null;
                borderLeft = null;
                borderRight = null;
                position = Vector2.zero;
            }
        }
        private class InventoryGrid
        {
            public readonly float OriginX;
            public readonly float OriginY;
            public readonly int Columns;
            public readonly int Rows;
            public readonly float CellWidth;
            public readonly float CellHeight;

            public InventoryGrid(float originX, float originY, int columns, int rows, float cellWidth, float cellHeight)
            {
                OriginX = originX;
                OriginY = originY;
                Columns = columns;
                Rows = rows;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
            }

            public Rect GetCellRect(int column, int row)
            {
                float x = OriginX + (column * CellWidth);
                float y = OriginY + (row * CellHeight);
                return new Rect(x, y, CellWidth, CellHeight);
            }
        }
        private void EnsureInventoryGrids(DaggerfallInventoryWindow menuWindow)
        {
            if (leftItemGrid != null)
                return;

            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
            {
                Debug.Log("[ControllerAssistant] EnsureInventoryGrids(): parentPanel is null.");
                return;
            }

            // Convert DFU native inventory-space rect to parentPanel overlay-space.
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

            // DFU native rect for local item list
            float nativeX = 163f;
            float nativeY = 48f;
            float nativeW = 59f;
            float nativeH = 152f;

            float scrollBarWidthNative = 10f;
            float contentInsetXNative = 2f;
            float contentNudgeXNative = -3f;

            float contentX = nativeX + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;
            float contentW = nativeW - scrollBarWidthNative - contentInsetXNative;

            float columnStepNative = 25f;   // was effectively about 15.5
            float rowStepNative = 19f;      // was about 25.33

            leftItemGrid = new InventoryGrid(
                originX: offsetX + (contentX * scale),
                originY: offsetY + (nativeY * scale),
                columns: 2,
                rows: 8,
                cellWidth: columnStepNative * scale,
                cellHeight: rowStepNative * scale
            );

            float remoteNativeX = 261f;
            float remoteNativeY = 48f;
            float remoteNativeW = 59f;
            float remoteNativeH = 152f;

            float remoteContentX = remoteNativeX + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;

            rightItemGrid = new InventoryGrid(
                originX: offsetX + (remoteContentX * scale),
                originY: offsetY + (remoteNativeY * scale),
                columns: 2,
                rows: 8,
                cellWidth: columnStepNative * scale,
                cellHeight: rowStepNative * scale
            );
        }


    }
}