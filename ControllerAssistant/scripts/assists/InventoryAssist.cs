using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// INVENTORYASSIST REGION NAVIGATION MAP
// ----------------------------------------------------------------------------
// The Inventory window is divided into logical navigation regions. The
// selector moves between these regions horizontally, forming a continuous
// navigation ring for controller users.
//
// Horizontal navigation (RStick Left/Right):
//
//        ┌───────────────┐
//        │  SPECIAL ITEMS │
//        └───────┬───────┘
//                │
//                ▼
//   ┌───────────┐   ┌──────────┐   ┌─────────┐   ┌──────────┐
//   │ PAPERDOLL │ ↔ │  LOCAL   │ ↔ │ BUTTONS │ ↔ │  REMOTE  │
//   └───────────┘   └──────────┘   └─────────┘   └─────┬────┘
//        ▲                                              │
//        └──────────────────────────────────────────────┘
//
// Final horizontal ring:
//
//   SpecialItems ↔ PaperDoll ↔ Local ↔ Buttons ↔ Remote ↔ SpecialItems
//
// ---------------------------------------------------------------------------
// Region behavior
// ---------------------------------------------------------------------------
//
// PAPERDOLL
//   • Uses diamond indicator + target list overlay
//   • Up/Down moves between body part targets
//   • Right enters LOCAL grid
//
// LOCAL GRID
//   • Standard selector box
//   • 2 columns × 8 visible rows
//   • Scrolls vertically through player inventory
//   • Left enters PAPERDOLL
//   • Right enters BUTTONS
//
// BUTTONS
//   • Vertical list of actions:
//       Wagon
//       Info
//       Equip
//       Remove
//       Use
//       Gold
//   • Up/Down moves between buttons
//   • Left enters LOCAL
//   • Right enters REMOTE
//
// REMOTE GRID
//   • Standard selector box
//   • 2 columns × 8 visible rows
//   • Scrolls vertically through container inventory
//   • Left enters BUTTONS
//   • Right enters SPECIAL ITEMS
//
// SPECIAL ITEMS
//   • Fixed anchor selector region
//   • Used for quest / special inventory slots
//   • Left enters REMOTE
//   • Right enters PAPERDOLL
//
// ---------------------------------------------------------------------------
// Navigation rules
// ---------------------------------------------------------------------------
//
// Horizontal (RStick Left/Right)
//   Moves between regions using the ring above.
//
// Vertical (RStick Up/Down)
//   Moves inside the active region:
//     • grid rows
//     • button list
//     • paper-doll targets
//
// Selector visuals
//   GRID/BUTTONS/SPECIAL: cyan selector box
//   PAPERDOLL: yellow diamond indicator + target list
//
// State persistence
//   The following values are saved before opening sub-menus:
//
//     resumeRegion
//     resumeColumn
//     resumeRow
//     resumeButtonIndex
//     resumePaperDollIndex
//
// This allows selector state to be restored after actions such as:
//   item info dialogs
//   gold transfer window
//   equipment actions
//
// ---------------------------------------------------------------------------
// Implementation notes
// ---------------------------------------------------------------------------
//
// Movement is handled in two layers:
//
//   AdvanceSelector*State()
//       Mutates logical selector state.
//
//   TryMoveSelector*()
//       Applies selector rebuild / refresh if state changed.
//
// Region transitions use:
//
//   SwitchRegion()
//   SwitchRegionToButtons()
//
// Selector rebuilds automatically when region changes.
// ============================================================================
// ============================================================================
// INVENTORYASSIST DEVELOPER CHEAT SHEET
// ----------------------------------------------------------------------------
// Core navigation flow:
//
// Controller input
//      ↓
// Handle*Region()
//      ↓
// TryMoveSelector*()
//      ↓
// AdvanceSelector*State()
//      ↓
// ApplySelectorStateChange()
//      ↓
// RefreshSelectorToCurrentRegion() / RebuildSelectorForCurrentRegion()
//
// Key concepts:
//
// Regions
//   REGION_LEFT_GRID
//   REGION_RIGHT_GRID
//   REGION_PAPERDOLL
//   REGION_SPECIAL_ITEMS
//   REGION_BUTTONS
//
// Movement layers
//   AdvanceSelector*State()   → changes logical state only
//   TryMoveSelector*()        → handles selector refresh/rebuild
//
// Region transitions
//   SwitchRegion()
//   SwitchRegionToButtons()
//
// Selector visuals
//   SelectorBoxOverlay       → grids/buttons/special items
//   DiamondIndicatorOverlay  → paper doll targets
//
// Persistent selector state
//   resumeRegion
//   resumeColumn
//   resumeRow
//   resumeButtonIndex
//   resumePaperDollIndex
//
// When adding a new region:
//   1. Add REGION_* constant
//   2. Add Handle*Region()
//   3. Add routing in AdvanceSelectorLeft/RightState()
//   4. Define selector size in GetSelectorNativeSizeForCurrentRegion()
// ============================================================================

namespace gigantibyte.DFU.ControllerAssistant
{
    public class InventoryAssist : MenuAssistModule<DaggerfallInventoryWindow>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Selector
        private bool selectorMode = true;
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
        private int resumeButtonIndex = 0;

        private int buttonSelectedIndex = 0;

        private readonly Vector2[] buttonAnchorsNative = new Vector2[]
        {
            new Vector2(225.5f, 13.4f),   // Wagon
            new Vector2(225.5f, 35.4f),   // Info
            new Vector2(225.5f, 57.4f),   // Equip
            new Vector2(225.5f, 79.4f),  // Remove
            new Vector2(225.5f, 102.4f),  // Use
            new Vector2(225.5f, 125.4f),  // Gold
        };

        private DiamondIndicatorOverlay paperDollIndicator = null;
        private int paperDollSelectedIndex = 0;
        private int resumePaperDollIndex = 0;

        private readonly Vector2[] paperDollAnchorsNative = new Vector2[]
        {
            new Vector2(121f, 28f),
            new Vector2(71f, 54f),
            new Vector2(137f, 54f),
            new Vector2(63f, 74f),
            new Vector2(57f, 106f),
            new Vector2(57f, 106f),
            new Vector2(57f, 106f),
            new Vector2(68f, 136f),
            new Vector2(72f, 184f),
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
        private const int REGION_SPECIAL_ITEMS = 3;
        private const int REGION_BUTTONS = 4;

        private int currentRegion = REGION_LEFT_GRID;
        private int gridRowMemory = -1;   // stores 0-based row index for rows 6-8 (visual), else -1

        // Used in EnsureInitialized()
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

        private MethodInfo miUnequipItem;
        private MethodInfo miShowInfoPopup;
        private MethodInfo miUseItem;
        private MethodInfo miNextVariant;

        private PropertyInfo piPlayerEntity;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        // =========================
        // Core tick / main behavior
        // =========================
        protected override void OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Inventory);

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool isAssisting =
                (cm.DPadLeftPressed || cm.DPadRightPressed || cm.DPadUpPressed || cm.DPadDownPressed ||
                 cm.RStickUpPressed || cm.RStickDownPressed || cm.RStickLeftPressed || cm.RStickRightPressed ||
                 cm.RStickUpHeldSlow || cm.RStickDownHeldSlow || cm.RStickLeftHeldSlow || cm.RStickRightHeldSlow ||
                 cm.Action1Pressed || cm.Action2Pressed || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.DPadLeftPressed)
                    CycleTab(menuWindow, -1);

                if (cm.DPadRightPressed)
                    CycleTab(menuWindow, +1);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }

                switch (currentRegion)
                {
                    case REGION_LEFT_GRID:
                        HandleLeftGridRegion(menuWindow, cm);
                        break;

                    case REGION_RIGHT_GRID:
                        HandleRightGridRegion(menuWindow, cm);
                        break;

                    case REGION_PAPERDOLL:
                        HandlePaperDollRegion(menuWindow, cm);
                        break;

                    case REGION_SPECIAL_ITEMS:
                        HandleSpecialItemsRegion(menuWindow, cm);
                        break;

                    case REGION_BUTTONS:
                        HandleButtonsRegion(menuWindow, cm);
                        break;
                }
            }

            if (cm.BackPressed)
            {
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                DestroySelectorBox();
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                menuWindow.CloseWindow();
                return;
            }

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
                closeDeferred = true;

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                DestroySelectorBox();
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                menuWindow.CloseWindow();
                return;
            }
        }

        // =========================
        // Region helpers scaffold
        // =========================
        private void HandleLeftGridRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (leftItemGrid == null)
                return;

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentGridCell();

            if (cm.Action1Pressed)
            {
                InvokeSelectedVisibleLocalItemLeftClick(menuWindow);
                return;
            }

            if (cm.Action2Pressed)
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

        private void HandleRightGridRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (rightItemGrid == null)
                return;

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentGridCell();

            if (cm.Action1Pressed)
            {
                InvokeSelectedVisibleRemoteItemLeftClick(menuWindow);
                return;
            }

            if (cm.Action2Pressed)
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

        private void HandlePaperDollRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsurePaperDollIndicator(menuWindow);
            EnsurePaperDollTargetList(menuWindow);

            if (cm.Action1Pressed)
            {
                InvokeSelectedPaperDollLeftAction(menuWindow);
                return;
            }

            if (cm.Action2Pressed)
            {
                InvokeSelectedPaperDollRightAction(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedPaperDollMiddleAction(menuWindow);
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
                return;

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                SwitchRegion(menuWindow, REGION_LEFT_GRID, 0, 0);
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (paperDollSelectedIndex > 0)
                {
                    paperDollSelectedIndex--;
                    RefreshPaperDollIndicatorPosition();
                    EnsurePaperDollTargetList(menuWindow);
                }
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
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

        private void HandleSpecialItemsRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            // Scaffold only
        }

        private void HandleButtonsRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentRegion();

            if (cm.Action1Pressed)
            {
                InvokeSelectedButton(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (MoveButtonSelectionUp())
                    RefreshSelectorToCurrentRegion();
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                if (MoveButtonSelectionDown())
                    RefreshSelectorToCurrentRegion();
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                RouteButtonsToLeftGrid(menuWindow);
                return;
            }

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                RouteButtonsToRightGrid(menuWindow);
                return;
            }
        }

        // =========================
        // region Selector Helpers
        // =========================
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
                if (selectorBox != null)
                {
                    selectorBox.Destroy();
                    selectorBox = null;
                }

                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();

                panelRenderWindow = current;
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

        private int GetButtonIndexFromLocalGridRow(int row)
        {
            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                default: return 5; // Gold for rows 5-8 visual (and anything below Use)
            }
        }

        private int GetButtonIndexFromRemoteGridRow(int row)
        {
            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                default: return 5; // Gold for rows 5-8 visual (and anything below Use)
            }
        }

        private int GetLocalRowFromButtons()
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon  -> Local 2,1
                case 1: return 0; // Info   -> Local 2,1
                case 2: return 1; // Equip  -> Local 2,2
                case 3: return 2; // Remove -> Local 2,3
                case 4: return 3; // Use    -> Local 2,4
                case 5: return 4; // Gold   -> Local 2,5
                default: return 0;
            }
        }

        private int GetRemoteRowFromButtons()
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon  -> Remote 1,1
                case 1: return 0; // Info   -> Remote 1,1
                case 2: return 1; // Equip  -> Remote 1,2
                case 3: return 2; // Remove -> Remote 1,3
                case 4: return 3; // Use    -> Remote 1,4
                case 5: return 4; // Gold   -> Remote 1,5
                default: return 0;
            }
        }

        private void RouteLeftGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromLocalGridRow(selectedRow));
        }

        private void RouteRightGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromRemoteGridRow(selectedRow));
        }

        private void RouteButtonsToLeftGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetLocalRowFromButtons();
            SwitchRegion(menuWindow, REGION_LEFT_GRID, 1, targetRow); // Local col 2 = zero-based col 1
        }

        private void RouteButtonsToRightGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetRemoteRowFromButtons();
            SwitchRegion(menuWindow, REGION_RIGHT_GRID, 0, targetRow); // Remote col 1 = zero-based col 0
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
            if (currentRegion == REGION_PAPERDOLL)
            {
                DestroySelectorBox();
                return;
            }

            if (currentRegion != previousRegion)
                RebuildSelectorForCurrentRegion(menuWindow);
            else
                RefreshSelectorToCurrentRegion();
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
        private void InvokeSelectedButton(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            SaveResumeSelectorState();

            switch (buttonSelectedIndex)
            {
                case 0: // Wagon
                    if (miWagonButtonClick != null && fiWagonButton != null)
                    {
                        object button = fiWagonButton.GetValue(menuWindow);
                        if (button != null)
                            miWagonButtonClick.Invoke(menuWindow, new object[] { button, Vector2.zero });
                    }
                    break;

                case 1: // Info
                    InvokeSelectActionMode(menuWindow, 0);
                    break;

                case 2: // Equip
                    InvokeSelectActionMode(menuWindow, 1);
                    break;

                case 3: // Remove
                    InvokeSelectActionMode(menuWindow, 2);
                    break;

                case 4: // Use
                    InvokeSelectActionMode(menuWindow, 3);
                    break;

                case 5: // Gold
                    if (miGoldButtonClick != null && fiGoldButton != null)
                    {
                        object button = fiGoldButton.GetValue(menuWindow);
                        if (button != null)
                            miGoldButtonClick.Invoke(menuWindow, new object[] { button, Vector2.zero });
                    }
                    break;
            }
        }
        private void InvokeSelectActionMode(DaggerfallInventoryWindow menuWindow, int modeValue)
        {
            if (menuWindow == null || miSelectActionMode == null || fiSelectedActionMode == null)
                return;

            object currentValue = fiSelectedActionMode.GetValue(menuWindow);
            if (currentValue == null)
                return;

            object nextEnum = Enum.ToObject(currentValue.GetType(), modeValue);
            miSelectActionMode.Invoke(menuWindow, new object[] { nextEnum });
        }
        private bool MoveButtonSelectionUp()
        {
            if (buttonSelectedIndex <= 0)
                return false;

            ClearGridRowMemory();
            buttonSelectedIndex--;
            return true;
        }

        private bool MoveButtonSelectionDown()
        {
            if (buttonSelectedIndex >= buttonAnchorsNative.Length - 1)
                return false;

            ClearGridRowMemory();
            buttonSelectedIndex++;
            return true;
        }
        private Vector2 GetSelectorNativeSizeForCurrentRegion()
        {
            switch (currentRegion)
            {
                case REGION_BUTTONS:
                    return new Vector2(32f, 15f);

                case REGION_SPECIAL_ITEMS:
                    return new Vector2(32f, 24f);

                default:
                    return new Vector2(25f, 19f);
            }
        }
        private void RebuildSelectorForCurrentRegion(DaggerfallInventoryWindow menuWindow)
        {
            DestroySelectorBox();
            EnsureSelectorBox(menuWindow);
            RefreshSelectorToCurrentRegion();
        }
        private void SwitchRegion(DaggerfallInventoryWindow menuWindow, int newRegion)
        {
            if (currentRegion == newRegion)
            {
                RefreshSelectorToCurrentRegion();
                return;
            }

            currentRegion = newRegion;
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
            buttonSelectedIndex = Mathf.Clamp(newButtonIndex, 0, buttonAnchorsNative.Length - 1);
            SwitchRegion(menuWindow, REGION_BUTTONS);
        }
        private void RefreshSelectorToCurrentRegion()
        {
            if (selectorBox == null)
                return;

            if (currentRegion == REGION_BUTTONS)
            {
                Vector2 pos = GetScaledNativePoint(buttonAnchorsNative[buttonSelectedIndex]);
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

        private void RefreshPaperDollIndicatorPosition()
        {
            if (paperDollIndicator == null)
                return;

            if (paperDollSelectedIndex < 0 || paperDollSelectedIndex >= paperDollAnchorsNative.Length)
                return;

            Vector2 pos = NativeInventoryPointToOverlay(paperDollAnchorsNative[paperDollSelectedIndex]);
            paperDollIndicator.SetCenter(pos);
        }

        private EquipSlots? GetPaperDollSelectedSlot()
        {
            switch (paperDollSelectedIndex)
            {
                case 0: return EquipSlots.Head;
                case 1: return EquipSlots.RightArm;
                case 2: return EquipSlots.LeftArm;
                case 3: return EquipSlots.ChestArmor;
                case 4: return EquipSlots.RightHand;
                case 5: return EquipSlots.LeftHand;
                case 6: return EquipSlots.Gloves;
                case 7: return EquipSlots.LegsArmor;
                case 8: return EquipSlots.Feet;
                default: return null;
            }
        }

        //private PlayerEntity GetPlayerEntity(DaggerfallInventoryWindow menuWindow)
        //{
        //    if (menuWindow == null || piPlayerEntity == null)
        //        return null;

        //    return piPlayerEntity.GetValue(menuWindow, null) as PlayerEntity;
        //}

        private DaggerfallUnityItem GetSelectedPaperDollItem(DaggerfallInventoryWindow menuWindow)
        {
            EquipSlots? slot = GetPaperDollSelectedSlot();
            if (!slot.HasValue)
                return null;

            //PlayerEntity player = GetPlayerEntity(menuWindow);
            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.ItemEquipTable == null)
                return null;

            return GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(slot.Value);
        }

        private void InvokeSelectedPaperDollLeftAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);
            
            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);
            
            // ActionModes:
            // 0 = Info
            // 1 = Equip
            // 2 = Remove
            // 3 = Use
            // 4 = Select
            switch (actionMode)
            {
                case 0: // Info
                    if (miShowInfoPopup != null)
                        miShowInfoPopup.Invoke(menuWindow, new object[] { item });
                    break;

                case 1: // Equip
                case 4: // Select
                    if (miUnequipItem != null)
                        miUnequipItem.Invoke(menuWindow, new object[] { item, true });
                    break;

                case 2: // Remove
                        // Paper doll left-click in Remove mode should do nothing useful in vanilla path.
                        // Remove is for item lists/containers, not equipped-slot clicks.
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedPaperDollRightAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);
            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);

            // Mirror DFU GetActionModeRightClick():
            // Equip -> Remove
            // Remove -> Equip
            // Select -> Remove
            if (actionMode == 1)
                actionMode = 2;
            else if (actionMode == 2)
                actionMode = 1;
            else if (actionMode == 4)
                actionMode = 2;

            switch (actionMode)
            {
                case 0: // Info
                    if (miShowInfoPopup != null)
                        miShowInfoPopup.Invoke(menuWindow, new object[] { item });
                    break;

                case 1: // Equip
                case 4: // Select
                    if (miUnequipItem != null)
                        miUnequipItem.Invoke(menuWindow, new object[] { item, true });
                    break;

                case 2: // Remove
                        // Again, no direct paper-doll remove behavior needed here.
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedPaperDollMiddleAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);
            if (item == null || miNextVariant == null)
                return;

            SaveResumeSelectorState();
            miNextVariant.Invoke(menuWindow, new object[] { item });
        }

        // =========================
        // Lifecycle hooks
        // =========================
        protected override void OnOpened(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
            EnsureInventoryGrids(menuWindow);

            if (resumeSelectorMode)
            {
                selectorMode = true;
                currentRegion = resumeRegion;
                selectedColumn = resumeColumn;
                selectedRow = resumeRow;
                buttonSelectedIndex = resumeButtonIndex;
                resumeSelectorMode = false;
                paperDollSelectedIndex = resumePaperDollIndex;
            }

            if (currentRegion == REGION_PAPERDOLL)
            {
                DestroySelectorBox();
                EnsurePaperDollIndicator(menuWindow);
                EnsurePaperDollTargetList(menuWindow);
            }
            else
            {
                EnsureSelectorBox(menuWindow);
                RefreshSelectorToCurrentRegion();
            }
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
        }

        public override void ResetState()
        {
            base.ResetState();

            closeDeferred = false;
            legendVisible = false;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            DestroySelectorBox();
            DestroyPaperDollIndicator();
            DestroyPaperDollTargetList();

            paperDollSelectedIndex = 0;
            resumePaperDollIndex = 0;
            panelRenderWindow = null;
            inventoryUiScale = 1f;
            leftItemGrid = null;
            rightItemGrid = null;

            selectedColumn = 0;
            selectedRow = 0;
            buttonSelectedIndex = 0;
            gridRowMemory = -1;
            currentRegion = REGION_LEFT_GRID;

            selectorMode = true;
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

            miUnequipItem = CacheMethod(type, "UnequipItem");
            miShowInfoPopup = CacheMethod(type, "ShowInfoPopup");
            miUseItem = CacheMethod(type, "UseItem");
            miNextVariant = CacheMethod(type, "NextVariant");

            piPlayerEntity = type.GetProperty("PlayerEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            piNativePanel = type.GetProperty("NativePanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            reflectionCached = true;
        }

        private void EnsureInventoryGrids(DaggerfallInventoryWindow menuWindow)
        {
            if (leftItemGrid != null && rightItemGrid != null)
                return;

            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

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

            float scrollBarWidthNative = 10f;
            float contentInsetXNative = 2f;
            float contentNudgeXNative = -3f;

            float columnStepNative = 25f;
            float rowStepNative = 19f;

            // Local grid
            float localNativeX = 163f;
            float localNativeY = 48f;
            float localNativeW = 59f;

            float localContentX = localNativeX + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;
            float localContentW = localNativeW - scrollBarWidthNative - contentInsetXNative;

            leftItemGrid = new InventoryGrid(
                originX: offsetX + (localContentX * scale),
                originY: offsetY + (localNativeY * scale),
                columns: 2,
                rows: 8,
                cellWidth: columnStepNative * scale,
                cellHeight: rowStepNative * scale
            );

            // Remote grid
            float remoteNativeX = 261f;
            float remoteNativeY = 48f;
            float remoteNativeW = 59f;

            float remoteContentX = remoteNativeX + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;
            float remoteContentW = remoteNativeW - scrollBarWidthNative - contentInsetXNative;

            rightItemGrid = new InventoryGrid(
                originX: offsetX + (remoteContentX * scale),
                originY: offsetY + (remoteNativeY * scale),
                columns: 2,
                rows: 8,
                cellWidth: columnStepNative * scale,
                cellHeight: rowStepNative * scale
            );
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
                    new LegendOverlay.LegendRow("D-Pad Left/Right", "Cycle Tabs"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Left Click"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Right Click"),
                    new LegendOverlay.LegendRow("D-Pad Up", "Middle Click"),
                    new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
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
        // Existing helpers kept alive
        // =========================
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

        private void DestroySelectorBox()
        {
            if (selectorBox != null)
            {
                selectorBox.Destroy();
                selectorBox = null;
            }
        }

        private void DestroyPaperDollIndicator()
        {
            if (paperDollIndicator != null)
            {
                paperDollIndicator.Destroy();
                paperDollIndicator = null;
            }
        }

        private void DestroyPaperDollTargetList()
        {
            if (paperDollTargetList != null)
            {
                paperDollTargetList.Destroy();
                paperDollTargetList = null;
            }
        }

        // =========================
        // Reflection helpers
        // =========================
        private MethodInfo CacheMethod(Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        // =========================
        // Minimal placeholder nested types
        // =========================
        private class SelectorBoxOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private BaseScreenComponent top;
            private BaseScreenComponent bottom;
            private BaseScreenComponent left;
            private BaseScreenComponent right;
            private bool built = false;

            public SelectorBoxOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public bool IsAttached()
            {
                return built && root != null && root.Parent == parent;
            }

            public void BuildCenteredBox(float boxWidth, float boxHeight, float borderThickness, Color borderColor)
            {
                Destroy();

                if (parent == null)
                    return;

                root = new Panel();
                root.AutoSize = AutoSizeModes.None;
                root.Size = new Vector2(boxWidth, boxHeight);
                root.BackgroundColor = Color.clear;

                top = CreateBorderPiece(new Vector2(boxWidth, borderThickness), borderColor);
                top.Position = Vector2.zero;

                bottom = CreateBorderPiece(new Vector2(boxWidth, borderThickness), borderColor);
                bottom.Position = new Vector2(0f, boxHeight - borderThickness);

                left = CreateBorderPiece(new Vector2(borderThickness, boxHeight), borderColor);
                left.Position = Vector2.zero;

                right = CreateBorderPiece(new Vector2(borderThickness, boxHeight), borderColor);
                right.Position = new Vector2(boxWidth - borderThickness, 0f);

                root.Components.Add(top);
                root.Components.Add(bottom);
                root.Components.Add(left);
                root.Components.Add(right);

                parent.Components.Add(root);
                built = true;
            }

            public void SetPosition(Vector2 topLeft)
            {
                if (!built || root == null)
                    return;

                root.Position = topLeft;
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
                top = null;
                bottom = null;
                left = null;
                right = null;
                built = false;
            }

            private BaseScreenComponent CreateBorderPiece(Vector2 size, Color color)
            {
                Panel piece = new Panel();
                piece.AutoSize = AutoSizeModes.None;
                piece.Size = size;
                piece.BackgroundColor = color;
                return piece;
            }
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
    }
}