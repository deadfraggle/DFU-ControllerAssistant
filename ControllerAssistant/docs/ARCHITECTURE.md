# Controller Assistant (DFU)

Adds controller navigation and assist features to Daggerfall Unity menus.

### ControllerAssistant
Routes controller input to context-specific “assist modules” based on the current DFU TopWindow.

Responsibilities:
- loads user bindings from settings
- checks user bindings for potential conflicts and issues warnings to HUD if found
- Detect top UIWindow has changed from DaggerfallHUD
- Detects controller inputs when menus are open
- Dispatch assist modules

Methods:
- EarlyInit(InitParams initParams)
- GameInit(InitParams initParams)
- Update()
- LoadSettings(ModSettings modSettings, ModSettingsChange change)
- GetAction1KeyCode()
- GetAction2KeyCode()
- GetLegendKeyCode()
- MapChoiceIndexToKeyCode(int idx)

Internal Classes:
- ControllerAssistConflictDetector


## Core System

### ControllerManager
Central polling loop.

Responsibilities:
- process inputs
- output signals based on input
- assign button names to user bindings for legend overlay

Methods:
- Update()
- ControllerManager()
- SetAction1Key(KeyCode key)
- SetAction2Key(KeyCode key)
- SetLegendKey(KeyCode key)
- Action1Name
- Action2Name
- GetButtonName(KeyCode key)

### IMenuAssist
Interface implemented by all assist modules.

Methods:
- Claims(UIWindow top)
- Tick(UIWindow top)
- ResetState()

### MenuAssistModule
Generic base class for menu-specific assist modules

Responsibilities:
- Determine whether the currently active UI window matches the assist's target type.
- Detect window open/close transitions using a simple edge-detection flag (wasOpen).
- Provide lifecycle hooks for assist modules
- Ensure modules only run when their window type is active.

Methods:
- Tick(IUserInterfaceWindow top, ControllerManager cm)
- ResetState()


## Assist Modules

### DefaultMenuAssist
Attempts to add navigation to non-supported menus (assuming they are a Listbox type window) 

Features:
- keyboard arrow navigation via D-Pad
- invoking submit by sending Keycode.Return

Methods:
- outdated, requires revision
- Tick(IUserInterfaceWindow top, ControllerManager cm)
- ResetState()
- TapKey(KeyCode key)
- UpdateHeldArrowHorizontal(int newDir)
- UpdateHeldArrowVertical(int newDir)
- ReleaseIfHeldHorizontal(int dir)
- ReleaseIfHeldVertical(int dir)

### SpellbookAssist
adds navigation and assist actions to the Spellbook window 

Features:
- keyboard arrow navigation via D-Pad
- invoking submit by sending Keycode.Return

Methods:
- outdated, requires revision

### ExtAutomapAssist
Controls exterior automap navigation.

Features:
- panning
- rotation
- zoom
- center map on player
- reset view
- legend overlay

Methods:
- OnTickOpen(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
- PanMapLeft(DaggerfallExteriorAutomapWindow menuWindow)
- PanMapRight(DaggerfallExteriorAutomapWindow menuWindow)
- PanMapDown(DaggerfallExteriorAutomapWindow menuWindow)
- PanMapUp(DaggerfallExteriorAutomapWindow menuWindow)
- ZoomMapOut(DaggerfallExteriorAutomapWindow menuWindow)
- ZoomMapIn(DaggerfallExteriorAutomapWindow menuWindow)
- RotateMapClockwise(DaggerfallExteriorAutomapWindow menuWindow)
- RotateMapCounterClockwise(DaggerfallExteriorAutomapWindow menuWindow)
- ResetMapView(DaggerfallExteriorAutomapWindow menuWindow)
- CenterMapOnPlayer(DaggerfallExteriorAutomapWindow menuWindow)
- OnOpened(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
- OnClosed(ControllerManager cm)
- ResetState()
- EnsureInitialized(DaggerfallExteriorAutomapWindow menuWindow)
- EnsureLegendUI(DaggerfallExteriorAutomapWindow menuWindow, ControllerManager cm)
- RefreshLegendAttachment(DaggerfallExteriorAutomapWindow menuWindow)
- CacheMethod(System.Type type, string name)
- CacheField(System.Type type, string name)
- DumpWindowMembers(object window)

### InventoryAssist
Provides controller navigation for inventory.

Features:
- tab switching
- button selection
- legend overlay

Methods:
- OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
- ToggleWagon(DaggerfallInventoryWindow menuWindow)
- CycleTab(DaggerfallInventoryWindow menuWindow, int direction)
- CycleActionMode(DaggerfallInventoryWindow menuWindow, int direction)
- OpenGoldPopup(DaggerfallInventoryWindow menuWindow)
- OnOpened(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
- OnClosed(ControllerManager cm)
- ResetState()
- EnsureInitialized(DaggerfallInventoryWindow menuWindow)
- EnsureLegendUI(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
- RefreshLegendAttachment(DaggerfallInventoryWindow menuWindow)
- CacheMethod(System.Type type, string name)
- CacheField(System.Type type, string name)
- DumpWindowMembers(object window)

### InputMessageBoxAssist
Provides controller assists for specific message boxes including
- inventory gold to drop popup

Features for gold popup:
- D-Pad controlled amount increases and decreases
- incremental increases and decreases
- reset to 0
- maximum increase limit
- quick maximum amount insertion
- backspace with right stick left motion
- legend overlay with updated increment amount

Methods:
- OnTickOpen(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- TickInventoryGold(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- TickWait(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- BeginGoldHold(DaggerfallInputMessageBox menuWindow, int direction)
- UpdateGoldHold(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- EndGoldHold()
- StepGoldAmount(DaggerfallInputMessageBox menuWindow, int direction)
- SetGoldAmount(DaggerfallInputMessageBox menuWindow, int value)
- GetPlayerGold()
- SubmitInputBox(DaggerfallInputMessageBox menuWindow)
- IncreaseIncrement(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- DecreaseIncrement(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- BackspaceGoldAmount(DaggerfallInputMessageBox menuWindow)
- RefreshGoldLegend(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- OnOpened(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- OnClosed(ControllerManager cm)
- ResetState()
- EnsureInitialized(DaggerfallInputMessageBox menuWindow)
- EnsureLegendUI(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
- BuildInventoryGoldLegend(ControllerManager cm)
- RefreshLegendAttachment(DaggerfallInputMessageBox menuWindow)
- IsInventoryGoldPopup(DaggerfallInputMessageBox menuWindow)
- CacheMethod(System.Type type, string name)
- CacheField(System.Type type, string name)
- DumpWindowMembers(object window)


## UI Systems

### LegendOverlay
Displays contextual controller assists on HUD.

Features:
- Toggle on/off
- dynamic sizing based on assist info
- dynamic scaling and placement based on screen resolution

Methods:
- LegendOverlay(Panel parentPanel)
- SetParent(Panel parentPanel)
- LegendRow
- Build(string header, List<LegendRow> rows)
- SetEnabled(bool enabled)
- PositionBottomLeft()
- PositionAt(float x, float y)
- PositionTopRight()
- PositionNormalized(float xNorm, float yNorm, LegendAnchor anchor)
- IsAttached()
- AddHeader(string text)
- AddRow(string lText, string rText)
- Layout()
- ApplyScale(float scale)
- Destroy()