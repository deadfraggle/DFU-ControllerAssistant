using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
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
        private const bool debugMODE = false;

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
        private const int InventoryExitButtonIndex = 6;

        private readonly Vector2[] buttonAnchorsNative = new Vector2[]
        {
            new Vector2(225.5f, 13.4f),   // Wagon
            new Vector2(225.5f, 35.4f),   // Info
            new Vector2(225.5f, 57.4f),   // Equip
            new Vector2(225.5f, 79.4f),   // Remove
            new Vector2(225.5f, 102.4f),  // Use
            new Vector2(225.5f, 125.4f),  // Gold
            new Vector2(225.5f, 181.4f),  // Exit
        };

        private readonly Vector2[] tradeButtonAnchorsNative = new Vector2[]
        {
            new Vector2(225.5f, 13.4f),   // Wagon
            new Vector2(225.5f, 35.4f),   // Info
            new Vector2(225.5f, 57.4f),   // Select
            new Vector2(225.5f, 111.6f),  // Steal (Buy only)
            new Vector2(225.5f, 133.7f),  // ModeAction (Buy/Sell/Repair/Identify)
            new Vector2(225.5f, 155.7f),  // Clear
            new Vector2(225.5f, 181.4f),  // Exit
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

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool isAssisting =
                (cm.DPadLeftPressed || cm.DPadRightPressed || cm.DPadUpPressed || cm.DPadDownPressed ||
                    cm.RStickUpPressed || cm.RStickDownPressed || cm.RStickLeftPressed || cm.RStickRightPressed ||
                    cm.RStickUpHeldSlow || cm.RStickDownHeldSlow || cm.RStickLeftHeldSlow || cm.RStickRightHeldSlow ||
                    cm.Action1Released || cm.Action2Released || cm.LegendPressed);

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
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                }

                if (cm.DPadDownPressed)
                {
                    CycleActionMode(menuWindow);
                    return;
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

                menuWindow.CloseWindow();
                return;
            }
        }

        private void CycleActionMode(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            // Trade windows do not use the same action-mode cycling as regular inventory.
            // INFO is an action mode, but SELECT is a dedicated trade button.
            if (IsTradeWindow(menuWindow))
            {
                int currentMode = -1;

                if (fiSelectedActionMode != null)
                {
                    object currentValue = fiSelectedActionMode.GetValue(menuWindow);
                    if (currentValue != null)
                        currentMode = (int)currentValue;
                }

                bool currentlyInfo = (currentMode == 0);

                if (currentlyInfo)
                {
                    // Go back to SELECT by clicking the real trade Select button.
                    InvokeTradeButtonClick(menuWindow, fiSelectButton, miSelectButtonClick);
                    buttonSelectedIndex = 2;   // Select
                }
                else
                {
                    // Go to INFO through the normal action-mode path.
                    InvokeSelectActionMode(menuWindow, 0);
                    buttonSelectedIndex = 1;   // Info
                }

                if (currentRegion == REGION_BUTTONS && selectorBox != null)
                    RefreshSelectorToCurrentRegion(menuWindow);

                return;
            }

            // Regular inventory: INFO -> EQUIP -> REMOVE -> USE -> INFO
            if (miSelectActionMode == null || fiSelectedActionMode == null)
                return;

            object value = fiSelectedActionMode.GetValue(menuWindow);
            if (value == null)
                return;

            int current = (int)value;
            int nextValue;

            switch (current)
            {
                case 0: nextValue = 1; break; // Info -> Equip
                case 1: nextValue = 2; break; // Equip -> Remove
                case 2: nextValue = 3; break; // Remove -> Use
                default: nextValue = 0; break; // Use/anything else -> Info
            }

            InvokeSelectActionMode(menuWindow, nextValue);
            buttonSelectedIndex = nextValue + 1;   // Info..Use = button indices 1..4

            if (currentRegion == REGION_BUTTONS && selectorBox != null)
                RefreshSelectorToCurrentRegion(menuWindow);
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
                new LegendOverlay.LegendRow(cm.Action1Name, "Left Click"),
                new LegendOverlay.LegendRow(cm.Action2Name, "Right Click"),
                new LegendOverlay.LegendRow("D-Pad Up", "Middle Click"),
                new LegendOverlay.LegendRow("D-Pad Left/Right", "Cycle Tabs"),
                new LegendOverlay.LegendRow("D-Pad Down", "Cycle Buttons"),
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
                case 6: // Exit
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