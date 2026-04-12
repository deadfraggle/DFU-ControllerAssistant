using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;


namespace gigantibyte.DFU.ControllerAssistant
{
    public class CharacterSheetAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private DefaultSelectorBoxHost selector;
        private bool selectorVisible = false;

        const int NameButton = 0;
        const int LevelButton = 1;
        const int GoldButton = 2;
        const int HealthButton = 3;
        const int AffiliationsButton = 4;

        const int PrimaryButton = 5;
        const int MajorButton = 6;
        const int MinorButton = 7;
        const int MiscellaneousButton = 8;

        const int InventoryButton = 9;
        const int SpellbookButton = 10;
        const int LogButton = 11;
        const int HistoryButton = 12;
        const int ExitButton = 13;

        const int StrengthButton = 14;
        const int IntelligenceButton = 15;
        const int WillpowerButton = 16;
        const int AgilityButton = 17;
        const int EnduranceButton = 18;
        const int PersonalityButton = 19;
        const int SpeedButton = 20;
        const int LuckButton = 21;

        private int buttonSelected = PrimaryButton;

        private MethodInfo miNameButton_OnMouseClick;
        private MethodInfo miLevelButton_OnMouseClick;
        private MethodInfo miGoldButton_OnMouseClick;
        private MethodInfo miHealthButton_OnMouseClick;
        private MethodInfo miAffiliationsButton_OnMouseClick;
        private MethodInfo miPrimarySkillsButton_OnMouseClick;
        private MethodInfo miMajorSkillsButton_OnMouseClick;
        private MethodInfo miMinorSkillsButton_OnMouseClick;
        private MethodInfo miMiscSkillsButton_OnMouseClick;
        private MethodInfo miInventoryButton_OnMouseClick;
        private MethodInfo miSpellBookButton_OnMouseClick;
        private MethodInfo miLogBookButton_OnMouseClick;
        private MethodInfo miHistoryButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;
        private MethodInfo miStatButton_OnMouseClick;
        private MethodInfo miRefresh;

        private FieldInfo fiStatsRollout;
        private FieldInfo fiLeveling;
        private MethodInfo miUpdateSecondaryStatLabels;

        private PropertyInfo piStatsRolloutStartingStats;
        private PropertyInfo piStatsRolloutWorkingStats;
        private PropertyInfo piStatsRolloutBonusPool;
        private MethodInfo miStatsRolloutSelectStat;
        private MethodInfo miStatsRolloutSpinnerUp;
        private MethodInfo miStatsRolloutSpinnerDown;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        private AnchorEditor editor;

        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallCharacterSheetWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallCharacterSheetWindow menuWindow = top as DaggerfallCharacterSheetWindow;

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

        public void ResetState()
        {
            wasOpen = false;
            closeDeferred = false;

            DestroySelector();
            DestroyLegend();

            selectorVisible = false;
            legendVisible = false;
            panelRenderWindow = null;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.CharacterSheet);

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);
            EnsureSelectorUI(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            // Anchor Editor
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (panelRenderWindow != null)
                editor.Tick(panelRenderWindow);

            UpdateSelectorVisual();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool moved = false;
            bool isAssisting =
                cm.RStickUpPressed || cm.RStickUpHeldSlow ||
                cm.RStickDownPressed || cm.RStickDownHeldSlow ||
                cm.RStickLeftPressed || cm.RStickLeftHeldSlow ||
                cm.RStickRightPressed || cm.RStickRightHeldSlow ||
                cm.DPadUpPressed || cm.DPadUpHeldSlow ||
                cm.DPadDownPressed || cm.DPadDownHeldSlow ||
                cm.Action1Released ||
                cm.Action2Pressed ||
                cm.LegendPressed;

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                MoveSelection(0, -1);
                moved = true;
            }
            else if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                MoveSelection(0, 1);
                moved = true;
            }
            else if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                MoveSelection(-1, 0);
                moved = true;
            }
            else if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                MoveSelection(1, 0);
                moved = true;
            }

            if (moved && IsLevelingActive(menuWindow) && IsAttributeButton(buttonSelected))
            {
                SyncLevelUpSliderToSelectedAttribute(menuWindow);
            }

            bool levelStatChanged = false;

            if (IsLevelingActive(menuWindow) && IsAttributeButton(buttonSelected))
            {
                if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
                    levelStatChanged = TryAdjustLevelUpStat(menuWindow, +1);
                else if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
                    levelStatChanged = TryAdjustLevelUpStat(menuWindow, -1);
            }

            if (moved || levelStatChanged)
                UpdateSelectorVisual();

            if (cm.Action1Released)
            {
                Action1(menuWindow);
            }

            if (cm.Action2Pressed)
            {
                //Action2(menuWindow);
                //editor.Toggle();
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;
                DestroySelector();
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }
        }

        // =========================
        // Assist action helpers
        // =========================

        private void Action2(DaggerfallCharacterSheetWindow menuWindow)
        {
            var player = GameManager.Instance.PlayerEntity;
            if (player == null)
                return;

            // Force the next character-sheet refresh into the level-up variant.
            // This is cleaner than trying to fake exact skill tallies.
            player.ReadyToLevelUp = true;
            player.OghmaLevelUp = false;

            // If we have the private Refresh() cached, invoke it so the window
            // flips immediately without needing to close/reopen.
            if (miRefresh != null)
            {
                miRefresh.Invoke(menuWindow, null);
                RefreshSelectorAttachment(menuWindow);
                EnsureSelectorUI(menuWindow);
                UpdateSelectorVisual();
                DaggerfallUI.AddHUDText("Level-up state forced.");
            }
            else
            {
                DaggerfallUI.AddHUDText("Level-up queued. Reopen character sheet if needed.");
            }
        }

        // =========================
        // Selector helpers
        // =========================

        private void EnsureSelectorUI(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (selector == null)
                selector = new DefaultSelectorBoxHost();

            selectorVisible = true;
        }

        private void RefreshSelectorAttachment(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroySelector();
                DestroyLegend();
                panelRenderWindow = current;
                selectorVisible = false;
                legendVisible = false;
                return;
            }

            if (selector != null)
                selector.RefreshAttachment(current);
        }

        private void DestroySelector()
        {
            if (selector != null)
            {
                selector.Destroy();
                selector = null;
            }
        }

        private void UpdateSelectorVisual()
        {
            if (!selectorVisible || selector == null || panelRenderWindow == null)
                return;

            selector.ShowAtNativeRect(
                panelRenderWindow,
                GetButtonRect(buttonSelected),
                Color.cyan);
        }

        private Rect GetButtonRect(int button)
        {
            switch (button)
            {
                case NameButton: return new Rect(4.0f, 2.7f, 129.3f, 8.2f);
                case LevelButton: return new Rect(4.0f, 32.7f, 129.3f, 8.2f);
                case GoldButton: return new Rect(4.0f, 43.0f, 129.3f, 8.2f);
                case HealthButton: return new Rect(4.0f, 62.7f, 129.3f, 8.2f);
                case AffiliationsButton: return new Rect(4.0f, 83.8f, 129.3f, 8.2f);

                case PrimaryButton: return new Rect(11.2f, 106.0f, 115.8f, 8.1f);
                case MajorButton: return new Rect(11.2f, 115.8f, 115.8f, 8.1f);
                case MinorButton: return new Rect(11.2f, 126.0f, 115.8f, 8.1f);
                case MiscellaneousButton: return new Rect(11.2f, 135.9f, 115.8f, 8.1f);

                case InventoryButton: return new Rect(2.9f, 151.9f, 64.4f, 10.1f);
                case SpellbookButton: return new Rect(70.0f, 151.9f, 64.4f, 10.1f);
                case LogButton: return new Rect(2.9f, 165.9f, 64.4f, 10.1f);
                case HistoryButton: return new Rect(70.0f, 165.9f, 64.4f, 10.1f);
                case ExitButton: return new Rect(50.6f, 179.0f, 39.2f, 18.9f);

                case StrengthButton: return new Rect(141.3f, 5.1f, 30.1f, 21.9f);
                case IntelligenceButton: return new Rect(141.3f, 29.1f, 30.1f, 21.9f);
                case WillpowerButton: return new Rect(141.3f, 52.9f, 30.1f, 21.9f);
                case AgilityButton: return new Rect(141.3f, 77.0f, 30.1f, 21.9f);
                case EnduranceButton: return new Rect(141.3f, 101.0f, 30.1f, 21.9f);
                case PersonalityButton: return new Rect(141.3f, 125.0f, 30.1f, 21.9f);
                case SpeedButton: return new Rect(141.3f, 149.0f, 30.1f, 21.9f);
                case LuckButton: return new Rect(141.3f, 173.0f, 30.1f, 21.9f);

                default: return new Rect(11, 106, 115, 8);
            }
        }

        private void MoveSelection(int dx, int dy)
        {
            if (dy < 0)
                buttonSelected = MoveUp(buttonSelected);
            else if (dy > 0)
                buttonSelected = MoveDown(buttonSelected);
            else if (dx < 0)
                buttonSelected = MoveLeft(buttonSelected);
            else if (dx > 0)
                buttonSelected = MoveRight(buttonSelected);
        }

        private int MoveUp(int current)
        {
            switch (current)
            {
                case NameButton: return NameButton;
                case LevelButton: return NameButton;
                case GoldButton: return LevelButton;
                case HealthButton: return GoldButton;
                case AffiliationsButton: return HealthButton;

                case PrimaryButton: return AffiliationsButton;
                case MajorButton: return PrimaryButton;
                case MinorButton: return MajorButton;
                case MiscellaneousButton: return MinorButton;

                case InventoryButton: return MiscellaneousButton;
                case SpellbookButton: return MiscellaneousButton;
                case LogButton: return InventoryButton;
                case HistoryButton: return SpellbookButton;
                case ExitButton: return LogButton;

                case StrengthButton: return StrengthButton;
                case IntelligenceButton: return StrengthButton;
                case WillpowerButton: return IntelligenceButton;
                case AgilityButton: return WillpowerButton;
                case EnduranceButton: return AgilityButton;
                case PersonalityButton: return EnduranceButton;
                case SpeedButton: return PersonalityButton;
                case LuckButton: return SpeedButton;
            }

            return current;
        }

        private int MoveDown(int current)
        {
            switch (current)
            {
                case NameButton: return LevelButton;
                case LevelButton: return GoldButton;
                case GoldButton: return HealthButton;
                case HealthButton: return AffiliationsButton;
                case AffiliationsButton: return PrimaryButton;

                case PrimaryButton: return MajorButton;
                case MajorButton: return MinorButton;
                case MinorButton: return MiscellaneousButton;
                case MiscellaneousButton: return InventoryButton;

                case InventoryButton: return LogButton;
                case SpellbookButton: return HistoryButton;
                case LogButton: return ExitButton;
                case HistoryButton: return ExitButton;
                case ExitButton: return ExitButton;

                case StrengthButton: return IntelligenceButton;
                case IntelligenceButton: return WillpowerButton;
                case WillpowerButton: return AgilityButton;
                case AgilityButton: return EnduranceButton;
                case EnduranceButton: return PersonalityButton;
                case PersonalityButton: return SpeedButton;
                case SpeedButton: return LuckButton;
                case LuckButton: return LuckButton;
            }

            return current;
        }

        private int MoveLeft(int current)
        {
            switch (current)
            {
                case SpellbookButton: return InventoryButton;
                case HistoryButton: return LogButton;

                case StrengthButton: return NameButton;
                case IntelligenceButton: return LevelButton;
                case WillpowerButton: return GoldButton;
                case AgilityButton: return HealthButton;
                case EnduranceButton: return AffiliationsButton;
                case PersonalityButton: return MinorButton;
                case SpeedButton: return SpellbookButton;
                case LuckButton: return ExitButton;
            }

            return current;
        }

        private int MoveRight(int current)
        {
            switch (current)
            {
                case NameButton: return StrengthButton;
                case LevelButton: return IntelligenceButton;
                case GoldButton: return WillpowerButton;
                case HealthButton: return AgilityButton;
                case AffiliationsButton: return EnduranceButton;

                case PrimaryButton: return EnduranceButton;
                case MajorButton: return PersonalityButton;
                case MinorButton: return PersonalityButton;
                case MiscellaneousButton: return SpeedButton;

                case InventoryButton: return SpellbookButton;
                case SpellbookButton: return SpeedButton;
                case LogButton: return HistoryButton;
                case HistoryButton: return LuckButton;
                case ExitButton: return LuckButton;

                default: return current;
            }
        }

        private void Action1(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (IsLevelingActive(menuWindow) && IsAttributeButton(buttonSelected))
            {
                SyncLevelUpSliderToSelectedAttribute(menuWindow);
                return;
            }

            switch (buttonSelected)
            {
                case NameButton:
                    InvokeButtonHandler(miNameButton_OnMouseClick, menuWindow);
                    break;
                case LevelButton:
                    InvokeButtonHandler(miLevelButton_OnMouseClick, menuWindow);
                    break;
                case GoldButton:
                    InvokeButtonHandler(miGoldButton_OnMouseClick, menuWindow);
                    break;
                case HealthButton:
                    InvokeButtonHandler(miHealthButton_OnMouseClick, menuWindow);
                    break;
                case AffiliationsButton:
                    InvokeButtonHandler(miAffiliationsButton_OnMouseClick, menuWindow);
                    break;

                case PrimaryButton:
                    InvokeButtonHandler(miPrimarySkillsButton_OnMouseClick, menuWindow);
                    break;
                case MajorButton:
                    InvokeButtonHandler(miMajorSkillsButton_OnMouseClick, menuWindow);
                    break;
                case MinorButton:
                    InvokeButtonHandler(miMinorSkillsButton_OnMouseClick, menuWindow);
                    break;
                case MiscellaneousButton:
                    InvokeButtonHandler(miMiscSkillsButton_OnMouseClick, menuWindow);
                    break;

                case InventoryButton:
                    InvokeButtonHandler(miInventoryButton_OnMouseClick, menuWindow);
                    break;
                case SpellbookButton:
                    InvokeButtonHandler(miSpellBookButton_OnMouseClick, menuWindow);
                    break;
                case LogButton:
                    InvokeButtonHandler(miLogBookButton_OnMouseClick, menuWindow);
                    break;
                case HistoryButton:
                    InvokeButtonHandler(miHistoryButton_OnMouseClick, menuWindow);
                    break;
                case ExitButton:
                    InvokeButtonHandler(miExitButton_OnMouseClick, menuWindow);
                    break;

                case StrengthButton:
                    InvokeStatHandler(menuWindow, 0);
                    break;
                case IntelligenceButton:
                    InvokeStatHandler(menuWindow, 1);
                    break;
                case WillpowerButton:
                    InvokeStatHandler(menuWindow, 2);
                    break;
                case AgilityButton:
                    InvokeStatHandler(menuWindow, 3);
                    break;
                case EnduranceButton:
                    InvokeStatHandler(menuWindow, 4);
                    break;
                case PersonalityButton:
                    InvokeStatHandler(menuWindow, 5);
                    break;
                case SpeedButton:
                    InvokeStatHandler(menuWindow, 6);
                    break;
                case LuckButton:
                    InvokeStatHandler(menuWindow, 7);
                    break;
            }
        }

        private void InvokeButtonHandler(MethodInfo method, DaggerfallCharacterSheetWindow menuWindow)
        {
            if (method == null || menuWindow == null)
                return;

            method.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void InvokeStatHandler(DaggerfallCharacterSheetWindow menuWindow, int statIndex)
        {
            if (miStatButton_OnMouseClick == null || menuWindow == null)
                return;

            Button fakeSender = new Button();
            fakeSender.Tag = DaggerfallUnity.Instance.TextProvider.GetStatDescriptionTextID((DFCareer.Stats)statIndex);

            miStatButton_OnMouseClick.Invoke(menuWindow, new object[] { fakeSender, Vector2.zero });
        }

        private bool IsLevelingActive(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (menuWindow == null || fiLeveling == null)
                return false;

            object value = fiLeveling.GetValue(menuWindow);
            return value is bool && (bool)value;
        }

        private bool IsAttributeButton(int button)
        {
            return button >= StrengthButton && button <= LuckButton;
        }

        private int GetSelectedStatIndex()
        {
            if (!IsAttributeButton(buttonSelected))
                return -1;

            return buttonSelected - StrengthButton;
        }

        private object GetStatsRollout(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (menuWindow == null || fiStatsRollout == null)
                return null;

            return fiStatsRollout.GetValue(menuWindow);
        }
        private void SyncLevelUpSliderToSelectedAttribute(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (!IsLevelingActive(menuWindow))
                return;

            int statIndex = GetSelectedStatIndex();
            if (statIndex < 0)
                return;

            object statsRollout = GetStatsRollout(menuWindow);
            if (statsRollout == null || miStatsRolloutSelectStat == null)
                return;

            try
            {
                miStatsRolloutSelectStat.Invoke(statsRollout, new object[] { statIndex });
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] SyncLevelUpSliderToSelectedAttribute failed: " + ex);
            }
        }


        private bool TryAdjustLevelUpStat(DaggerfallCharacterSheetWindow menuWindow, int delta)
        {
            if (delta == 0)
                return false;

            if (!IsLevelingActive(menuWindow))
                return false;

            int statIndex = GetSelectedStatIndex();
            if (statIndex < 0)
                return false;

            object statsRollout = GetStatsRollout(menuWindow);
            if (statsRollout == null)
                return false;

            try
            {
                // Keep rollout stat selection synced to the selector.
                if (miStatsRolloutSelectStat != null)
                    miStatsRolloutSelectStat.Invoke(statsRollout, new object[] { statIndex });

                if (delta > 0)
                {
                    if (miStatsRolloutSpinnerUp != null)
                    {
                        miStatsRolloutSpinnerUp.Invoke(statsRollout, null);
                        return true;
                    }
                }
                else
                {
                    if (miStatsRolloutSpinnerDown != null)
                    {
                        miStatsRolloutSpinnerDown.Invoke(statsRollout, null);
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log("[ControllerAssistant] TryAdjustLevelUpStat failed: " + ex);
            }

            return false;
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            if (buttonSelected < NameButton || buttonSelected > LuckButton)
                buttonSelected = PrimaryButton;

            if (fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            EnsureSelectorUI(menuWindow);
            UpdateSelectorVisual();

            // Anchor Editor
            if (editor == null)
            {
                // Match Inventory's default selector size: 25 x 19 native-ish feel
                editor = new AnchorEditor(25f, 19f);
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallCharacterSheetWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            miNameButton_OnMouseClick = CacheMethod(type, "NameButton_OnMouseClick");
            miLevelButton_OnMouseClick = CacheMethod(type, "LevelButton_OnMouseClick");
            miGoldButton_OnMouseClick = CacheMethod(type, "GoldButton_OnMouseClick");
            miHealthButton_OnMouseClick = CacheMethod(type, "HealthButton_OnMouseClick");
            miAffiliationsButton_OnMouseClick = CacheMethod(type, "AffiliationsButton_OnMouseClick");
            miPrimarySkillsButton_OnMouseClick = CacheMethod(type, "PrimarySkillsButton_OnMouseClick");
            miMajorSkillsButton_OnMouseClick = CacheMethod(type, "MajorSkillsButton_OnMouseClick");
            miMinorSkillsButton_OnMouseClick = CacheMethod(type, "MinorSkillsButton_OnMouseClick");
            miMiscSkillsButton_OnMouseClick = CacheMethod(type, "MiscSkillsButton_OnMouseClick");
            miInventoryButton_OnMouseClick = CacheMethod(type, "InventoryButton_OnMouseClick");
            miSpellBookButton_OnMouseClick = CacheMethod(type, "SpellBookButton_OnMouseClick");
            miLogBookButton_OnMouseClick = CacheMethod(type, "LogBookButton_OnMouseClick");
            miHistoryButton_OnMouseClick = CacheMethod(type, "HistoryButton_OnMouseClick");
            miExitButton_OnMouseClick = CacheMethod(type, "ExitButton_OnMouseClick");
            miStatButton_OnMouseClick = CacheMethod(type, "StatButton_OnMouseClick");
            fiStatsRollout = CacheField(type, "statsRollout");
            fiLeveling = CacheField(type, "leveling");
            miUpdateSecondaryStatLabels = CacheMethod(type, "UpdateSecondaryStatLabels");

            if (fiStatsRollout != null)
            {
                System.Type statsRolloutType = fiStatsRollout.FieldType;
                piStatsRolloutStartingStats = statsRolloutType.GetProperty("StartingStats", BindingFlags.Instance | BindingFlags.Public);
                piStatsRolloutWorkingStats = statsRolloutType.GetProperty("WorkingStats", BindingFlags.Instance | BindingFlags.Public);
                piStatsRolloutBonusPool = statsRolloutType.GetProperty("BonusPool", BindingFlags.Instance | BindingFlags.Public);

                miStatsRolloutSelectStat = statsRolloutType.GetMethod("SelectStat", BF);
                miStatsRolloutSpinnerUp = statsRolloutType.GetMethod("Spinner_OnUpButtonClicked", BF);
                miStatsRolloutSpinnerDown = statsRolloutType.GetMethod("Spinner_OnDownButtonClicked", BF);
            }

            miRefresh = CacheMethod(type, "Refresh");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallCharacterSheetWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                //! TUNING MAY REQUIRE ADJUSTMENT FOR CURRENT WINDOW
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
                    new LegendOverlay.LegendRow("Version", "6"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate"),
                    new LegendOverlay.LegendRow("DPad Up/Down", "assign level up points"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Invoke level up"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallCharacterSheetWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            // If DFU swapped the panel instance, our old legend is invalid
            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            // If DFU cleared components, our legend may be detached
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

        private void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}
