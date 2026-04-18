using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class PauseAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private MethodInfo miSaveButtonOnMouseClick;
        private MethodInfo miLoadButtonOnMouseClick;
        private MethodInfo miExitButtonOnMouseClick;
        private MethodInfo miContinueButtonOnMouseClick;
        private MethodInfo miControlsButtonOnMouseClick;
        private MethodInfo miFullScreenButtonOnMouseClick;
        private MethodInfo miHeadBobbingButtonOnMouseClick;
        private MethodInfo miSoundBarOnMouseClick;
        private MethodInfo miMusicBarOnMouseClick;
        private MethodInfo miDetailButtonOnMouseClick;

        private PauseQuickButtonOverlay quickButtonOverlay;
        private Texture2D quickButtonsAtlas;

        private const float PauseBarMaxLength = 109.1f;
        private const float PauseBarClickY = 2.75f;
        private const float PauseVolumeStep = 0.10f;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        // Button & selector setup

        private DefaultSelectorBoxHost selectorHost;

        const int SaveGameButton = 0;
        const int LoadGameButton = 1;
        const int ExitButton = 2;
        const int SoundLevel = 3;
        const int MusicLevel = 4;
        const int DetailLevel = 5;
        const int FullScreenButton = 6;
        const int HeadBobbingButton = 7;
        const int ControlsButton = 8;
        const int ContinueButton = 9;
        const int LocalMapButton = 10;
        const int StatusButton = 11;
        const int TransportButton = 12;
        const int CharacterButton = 13;
        const int TravelMapButton = 14;
        const int SpellbookButton = 15;
        const int LogbookButton = 16;
        const int NotebookButton = 17;
        const int RestButton = 18;
        const int InventoryButton = 19;
        const int QuicksaveButton = 20;
        const int QuickLoadButton = 21;
        const int UseMagicItemButton = 22;

        const int IncreaseSound = 1000;
        const int DecreaseSound = 1001;
        const int IncreaseMusic = 1002;
        const int DecreaseMusic = 1003;
        const int IncreaseDetail = 1004;
        const int DecreaseDetail = 1005;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(88.9f, 43.5f, 45.6f, 16.7f), N = RestButton, E = LoadGameButton, S = SoundLevel, W = ExitButton }, // SaveGameButton
            new SelectorButtonInfo { rect = new Rect(136.8f, 43.5f, 46.7f, 16.7f), N = UseMagicItemButton, E = ExitButton, S = SoundLevel, W = SaveGameButton }, // LoadGameButton
            new SelectorButtonInfo { rect = new Rect(185.8f, 43.5f, 45.8f, 16.7f), N = QuickLoadButton, E = SaveGameButton, S = SoundLevel, W = LoadGameButton }, // ExitButton
            new SelectorButtonInfo { rect = new Rect(88.2f, 61.0f, 143.9f, 8.5f), N = LoadGameButton, E = IncreaseSound, S = MusicLevel, W = DecreaseSound }, // SoundLevel
            new SelectorButtonInfo { rect = new Rect(88.2f, 68.7f, 143.9f, 8.5f), N = SoundLevel, E = IncreaseMusic, S = DetailLevel, W = DecreaseMusic }, // MusicLevel
            new SelectorButtonInfo { rect = new Rect(88.2f, 76.8f, 143.9f, 8.5f), N = MusicLevel, E = IncreaseDetail, S = FullScreenButton, W = DecreaseDetail }, // DetailLevel
            new SelectorButtonInfo { rect = new Rect(89.6f, 86.6f, 70.8f, 9.9f), N = DetailLevel, E = HeadBobbingButton, S = ControlsButton, W = HeadBobbingButton }, // FullScreenButton
            new SelectorButtonInfo { rect = new Rect(159.7f, 86.6f, 71.8f, 9.9f), N = DetailLevel, E = FullScreenButton, S = ContinueButton, W = FullScreenButton }, // HeadBobbingButton
            new SelectorButtonInfo { rect = new Rect(89.0f, 99.9f, 71.3f, 17.9f), N = FullScreenButton, E = ContinueButton, S = LocalMapButton, W = ContinueButton }, // ControlsButton
            new SelectorButtonInfo { rect = new Rect(160.0f, 99.9f, 71.3f, 17.9f), N = HeadBobbingButton, E = ControlsButton, S = TransportButton, W = ControlsButton }, // ContinueButton

            new SelectorButtonInfo { rect = new Rect(85.9f, 130.0f, 34.4f, 6.8f), N = ControlsButton,  E = StatusButton,    S = TravelMapButton, W = CharacterButton }, // LocalMapButton
            new SelectorButtonInfo { rect = new Rect(124.2f, 130.0f, 34.4f, 6.8f), N = ControlsButton,  E = TransportButton, S = SpellbookButton, W = LocalMapButton }, // StatusButton
            new SelectorButtonInfo { rect = new Rect(162.5f, 130.0f, 34.4f, 6.8f), N = ContinueButton,  E = CharacterButton, S = LogbookButton,   W = StatusButton }, // TransportButton
            new SelectorButtonInfo { rect = new Rect(200.8f, 130.0f, 34.4f, 6.8f), N = ContinueButton,  E = LocalMapButton,  S = NotebookButton,  W = TransportButton }, // CharacterButton

            new SelectorButtonInfo { rect = new Rect(85.9f, 142.2f, 34.4f, 6.8f), N = LocalMapButton,   E = SpellbookButton, S = RestButton,      W = NotebookButton }, // TravelMapButton
            new SelectorButtonInfo { rect = new Rect(124.2f, 142.2f, 34.4f, 6.8f), N = StatusButton,    E = LogbookButton,   S = InventoryButton, W = TravelMapButton }, // SpellbookButton
            new SelectorButtonInfo { rect = new Rect(162.5f, 142.2f, 34.4f, 6.8f), N = TransportButton, E = NotebookButton,  S = QuicksaveButton, W = SpellbookButton }, // LogbookButton
            new SelectorButtonInfo { rect = new Rect(200.8f, 142.2f, 34.4f, 6.8f), N = CharacterButton, E = TravelMapButton, S = QuickLoadButton, W = LogbookButton }, // NotebookButton

            new SelectorButtonInfo { rect = new Rect(85.9f, 154.4f, 34.4f, 6.8f), N = TravelMapButton,  E = InventoryButton, S = SaveGameButton,   W = QuickLoadButton }, // RestButton
            new SelectorButtonInfo { rect = new Rect(124.2f, 154.4f, 34.4f, 6.8f), N = SpellbookButton, E = QuicksaveButton, S = UseMagicItemButton, W = RestButton }, // InventoryButton
            new SelectorButtonInfo { rect = new Rect(162.5f, 154.4f, 34.4f, 6.8f), N = LogbookButton,   E = QuickLoadButton, S = UseMagicItemButton, W = InventoryButton }, // QuicksaveButton
            new SelectorButtonInfo { rect = new Rect(200.8f, 154.4f, 34.4f, 6.8f), N = NotebookButton,  E = RestButton,      S = ExitButton,       W = QuicksaveButton }, // QuickLoadButton

            new SelectorButtonInfo { rect = new Rect(124.2f, 166.6f, 72.7f, 6.8f), N = InventoryButton, E = -1, S = LoadGameButton, W = -1 }, // UseMagicItemButton
        };


        public int buttonSelected = SaveGameButton;

        private void ActivateSelectedButton(DaggerfallPauseOptionsWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case SaveGameButton: InvokeWindowClick(miSaveButtonOnMouseClick, menuWindow); break;
                case LoadGameButton: InvokeWindowClick(miLoadButtonOnMouseClick, menuWindow); break;
                case ExitButton: InvokeWindowClick(miExitButtonOnMouseClick, menuWindow); break;
                case FullScreenButton: InvokeWindowClick(miFullScreenButtonOnMouseClick, menuWindow); break;
                case HeadBobbingButton: InvokeWindowClick(miHeadBobbingButtonOnMouseClick, menuWindow); break;
                case ControlsButton: InvokeWindowClick(miControlsButtonOnMouseClick, menuWindow); break;
                case ContinueButton: InvokeWindowClick(miContinueButtonOnMouseClick, menuWindow); break;

                case LocalMapButton: SelectLocalMap(menuWindow); break;
                case StatusButton: SelectStatus(menuWindow); break;
                case TransportButton: SelectTransport(menuWindow); break;
                case CharacterButton: SelectCharacter(menuWindow); break;
                case TravelMapButton: SelectTravelMap(menuWindow); break;
                case SpellbookButton: SelectSpellbook(menuWindow); break;
                case LogbookButton: SelectLogbook(menuWindow); break;
                case NotebookButton: SelectNotebook(menuWindow); break;
                case RestButton: SelectRest(menuWindow); break;
                case InventoryButton: SelectInventory(menuWindow); break;
                case QuicksaveButton: SelectQuickSave(menuWindow); break;
                case QuickLoadButton: SelectQuickLoad(menuWindow); break;
                case UseMagicItemButton: SelectUseMagicItem(menuWindow); break;
            }
        }

        private void TryMoveSelector(DaggerfallPauseOptionsWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            var btn = menuButton[buttonSelected];

            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
            }

            if (next > -1)
                buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }


        private Panel GetCurrentRenderPanel(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (menuWindow == null)
                return null;

            return menuWindow.ParentPanel;
        }


        private void RefreshSelectorToCurrentButton(DaggerfallPauseOptionsWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallPauseOptionsWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
                selectorHost.Destroy();
        }


        // =========================
        // IMenuAssist
        // =========================
        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallPauseOptionsWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallPauseOptionsWindow menuWindow = top as DaggerfallPauseOptionsWindow;

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

            DestroyLegend();
            DestroySelectorBox();
            DestroyQuickButtonOverlay();

            legendVisible = false;
            panelRenderWindow = null;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);
            EnsureQuickButtonOverlay(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomRight();

            if (panelRenderWindow == null)
                panelRenderWindow = GetCurrentRenderPanel(menuWindow);

            bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
            bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

            // Slider handling first
            if (buttonSelected == SoundLevel)
            {
                if (moveLeft) { AdjustSoundLevel(menuWindow, false); return; }
                if (moveRight) { AdjustSoundLevel(menuWindow, true); return; }
            }
            else if (buttonSelected == MusicLevel)
            {
                if (moveLeft) { AdjustMusicLevel(menuWindow, false); return; }
                if (moveRight) { AdjustMusicLevel(menuWindow, true); return; }
            }
            else if (buttonSelected == DetailLevel)
            {
                if (moveLeft) { AdjustDetailLevel(menuWindow, false); return; }
                if (moveRight) { AdjustDetailLevel(menuWindow, true); return; }
            }

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            // Quick actions (bypass selector, use same methods as buttons)
            if (cm.DPadRightReleased)
            {
                InvokeWindowClick(miExitButtonOnMouseClick, menuWindow);
                return;
            }

            if (cm.DPadDownReleased)
            {
                SelectInventory(menuWindow);
                return;
            }

            if (cm.DPadLeftReleased)
            {
                SelectTravelMap(menuWindow);
                return;
            }

            if (cm.DPadUpReleased)
            {
                SelectLocalMap(menuWindow);
                return;
            }

            if (cm.Action2Released)
            {
                SelectQuickSave(menuWindow);
                return;
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.Action1Released)
                    ActivateSelectedButton(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        // =========================
        // Assist action helpers
        // =========================

        private void SelectContinue(DaggerfallPauseOptionsWindow menuWindow)
        {
            DestroyLegend();
            menuWindow.CloseWindow();
            return;
        }

        private Rect[] GetQuickButtonNativeRects()
        {
            return new Rect[]
            {
                new Rect(84f, 128f, 35f, 10f),   // Local Map
                new Rect(123f, 128f, 35f, 10f),  // Status
                new Rect(162f, 128f, 35f, 10f),  // Transport
                new Rect(201f, 128f, 35f, 10f),  // Character

                new Rect(84f, 142f, 35f, 10f),   // Travel Map
                new Rect(123f, 142f, 35f, 10f),  // Spellbook
                new Rect(162f, 142f, 35f, 10f),  // Logbook
                new Rect(201f, 142f, 35f, 10f),  // Notebook

                new Rect(84f, 156f, 35f, 10f),   // Rest
                new Rect(123f, 156f, 35f, 10f),  // Inventory
                new Rect(162f, 156f, 35f, 10f),  // QuickSave
                new Rect(201f, 156f, 35f, 10f),  // QuickLoad
                new Rect(123f, 168f, 74f, 10f),  // Use Magic Item
            };
        }
        private Texture2D LoadQuickButtonsAtlas()
        {
            if (quickButtonsAtlas != null)
                return quickButtonsAtlas;

            Mod mod = ModManager.Instance.GetMod("ControllerAssistant");
            if (mod == null)
                return null;

            Texture2D tex = mod.GetAsset<Texture2D>("buttonatlas");
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Point; // or Bilinear
            }

            quickButtonsAtlas = tex;
            return quickButtonsAtlas;
        }


        private void ApplyQuickButtonRects()
        {
            Rect[] rects = GetQuickButtonNativeRects();

            menuButton[LocalMapButton].rect = rects[0];
            menuButton[StatusButton].rect = rects[1];
            menuButton[TransportButton].rect = rects[2];
            menuButton[CharacterButton].rect = rects[3];
            menuButton[TravelMapButton].rect = rects[4];
            menuButton[SpellbookButton].rect = rects[5];
            menuButton[LogbookButton].rect = rects[6];
            menuButton[NotebookButton].rect = rects[7];
            menuButton[RestButton].rect = rects[8];
            menuButton[InventoryButton].rect = rects[9];
            menuButton[QuicksaveButton].rect = rects[10];
            menuButton[QuickLoadButton].rect = rects[11];
            menuButton[UseMagicItemButton].rect = rects[12];
        }

        private void EnsureQuickButtonOverlay(DaggerfallPauseOptionsWindow menuWindow)
        {
            Panel panel = GetCurrentRenderPanel(menuWindow);
            if (panel == null)
                return;

            Texture2D atlas = LoadQuickButtonsAtlas();
            if (atlas == null)
                return;

            if (quickButtonOverlay == null || !quickButtonOverlay.IsAttached())
            {
                quickButtonOverlay = new PauseQuickButtonOverlay(
                    panel,
                    GetQuickButtonNativeRects(),
                    atlas,
                    delegate { buttonSelected = LocalMapButton; SelectLocalMap(menuWindow); },
                    delegate { buttonSelected = StatusButton; SelectStatus(menuWindow); },
                    delegate { buttonSelected = TransportButton; SelectTransport(menuWindow); },
                    delegate { buttonSelected = CharacterButton; SelectCharacter(menuWindow); },
                    delegate { buttonSelected = TravelMapButton; SelectTravelMap(menuWindow); },
                    delegate { buttonSelected = SpellbookButton; SelectSpellbook(menuWindow); },
                    delegate { buttonSelected = LogbookButton; SelectLogbook(menuWindow); },
                    delegate { buttonSelected = NotebookButton; SelectNotebook(menuWindow); },
                    delegate { buttonSelected = RestButton; SelectRest(menuWindow); },
                    delegate { buttonSelected = InventoryButton; SelectInventory(menuWindow); },
                    delegate { buttonSelected = QuicksaveButton; SelectQuickSave(menuWindow); },
                    delegate { buttonSelected = QuickLoadButton; SelectQuickLoad(menuWindow); },
                    delegate { buttonSelected = UseMagicItemButton; SelectUseMagicItem(menuWindow); });
                quickButtonOverlay.Build();
            }
            else
            {
                quickButtonOverlay.SetLayout();
            }
        }

        private void DestroyQuickButtonOverlay()
        {
            if (quickButtonOverlay != null)
            {
                quickButtonOverlay.Destroy();
                quickButtonOverlay = null;
            }
        }

        private void InvokeWindowClick(MethodInfo mi, DaggerfallPauseOptionsWindow menuWindow)
        {
            if (mi == null || menuWindow == null)
                return;

            mi.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void InvokeBarClick(MethodInfo mi, DaggerfallPauseOptionsWindow menuWindow, float x)
        {
            if (mi == null || menuWindow == null)
                return;

            x = Mathf.Clamp(x, 0f, PauseBarMaxLength);

            // DetailButton_OnMouseClick() needs sender.Size.x.
            // Sound/Music ignore sender, so this is safe for all three.
            Panel dummySender = new Panel();
            dummySender.Size = new Vector2(PauseBarMaxLength, 5.5f);

            mi.Invoke(menuWindow, new object[] { dummySender, new Vector2(x, PauseBarClickY) });
        }

        private void AdjustSoundLevel(DaggerfallPauseOptionsWindow menuWindow, bool increase)
        {
            float value = DaggerfallUnity.Settings.SoundVolume + (increase ? PauseVolumeStep : -PauseVolumeStep);
            value = Mathf.Clamp01((float)System.Math.Round(value, 2));
            InvokeBarClick(miSoundBarOnMouseClick, menuWindow, value * PauseBarMaxLength);
        }

        private void AdjustMusicLevel(DaggerfallPauseOptionsWindow menuWindow, bool increase)
        {
            float value = DaggerfallUnity.Settings.MusicVolume + (increase ? PauseVolumeStep : -PauseVolumeStep);
            value = Mathf.Clamp01((float)System.Math.Round(value, 2));
            InvokeBarClick(miMusicBarOnMouseClick, menuWindow, value * PauseBarMaxLength);
        }

        private void AdjustDetailLevel(DaggerfallPauseOptionsWindow menuWindow, bool increase)
        {
            int max = QualitySettings.names.Length - 1;
            int current = QualitySettings.GetQualityLevel();
            int next = Mathf.Clamp(current + (increase ? 1 : -1), 0, max);

            // Click at the center of the target quality bucket
            float bucketWidth = PauseBarMaxLength / (max + 1f);
            float clickX = (next + 0.5f) * bucketWidth;

            InvokeBarClick(miDetailButtonOnMouseClick, menuWindow, clickX);
        }

        private void SelectLocalMap(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenAutomap);
        }

        private void SelectStatus(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiStatusInfo);
        }

        private void SelectTransport(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenTransportWindow);
        }

        private void SelectCharacter(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenCharacterSheetWindow);
        }

        private void SelectTravelMap(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenTravelMapWindow);
        }

        private void SelectSpellbook(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenSpellBookWindow);
        }

        private void SelectLogbook(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenQuestJournalWindow);
        }

        private void SelectNotebook(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenNotebookWindow);
        }

        private void SelectRest(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenRestWindow);
        }

        private void SelectInventory(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenInventoryWindow);
        }

        private void SelectQuickSave(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenQuickSave(menuWindow);
        }

        private void SelectQuickLoad(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenQuickLoad(menuWindow);
        }
        private void SelectUseMagicItem(DaggerfallPauseOptionsWindow menuWindow)
        {
            ClosePauseThenPostMessage(menuWindow, DaggerfallUIMessages.dfuiOpenUseMagicItemWindow);
        }

        private void ClosePauseWindow(DaggerfallPauseOptionsWindow menuWindow)
        {
            DestroyLegend();

            if (menuWindow != null)
                menuWindow.CloseWindow();
        }

        private void ClosePauseThenPostMessage(DaggerfallPauseOptionsWindow menuWindow, string uiMessage)
        {
            if (string.IsNullOrEmpty(uiMessage))
                return;

            ClosePauseWindow(menuWindow);
            DaggerfallUI.PostMessage(uiMessage);
        }

        private void ClosePauseThenQuickSave(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveLoadManager == null)
                return;

            if (GameManager.Instance.SaveLoadManager.IsSavingPrevented)
            {
                DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("cannotSaveNow"));
                return;
            }

            ClosePauseWindow(menuWindow);
            SaveLoadManager.Instance.QuickSave();
        }

        private void ClosePauseThenQuickLoad(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveLoadManager == null)
                return;

            if (GameManager.Instance.PlayerEntity == null)
                return;

            string playerName = GameManager.Instance.PlayerEntity.Name;

            if (string.IsNullOrEmpty(playerName))
                return;

            // Always close pause first
            ClosePauseWindow(menuWindow);

            if (!SaveLoadManager.Instance.HasQuickSave(playerName))
            {
                DaggerfallUI.AddHUDText("No quicksave found.");
                return;
            }

            GameManager.Instance.SaveLoadManager.PromptQuickLoadGame(playerName, () =>
            {
                SaveLoadManager.Instance.QuickLoad();
            });
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
            ApplyQuickButtonRects();
            EnsureQuickButtonOverlay(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);

        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallPauseOptionsWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================

        private void EnsureInitialized(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            miSaveButtonOnMouseClick = CacheMethod(type, "SaveButton_OnMouseClick");
            miLoadButtonOnMouseClick = CacheMethod(type, "LoadButton_OnMouseClick");
            miExitButtonOnMouseClick = CacheMethod(type, "ExitButton_OnMouseClick");
            miContinueButtonOnMouseClick = CacheMethod(type, "ContinueButton_OnMouseClick");
            miControlsButtonOnMouseClick = CacheMethod(type, "ControlsButton_OnMouseClick");
            miFullScreenButtonOnMouseClick = CacheMethod(type, "FullScreenButton_OnMouseClick");
            miHeadBobbingButtonOnMouseClick = CacheMethod(type, "HeadBobbingButton_OnMouseClick");
            miSoundBarOnMouseClick = CacheMethod(type, "SoundBar_OnMouseClick");
            miMusicBarOnMouseClick = CacheMethod(type, "MusicBar_OnMouseClick");
            miDetailButtonOnMouseClick = CacheMethod(type, "DetailButton_OnMouseClick");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================

        private void EnsureLegendUI(DaggerfallPauseOptionsWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            panelRenderWindow = GetCurrentRenderPanel(menuWindow);
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
                    new LegendOverlay.LegendRow("D-Pad Right", "Exit"),
                    new LegendOverlay.LegendRow("D-Pad Down", "Inventory"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Travel Map"),
                    new LegendOverlay.LegendRow("D-Pad Up", "Local Map"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow("RStick L/R on sliders", "adjust level"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Quicksave"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallPauseOptionsWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            Panel current = GetCurrentRenderPanel(menuWindow);
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroyLegend();
                DestroyQuickButtonOverlay();
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