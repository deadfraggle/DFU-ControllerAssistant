using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SaveGameAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool reflectionCached = false;
        private bool wasOpen = false;
        private bool closeDeferred = false;
        private KeyCode suppressedBackButton = KeyCode.None;
        private bool backBindingSuppressed = false;
        private bool waitingToRestoreBackBinding = false;

        // Shared reflection/UI
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Reflection: save window controls/state
        private FieldInfo fiSavesList;
        private FieldInfo fiSaveNameTextBox;
        private FieldInfo fiRenameSaveButton;
        private FieldInfo fiDeleteSaveButton;
        private FieldInfo fiGoButton;
        private FieldInfo fiSwitchCharButton;
        private FieldInfo fiSavesScroller;
        private FieldInfo fiMode;

        private MethodInfo miSaveLoadEventHandler;
        private MethodInfo miCancelButton_OnMouseClick;
        private MethodInfo miRenameSaveButton_OnMouseClick;
        private MethodInfo miDeleteSaveButton_OnMouseClick;
        private MethodInfo miSwitchCharButton_OnMouseClick;

        private MethodInfo miInputManagerUpdateBindingCache;

        private OnScreenKeyboardOverlay keyboardOverlay;

        // Selector
        private DefaultSelectorBoxHost selectorHost;
        private bool selectorInitializedThisOpen = false;
        private int selectorInitStableTicks = 0;
        private float selectorInitLastWidth = -1;
        private float selectorInitLastHeight = -1;

        // Regions
        private const int RegionButtons = 0;
        private const int RegionFilesPanel = 1;
        private const int RegionNaming = 2;
        private int currentRegion = RegionFilesPanel;

        // Save variant buttons
        protected const int NamingBox = 0;
        protected const int FilesPanel = 1;
        protected const int RenameButton = 2;
        protected const int DeleteButton = 3;
        protected const int SaveButton = 4;
        protected const int CancelButton = 5;

        // Load variant buttons
        protected const int lwFilesPanel = 6;
        protected const int lwRenameButton = 7;
        protected const int lwDeleteButton = 8;
        protected const int lwLoadButton = 9;
        protected const int lwCancelButton = 10;
        protected const int lwSwitchCharButton = 11;

        protected int buttonSelected = FilesPanel;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallUnitySaveGameWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallUnitySaveGameWindow menuWindow = top as DaggerfallUnitySaveGameWindow;

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

        private void OnTickOpen(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);
            RefreshKeyboardAttachment(menuWindow);

            if (currentRegion == RegionNaming && keyboardOverlay != null)
                SuppressBackBindingForKeyboard();
            else
                RestoreBackBindingIfReady();

            if (!selectorInitializedThisOpen)
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                {
                    float w = currentPanel.Rectangle.width;
                    float h = currentPanel.Rectangle.height;

                    if (w > 0 && h > 0)
                    {
                        if (w == selectorInitLastWidth && h == selectorInitLastHeight)
                            selectorInitStableTicks++;
                        else
                            selectorInitStableTicks = 1;

                        selectorInitLastWidth = w;
                        selectorInitLastHeight = h;

                        if (selectorInitStableTicks >= 2)
                        {
                            RefreshSelectorForCurrentRegion(menuWindow);
                            selectorInitializedThisOpen = true;
                        }
                    }
                }
            }

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (currentRegion == RegionButtons)
                TickButtonsRegion(menuWindow, cm);
            else if (currentRegion == RegionFilesPanel)
                TickFilesPanelRegion(menuWindow, cm);
            else
                TickNamingRegion(menuWindow, cm);

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }


        private void OnOpened(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;
            closeDeferred = false;

            ForceFirstSaveSelectionIfNeeded(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ForceRestoreBackBinding();
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallUnitySaveGameWindow closed");
        }

        public void ResetState()
        {
            ForceRestoreBackBinding();

            wasOpen = false;
            closeDeferred = false;
            selectorInitializedThisOpen = false;

            DestroyLegend();
            DestroySelectorBox();
            DestroyKeyboard();

            panelRenderWindow = null;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;

            currentRegion = RegionFilesPanel;
            buttonSelected = FilesPanel;
        }

        private void EnsureInitialized(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            System.Type type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            fiSavesList = CacheField(type, "savesList");
            fiSaveNameTextBox = CacheField(type, "saveNameTextBox");
            fiRenameSaveButton = CacheField(type, "renameSaveButton");
            fiDeleteSaveButton = CacheField(type, "deleteSaveButton");
            fiGoButton = CacheField(type, "goButton");
            fiSwitchCharButton = CacheField(type, "switchCharButton");
            fiSavesScroller = CacheField(type, "savesScroller");
            fiMode = CacheField(type, "mode");

            miSaveLoadEventHandler = CacheMethod(type, "SaveLoadEventHandler");
            miCancelButton_OnMouseClick = CacheMethod(type, "CancelButton_OnMouseClick");
            miRenameSaveButton_OnMouseClick = CacheMethod(type, "RenameSaveButton_OnMouseClick");
            miDeleteSaveButton_OnMouseClick = CacheMethod(type, "DeleteSaveButton_OnMouseClick");
            miSwitchCharButton_OnMouseClick = CacheMethod(type, "SwitchCharButton_OnMouseClick");

            System.Type inputManagerType = typeof(InputManager);
            miInputManagerUpdateBindingCache = inputManagerType.GetMethod("UpdateBindingCache", BF);

            reflectionCached = true;
        }
        private LegendOverlay CreateLegendBase(Panel panel)
        {
            LegendOverlay newLegend = new LegendOverlay(panel);
            newLegend.HeaderScale = 6.0f;
            newLegend.RowScale = 5.0f;
            newLegend.PadL = 18f;
            newLegend.PadT = 16f;
            newLegend.LineGap = 36f;
            newLegend.ColGap = 22f;
            newLegend.MarginX = 8f;
            newLegend.MarginFromBottom = 24f;
            newLegend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
            return newLegend;
        }

        private void RebuildLegend(DaggerfallUnitySaveGameWindow menuWindow, List<LegendOverlay.LegendRow> rows)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            DestroyLegend();

            legend = CreateLegendBase(panelRenderWindow);
            legend.Build("Legend", rows);
            legendVisible = true;
            legend.SetEnabled(true);
        }

        private void EnsureFilesLegendUI(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
            {
                new LegendOverlay.LegendRow("Version", "10"),
                new LegendOverlay.LegendRow("D-Pad Left", "Rename Selected File"),
                new LegendOverlay.LegendRow("Right Stick", "Move Selector / Files"),
                new LegendOverlay.LegendRow(cm.Action1Name, "Save / Load File"),
                new LegendOverlay.LegendRow(cm.Action2Name, "Delete Selected File"),
            };

            RebuildLegend(menuWindow, rows);
        }

        private void EnsureKeyboardLegendUI(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
            {
                new LegendOverlay.LegendRow("Version", "10"),
                new LegendOverlay.LegendRow("Right Stick", "Move Keyboard"),
                new LegendOverlay.LegendRow("D-Pad Up", "Shift"),
                new LegendOverlay.LegendRow("D-Pad Down", "Toggle 123"),
                new LegendOverlay.LegendRow("D-Pad Left", "Backspace"),
                new LegendOverlay.LegendRow("D-Pad Right", "Save File"),
                new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                new LegendOverlay.LegendRow("Back", "Close Keyboard"),
            };

            RebuildLegend(menuWindow, rows);
        }

        private void RefreshLegendAttachment(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroyLegend();
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

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
        }

        protected Panel GetCurrentRenderPanel(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        protected void RefreshSelectorForCurrentRegion(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (currentRegion == RegionButtons)
                RefreshSelectorToCurrentButton(menuWindow);
            else if (currentRegion == RegionFilesPanel)
                RefreshSelectorToFilesPanel(menuWindow);
            else
                RefreshSelectorToNaming(menuWindow);
        }
        private void SuppressBackBindingForKeyboard()
        {
            if (backBindingSuppressed || InputManager.Instance == null)
                return;

            suppressedBackButton = InputManager.Instance.GetJoystickUIBinding(InputManager.JoystickUIActions.Back);

            if (suppressedBackButton != KeyCode.None)
            {
                InputManager.Instance.SetJoystickUIBinding(KeyCode.None, InputManager.JoystickUIActions.Back);
                RefreshInputManagerBindingCache();

                backBindingSuppressed = true;
                waitingToRestoreBackBinding = false;
            }
        }

        private void BeginRestoreBackBinding()
        {
            if (!backBindingSuppressed)
                return;

            waitingToRestoreBackBinding = true;
        }

        private void RestoreBackBindingIfReady()
        {
            if (!backBindingSuppressed || !waitingToRestoreBackBinding || suppressedBackButton == KeyCode.None)
                return;

            if (Input.GetKey(suppressedBackButton))
                return;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetJoystickUIBinding(suppressedBackButton, InputManager.JoystickUIActions.Back);
                RefreshInputManagerBindingCache();
            }

            backBindingSuppressed = false;
            waitingToRestoreBackBinding = false;
            suppressedBackButton = KeyCode.None;
        }

        private void ForceRestoreBackBinding()
        {
            if (!backBindingSuppressed)
                return;

            if (InputManager.Instance != null && suppressedBackButton != KeyCode.None)
            {
                InputManager.Instance.SetJoystickUIBinding(suppressedBackButton, InputManager.JoystickUIActions.Back);
                RefreshInputManagerBindingCache();
            }

            backBindingSuppressed = false;
            waitingToRestoreBackBinding = false;
            suppressedBackButton = KeyCode.None;
        }
        private void RefreshInputManagerBindingCache()
        {
            if (InputManager.Instance == null || miInputManagerUpdateBindingCache == null)
                return;

            miInputManagerUpdateBindingCache.Invoke(InputManager.Instance, null);
        }

        protected MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        protected FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            System.Type type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (MethodInfo m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (FieldInfo f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}