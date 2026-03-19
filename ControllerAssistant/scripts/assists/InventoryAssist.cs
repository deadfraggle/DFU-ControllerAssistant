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
    public partial class InventoryAssist : IMenuAssist
    {
        private const bool debugMODE = true;

        private Type cachedReflectionType = null;
        private PropertyInfo piTradeWindowMode = null;

        private string lastLoggedWindowTypeName = null;
        private string lastLoggedTradeModeName = null;
        private bool tradeButtonsLoggedThisOpen = false;

        private bool wasOpen = false;

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
        private readonly Vector2[] tradeButtonAnchorsNative = new Vector2[]
        {
            new Vector2(225.5f, 13.4f),   // Wagon
            new Vector2(225.5f, 35.4f),   // Info
            new Vector2(225.5f, 57.4f),   // Select
            new Vector2(225.5f, 111.6f),   // Steal (Buy only)
            new Vector2(225.5f, 133.7f),  // ModeAction (Buy/Sell/Repair/Identify)
            new Vector2(225.5f, 155.7f),  // Clear
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

        private ClothingExpandOverlay clothingExpandOverlay = null;
        private ClothingTargetListOverlay clothingTargetList = null;
        private GearExpandOverlay gearExpandOverlay = null;
        private int clothingSelectedIndex = 0;

        private readonly string[] clothingTargetNames = new string[]
        {
            "Hat",
            "Cloak",
            "Chest",
            "Gloves",
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
        private const int REGION_CLOTHING = 3;
        private const int REGION_SPECIAL_ITEMS = 4;
        private const int REGION_BUTTONS = 5;

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

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
        private FieldInfo fiWindowBinding;

        // Trade window button fields
        private FieldInfo fiSelectButton;
        private FieldInfo fiStealButton;
        private FieldInfo fiModeActionButton;
        private FieldInfo fiClearButton;

        // Trade window button methods
        private MethodInfo miSelectButtonClick;
        private MethodInfo miStealButtonClick;
        private MethodInfo miModeActionButtonClick;
        private MethodInfo miClearButtonClick;

        private bool closeDeferred = false;

        //private AnchorEditor editor;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallInventoryWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallInventoryWindow menuWindow = top as DaggerfallInventoryWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Inventory);

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            EnsureInitialized(menuWindow);
            LogInventoryWindowStateIfChanged(menuWindow);

            bool gridsWereInvalid = !HasValidInventoryGrids();

            EnsureInventoryGrids(menuWindow);


            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            if (gridsWereInvalid && HasValidInventoryGrids())
            {
                if (debugMODE)
                    Debug.Log("[ControllerAssistant][InventoryAssist] Inventory grids became valid during tick.");

                if (selectorBox != null &&
                    currentRegion != REGION_PAPERDOLL &&
                    currentRegion != REGION_CLOTHING)
                {
                    RebuildSelectorForCurrentRegion(menuWindow);
                }
            }

            if (IsTradeWindow(menuWindow) && !tradeButtonsLoggedThisOpen)
            {
                bool anyTradeButtonLive =
                    HasLiveButton(menuWindow, fiWagonButton) ||
                    HasLiveButton(menuWindow, fiSelectButton) ||
                    HasLiveButton(menuWindow, fiStealButton) ||
                    HasLiveButton(menuWindow, fiModeActionButton) ||
                    HasLiveButton(menuWindow, fiClearButton);

                if (anyTradeButtonLive)
                {
                    LogTradeButtonsIfChanged(menuWindow, "first-live-tick");

                    if (IsTradeBuyMode(menuWindow))
                        LogSelectorState(menuWindow, "buy-first-live-tick");

                    tradeButtonsLoggedThisOpen = true;
                }
            }

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

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
                    //editor.Toggle();
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

                    case REGION_CLOTHING:
                        HandleClothingRegion(menuWindow, cm);
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
                DestroyOwnedUi();

                //menuWindow.CloseWindow();
                return;
            }

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
                closeDeferred = true;

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;

                DestroyOwnedUi();

                //menuWindow.CloseWindow();
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

                // Row-aware entry from right grid into left special-items column
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
                        // Fallback for rows without a direct special-items counterpart
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
        private int GetTradeLeftGridRowFromButton(int buttonIndex)
        {
            // Map visible buttons (top → bottom) to reasonable rows
            // We’ll tune later if needed

            switch (buttonIndex)
            {
                case 0: return 0; // Wagon
                case 1: return 0; // Info
                case 2: return 1; // Select
                case 3: return 2; // Steal (if present)
                case 4: return 3; // Buy / Sell / Repair
                case 5: return 4; // Clear
                default: return 0;
            }
        }


        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
            EnsureInventoryGrids(menuWindow);

            LogInventoryWindowStateIfChanged(menuWindow, "opened");

            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}


            if (resumeSelectorMode)
            {
                selectorMode = true;
                currentRegion = resumeRegion;
                selectedColumn = resumeColumn;
                selectedRow = resumeRow;
                buttonSelectedIndex = resumeButtonIndex;
                paperDollSelectedIndex = resumePaperDollIndex;   // RESTORE FIRST

                resumeSelectorMode = false;
                resumePaperDollIndex = 0;                        // optional cleanup after restore
            }

            if (currentRegion == REGION_PAPERDOLL)
            {
                DestroySelectorBox();
                EnsurePaperDollIndicator(menuWindow);
                EnsurePaperDollTargetList(menuWindow);
                EnsureClothingExpandLabel(menuWindow);
            }
            else if (currentRegion == REGION_CLOTHING)
            {
                DestroySelectorBox();
                EnsureClothingTargetList(menuWindow);
                EnsureGearExpandLabel(menuWindow);
            }
            else
            {
                EnsureSelectorBox(menuWindow);
                LogSelectorState(menuWindow, "after-EnsureSelectorBox");
                RefreshSelectorToCurrentRegion(menuWindow);
                LogSelectorState(menuWindow, "after-RefreshSelectorToCurrentRegion");
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();
        }

        public void ResetState()
        {
            lastLoggedWindowTypeName = null;
            lastLoggedTradeModeName = null;
            tradeButtonsLoggedThisOpen = false;

            wasOpen = false;
            closeDeferred = false;
            legendVisible = false;

            DestroyOwnedUi();

            paperDollSelectedIndex = 0;
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
            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            if (cachedReflectionType == type)
                return;

            if (debugMODE)
                Debug.Log("[ControllerAssistant][InventoryAssist] Rebuilding reflection cache for type: " + type.FullName);

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

            fiSelectButton = CacheField(type, "selectButton");
            fiStealButton = CacheField(type, "stealButton");
            fiModeActionButton = CacheField(type, "modeActionButton");
            fiClearButton = CacheField(type, "clearButton");

            miSelectButtonClick = CacheMethod(type, "SelectButton_OnMouseClick");
            miStealButtonClick = CacheMethod(type, "StealButton_OnMouseClick");
            miModeActionButtonClick = CacheMethod(type, "ModeActionButton_OnMouseClick");
            miClearButtonClick = CacheMethod(type, "ClearButton_OnMouseClick");

            piNativePanel = type.GetProperty("NativePanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            piTradeWindowMode = type.GetProperty("WindowMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            cachedReflectionType = type;
        }

        private void EnsureInventoryGrids(DaggerfallInventoryWindow menuWindow)
        {
            if (HasValidInventoryGrids())
                return;

            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (panelRenderWindow.Size.x <= 0f || panelRenderWindow.Size.y <= 0f)
            {
                leftItemGrid = null;
                rightItemGrid = null;
                return;
            }

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

            Rect localRectNative = new Rect(163f, 48f, 59f, 152f);
            Rect remoteRectNative = new Rect(261f, 48f, 59f, 152f);

            if (fiLocalItemListScrollerRect != null)
            {
                object value = fiLocalItemListScrollerRect.GetValue(menuWindow);
                if (value != null && value is Rect)
                    localRectNative = (Rect)value;
            }

            if (fiRemoteItemListScrollerRect != null)
            {
                object value = fiRemoteItemListScrollerRect.GetValue(menuWindow);
                if (value != null && value is Rect)
                    remoteRectNative = (Rect)value;
            }

            float localContentX = localRectNative.x + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;
            float localContentY = localRectNative.y;

            leftItemGrid = new InventoryGrid(
                originX: offsetX + (localContentX * scale),
                originY: offsetY + (localContentY * scale),
                columns: 2,
                rows: 8,
                cellWidth: columnStepNative * scale,
                cellHeight: rowStepNative * scale
            );

            float remoteContentX = remoteRectNative.x + scrollBarWidthNative + contentInsetXNative + contentNudgeXNative;
            float remoteContentY = remoteRectNative.y;

            rightItemGrid = new InventoryGrid(
                originX: offsetX + (remoteContentX * scale),
                originY: offsetY + (remoteContentY * scale),
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
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                panelRenderWindow = current;
                legendVisible = false;
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

        private void DestroyClothingExpandLabel()
        {
            if (clothingExpandOverlay != null)
            {
                clothingExpandOverlay.Destroy();
                clothingExpandOverlay = null;
            }
        }

        private void DestroyClothingTargetList()
        {
            if (clothingTargetList != null)
            {
                clothingTargetList.Destroy();
                clothingTargetList = null;
            }
        }

        private void DestroyGearExpandLabel()
        {
            if (gearExpandOverlay != null)
            {
                gearExpandOverlay.Destroy();
                gearExpandOverlay = null;
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

        private bool IsTradeWindow(DaggerfallInventoryWindow menuWindow)
        {
            return menuWindow is DaggerfallTradeWindow;
        }

        private string GetTradeWindowModeName(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return null;

            if (piTradeWindowMode == null)
                return null;

            object value = piTradeWindowMode.GetValue(menuWindow, null);
            return value != null ? value.ToString() : null;
        }

        private bool IsTradeBuyMode(DaggerfallInventoryWindow menuWindow)
        {
            return string.Equals(GetTradeWindowModeName(menuWindow), "Buy", StringComparison.Ordinal);
        }

        private bool IsTradeButtonIndexValid(DaggerfallInventoryWindow menuWindow, int index)
        {
            if (!IsTradeWindow(menuWindow))
                return index >= 0 && index < buttonAnchorsNative.Length;

            switch (index)
            {
                case 0: // Wagon
                    return true;
                case 1: // Info
                    return true;
                case 2: // Select
                    return true;
                case 3: // Steal
                    return IsTradeBuyMode(menuWindow);
                case 4: // ModeAction
                    return true;
                case 5: // Clear
                    return true;
                default:
                    return false;
            }
        }

        private int ClampToValidTradeButtonIndex(DaggerfallInventoryWindow menuWindow, int index, int fallbackDirection)
        {
            if (!IsTradeWindow(menuWindow))
                return Mathf.Clamp(index, 0, buttonAnchorsNative.Length - 1);

            index = Mathf.Clamp(index, 0, tradeButtonAnchorsNative.Length - 1);

            if (IsTradeButtonIndexValid(menuWindow, index))
                return index;

            int dir = fallbackDirection < 0 ? -1 : 1;
            int probe = index;

            while (probe >= 0 && probe < tradeButtonAnchorsNative.Length)
            {
                probe += dir;
                if (probe >= 0 && probe < tradeButtonAnchorsNative.Length && IsTradeButtonIndexValid(menuWindow, probe))
                    return probe;
            }

            for (int i = 0; i < tradeButtonAnchorsNative.Length; i++)
            {
                if (IsTradeButtonIndexValid(menuWindow, i))
                    return i;
            }

            return 0;
        }

        private Vector2[] GetActiveButtonAnchors(DaggerfallInventoryWindow menuWindow)
        {
            return IsTradeWindow(menuWindow) ? tradeButtonAnchorsNative : buttonAnchorsNative;
        }

        private void LogInventoryWindowStateIfChanged(DaggerfallInventoryWindow menuWindow, string reason = null)
        {
            if (!debugMODE || menuWindow == null)
                return;

            string typeName = menuWindow.GetType().Name;
            string modeName = IsTradeWindow(menuWindow) ? GetTradeWindowModeName(menuWindow) : null;

            bool changed =
                typeName != lastLoggedWindowTypeName ||
                modeName != lastLoggedTradeModeName ||
                !string.IsNullOrEmpty(reason);

            if (!changed)
                return;

            Debug.Log(string.Format(
                "[ControllerAssistant][InventoryAssist] WindowState reason={0} type={1} isTrade={2} mode={3}",
                string.IsNullOrEmpty(reason) ? "tick-change" : reason,
                typeName,
                IsTradeWindow(menuWindow),
                string.IsNullOrEmpty(modeName) ? "-" : modeName
            ));

            lastLoggedWindowTypeName = typeName;
            lastLoggedTradeModeName = modeName;
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

                float scaleTo4K = rect.height / 540f;   // 50 native height becomes 540 px at 4K
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

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
        private class ClothingExpandOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel label;

            public ClothingExpandOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string text)
            {
                if (parent == null)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                label = new TextLabel();
                label.Text = text;
                label.TextColor = Color.white;
                label.Enabled = true;
                root.Components.Add(label);
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null || label == null)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                float scaleTo4K = rect.height / 540f;   // 50 native height becomes 540 px at 4K
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

                // Match one row of the 9-row paper-doll list
                float referenceListHeight = rect.height;
                float referenceUsableHeight = referenceListHeight - topMargin - bottomMargin;
                float rowHeight = referenceUsableHeight / 9f;

                float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);

                // Build a compact single-row panel instead of using the full 50-high rect
                float compactHeight = topMargin + rowHeight + bottomMargin;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, compactHeight);

                label.Position = new Vector2(
                    padL,
                    topMargin - rowNudge
                );

                label.TextScale = textScale;
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                label = null;
                root = null;
            }
        }

        private class ClothingTargetListOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel[] labels;

            public ClothingTargetListOverlay(Panel parent)
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
                    label.TextColor = Color.white;
                    label.Enabled = true;
                    root.Components.Add(label);
                    labels[i] = label;
                }
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(rect.x, rect.y);

                if (labels == null || labels.Length == 0)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                // Use the same vertical metrics as the paper-doll list:
                // reference rect = 50 native units high
                float referenceRectHeight = 50f * (rect.height / 78f);

                float scaleTo4K = referenceRectHeight / 540f;
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

                // Paper-doll uses 9 rows. Borrow its row height exactly.
                float referenceUsableHeight = referenceRectHeight - topMargin - bottomMargin;
                float referenceRowHeight = referenceUsableHeight / 9f;

                float rowNudge = Mathf.Max(0.5f, referenceRowHeight * 0.06f);

                // Build clothing panel height from 6 rows using paper-doll row spacing
                float compactHeight = topMargin + (referenceRowHeight * labels.Length) + bottomMargin;
                root.Size = new Vector2(rect.width, compactHeight);

                for (int i = 0; i < labels.Length; i++)
                {
                    float rowY = topMargin + i * referenceRowHeight;

                    if (labels[i] != null)
                    {
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
                            ? new Color(1f, 0.9f, 0.2f, 1f)
                            : Color.white;
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

        private class GearExpandOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel label;

            public GearExpandOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string text)
            {
                if (parent == null)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                label = new TextLabel();
                label.Text = text;
                label.TextColor = Color.white;
                label.Enabled = true;
                root.Components.Add(label);
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null || label == null)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                const float referenceRectHeightAt4K = 540f;
                const float referenceTopMarginAt4K = 14f;
                const float referenceBottomMarginAt4K = 5f;

                float scaleTo4K = rect.height / referenceRectHeightAt4K;
                float topMargin = referenceTopMarginAt4K * scaleTo4K;
                float bottomMargin = referenceBottomMarginAt4K * scaleTo4K;

                float usableHeight = rect.height - topMargin - bottomMargin;
                float rowHeight = usableHeight / 9f;
                float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);
                float compactHeight = topMargin + rowHeight + bottomMargin;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, compactHeight);

                label.Position = new Vector2(
                    padL,
                    topMargin - rowNudge
                );

                label.TextScale = textScale;
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                label = null;
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
        private void DestroyOwnedUi()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            DestroySelectorBox();
            DestroyPaperDollIndicator();
            DestroyPaperDollTargetList();
            DestroyClothingExpandLabel();
            DestroyClothingTargetList();
            DestroyGearExpandLabel();
        }

        private bool HasLiveButton(DaggerfallInventoryWindow menuWindow, FieldInfo fiButton)
        {
            if (menuWindow == null || fiButton == null)
                return false;

            return fiButton.GetValue(menuWindow) != null;
        }

        private void LogTradeButtonsIfChanged(DaggerfallInventoryWindow menuWindow, string reason = null)
        {
            if (!debugMODE || menuWindow == null || !IsTradeWindow(menuWindow))
                return;

            string modeName = GetTradeWindowModeName(menuWindow) ?? "-";

            bool hasWagon = HasLiveButton(menuWindow, fiWagonButton);
            bool hasInfo = true; // trade window always has an Info action path
            bool hasSelect = HasLiveButton(menuWindow, fiSelectButton);
            bool hasSteal = HasLiveButton(menuWindow, fiStealButton);
            bool hasModeAction = HasLiveButton(menuWindow, fiModeActionButton);
            bool hasClear = HasLiveButton(menuWindow, fiClearButton);

            Debug.Log(string.Format(
                "[ControllerAssistant][InventoryAssist] TradeButtons reason={0} mode={1} wagon={2} info={3} select={4} steal={5} modeAction={6} clear={7}",
                string.IsNullOrEmpty(reason) ? "state" : reason,
                modeName,
                hasWagon,
                hasInfo,
                hasSelect,
                hasSteal,
                hasModeAction,
                hasClear
            ));
        }
        private void LogSelectorState(DaggerfallInventoryWindow menuWindow, string reason)
        {
            if (!debugMODE || menuWindow == null)
                return;

            string modeName = IsTradeWindow(menuWindow) ? (GetTradeWindowModeName(menuWindow) ?? "-") : "-";

            string leftGridInfo = "null";
            if (leftItemGrid != null)
            {
                Rect leftCell = leftItemGrid.GetCellRect(0, 0);
                leftGridInfo = string.Format("origin=({0:F1},{1:F1}) cell00=({2:F1},{3:F1},{4:F1},{5:F1})",
                    leftItemGrid.OriginX, leftItemGrid.OriginY,
                    leftCell.x, leftCell.y, leftCell.width, leftCell.height);
            }

            Debug.Log(string.Format(
                "[ControllerAssistant][InventoryAssist] SelectorState reason={0} mode={1} region={2} col={3} row={4} buttonIndex={5} selectorExists={6} leftGrid={7}",
                reason,
                modeName,
                currentRegion,
                selectedColumn,
                selectedRow,
                buttonSelectedIndex,
                selectorBox != null,
                leftGridInfo
            ));
        }


    }

}