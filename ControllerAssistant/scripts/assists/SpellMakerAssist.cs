using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class SpellMakerAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Used in EnsureInitialized()
        // Cache for reflection so we don’t re-query every press

        // Reflected action methods
        private MethodInfo miAddEffectButton;
        private MethodInfo miBuyButton;
        private MethodInfo miNewSpellButton;
        private MethodInfo miNameSpellButton;

        private MethodInfo miCasterOnlyButton;
        private MethodInfo miByTouchButton;
        private MethodInfo miSingleTargetAtRangeButton;
        private MethodInfo miAreaAroundCasterButton;
        private MethodInfo miAreaAtRangeButton;

        private MethodInfo miFireBasedButton;
        private MethodInfo miColdBasedButton;
        private MethodInfo miPoisonBasedButton;
        private MethodInfo miShockBasedButton;
        private MethodInfo miMagicBasedButton;

        private MethodInfo miNextIconButton;
        private MethodInfo miPreviousIconButton;
        private MethodInfo miSelectIconButton;

        private MethodInfo miEffect1NamePanel;
        private MethodInfo miEffect2NamePanel;
        private MethodInfo miEffect3NamePanel;

        // Reflected data for effect existence checks
        private FieldInfo fiEffectEntries;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        // Last known occupancy of effect slots so we can detect add/delete/reset changes
        private bool lastEffect1Exists = false;
        private bool lastEffect2Exists = false;
        private bool lastEffect3Exists = false;
        private bool effectStateInitialized = false;

        // Button & selector setup

        private DefaultSelectorBoxHost selectorHost;

        const int AddEffectButton = 0;
        const int BuyButton = 1;
        const int NewButton = 2;
        const int ExitButton = 3;
        const int RangeAreaButton = 4;
        const int CasterAreaButton = 5;
        const int TargetButton = 6;
        const int ByTouchButton = 7;
        const int CasterOnlyButton = 8;
        const int FireBasedButton = 9;
        const int ColdBasedButton = 10;
        const int PoisonBasedButton = 11;
        const int ShockBasedButton = 12;
        const int MagicBasedButton = 13;
        const int SpellNameButton = 14;
        const int Effect3Button = 15;
        const int Effect2Button = 16;
        const int Effect1Button = 17;
        const int NextIconButton = 18;
        const int PreviousIconButton = 19;
        const int SelectIconButton = 20;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(244.1f, 114.1f, 27.9f, 27.9f), // AddEffectButton
                N = Effect3Button, NE = PreviousIconButton, E = CasterOnlyButton, SE = TargetButton, S = BuyButton, SW = SpellNameButton, W = SpellNameButton, NW = Effect3Button },
            new SelectorButtonInfo { rect = new Rect(244.4f, 147.2f, 23.1f, 15.6f), // BuyButton
                N = AddEffectButton, NE = ByTouchButton, E = TargetButton, SE = CasterAreaButton, S = NewButton, SW = SpellNameButton, W = SpellNameButton, NW = Effect3Button },
            new SelectorButtonInfo { rect = new Rect(244.4f, 163.1f, 23.1f, 15.6f), // NewButton
                N = BuyButton, NE = TargetButton, E = CasterAreaButton, SE = RangeAreaButton, S = ExitButton, SW = SpellNameButton, W = SpellNameButton, NW = Effect3Button },
            new SelectorButtonInfo { rect = new Rect(244.4f, 178.7f, 23.1f, 15.6f), // ExitButton
                N = NewButton, NE = CasterAreaButton, E = RangeAreaButton, SE = SpellNameButton, S = Effect1Button, SW = SpellNameButton, W = SpellNameButton, NW = Effect3Button },
            new SelectorButtonInfo { rect = new Rect(275.1f, 178.1f, 24.0f, 15.7f), // RangeAreaButton
                N = CasterAreaButton, NE = ShockBasedButton, E = MagicBasedButton, SE = MagicBasedButton, S = SelectIconButton, SW = ExitButton, W = ExitButton, NW = NewButton },
            new SelectorButtonInfo { rect = new Rect(275.1f, 162.1f, 24.0f, 15.7f), // CasterAreaButton
                N = TargetButton, NE = PoisonBasedButton, E = ShockBasedButton, SE = MagicBasedButton, S = RangeAreaButton, SW = ExitButton, W = NewButton, NW = BuyButton  },
            new SelectorButtonInfo { rect = new Rect(275.1f, 146.0f, 24.0f, 15.7f), // TargetButton
                N = ByTouchButton, NE = ColdBasedButton, E = PoisonBasedButton, SE = ShockBasedButton, S = CasterAreaButton, SW = NewButton, W = BuyButton, NW = AddEffectButton },
            new SelectorButtonInfo { rect = new Rect(275.1f, 130.0f, 24.0f, 15.7f), // ByTouchButton
                N = CasterOnlyButton, NE = FireBasedButton, E = ColdBasedButton, SE = PoisonBasedButton, S = TargetButton, SW = BuyButton, W = AddEffectButton, NW = AddEffectButton },
            new SelectorButtonInfo { rect = new Rect(275.1f, 114.1f, 24.0f, 15.7f), // CasterOnlyButton
                N = SelectIconButton, NE = SelectIconButton, E = FireBasedButton, SE = ColdBasedButton, S = ByTouchButton, SW = AddEffectButton, W = AddEffectButton, NW = PreviousIconButton },
            new SelectorButtonInfo { rect = new Rect(299.0f, 114.1f, 15.9f, 15.9f), // FireBasedButton
                N = SelectIconButton, NE = Effect3Button, E = AddEffectButton, SE = SpellNameButton, S = ColdBasedButton, SW = ByTouchButton, W = CasterOnlyButton, NW = SelectIconButton },
            new SelectorButtonInfo { rect = new Rect(299.0f, 130.1f, 15.9f, 15.9f), // ColdBasedButton
                N = FireBasedButton, NE = Effect3Button, E = AddEffectButton, SE = SpellNameButton, S = PoisonBasedButton, SW = TargetButton, W = ByTouchButton, NW = CasterOnlyButton },
            new SelectorButtonInfo { rect = new Rect(299.0f, 146.0f, 15.9f, 15.9f), // PoisonBasedButton
                N = ColdBasedButton, NE = Effect3Button, E = BuyButton, SE = SpellNameButton, S = ShockBasedButton, SW = CasterAreaButton, W = TargetButton, NW = ByTouchButton },
            new SelectorButtonInfo { rect = new Rect(299.0f, 162.0f, 15.9f, 15.9f), // ShockBasedButton
                N = PoisonBasedButton, NE = Effect3Button, E = NewButton, SE = SpellNameButton, S = MagicBasedButton, SW = RangeAreaButton, W = CasterAreaButton, NW = TargetButton },
            new SelectorButtonInfo { rect = new Rect(299.0f, 178.0f, 15.9f, 15.9f), // MagicBasedButton
                N = ShockBasedButton, NE = Effect3Button, E = SpellNameButton, SE = Effect1Button, S = SelectIconButton, SW = Effect1Button, W = RangeAreaButton, NW = CasterAreaButton },
            new SelectorButtonInfo { rect = new Rect(7.2f, 182.6f, 194.9f, 9.1f), // SpellNameButton
                N = Effect3Button, NE = AddEffectButton, E = ExitButton, SE = Effect1Button, S = Effect1Button, SW = Effect1Button, W = MagicBasedButton, NW = Effect3Button },
            new SelectorButtonInfo { rect = new Rect(78.8f, 89.7f, 163.9f, 20.0f), // Effect3Button
                N = Effect2Button, NE = Effect2Button, E = PreviousIconButton, SE = AddEffectButton, S = SpellNameButton, SW = SpellNameButton, W = SelectIconButton, NW = Effect2Button },
            new SelectorButtonInfo { rect = new Rect(78.8f, 58.0f, 163.9f, 20.0f), // Effect2Button
                N = Effect1Button, NE = Effect1Button, E = PreviousIconButton, SE = AddEffectButton, S = Effect3Button, SW = Effect3Button, W = SelectIconButton, NW = Effect1Button },
            new SelectorButtonInfo { rect = new Rect(78.8f, 26.2f, 163.9f, 20.0f), // Effect1Button
                N = SpellNameButton, NE = ExitButton, E = PreviousIconButton, SE = AddEffectButton, S = Effect2Button, SW = Effect2Button, W = SelectIconButton, NW = SpellNameButton },
            new SelectorButtonInfo { rect = new Rect(275.0f, 80.0f, 9.1f, 15.9f), // NextIconButton
                N = RangeAreaButton, NE = SpellNameButton, E = SelectIconButton, SE = SelectIconButton, S = PreviousIconButton, SW = AddEffectButton, W = Effect3Button, NW = Effect2Button },
            new SelectorButtonInfo { rect = new Rect(275.0f, 95.9f, 9.1f, 15.9f), // PreviousIconButton
                N = NextIconButton, NE = SelectIconButton, E = SelectIconButton, SE = CasterOnlyButton, S = CasterOnlyButton, SW = AddEffectButton, W = Effect3Button, NW = Effect2Button },
            new SelectorButtonInfo { rect = new Rect(285.1f, 90.5f, 22.0f, 22.1f), // SelectIconButton
                N = RangeAreaButton, NE = Effect2Button, E = Effect3Button, SE = FireBasedButton, S = CasterOnlyButton, SW = CasterOnlyButton, W = PreviousIconButton, NW = NextIconButton },
        };


        //private AnchorEditor editor;

        public int buttonSelected = AddEffectButton;

        private void ActivateSelectedButton(DaggerfallSpellMakerWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case AddEffectButton:
                    InvokeButtonHandler(menuWindow, miAddEffectButton);
                    break;

                case BuyButton:
                    InvokeButtonHandler(menuWindow, miBuyButton);
                    break;

                case NewButton:
                    InvokeButtonHandler(menuWindow, miNewSpellButton);
                    break;

                case ExitButton:
                    SelectExit(menuWindow);
                    break;

                case RangeAreaButton:
                    InvokeButtonHandler(menuWindow, miAreaAtRangeButton);
                    break;

                case CasterAreaButton:
                    InvokeButtonHandler(menuWindow, miAreaAroundCasterButton);
                    break;

                case TargetButton:
                    InvokeButtonHandler(menuWindow, miSingleTargetAtRangeButton);
                    break;

                case ByTouchButton:
                    InvokeButtonHandler(menuWindow, miByTouchButton);
                    break;

                case CasterOnlyButton:
                    InvokeButtonHandler(menuWindow, miCasterOnlyButton);
                    break;

                case FireBasedButton:
                    InvokeButtonHandler(menuWindow, miFireBasedButton);
                    break;

                case ColdBasedButton:
                    InvokeButtonHandler(menuWindow, miColdBasedButton);
                    break;

                case PoisonBasedButton:
                    InvokeButtonHandler(menuWindow, miPoisonBasedButton);
                    break;

                case ShockBasedButton:
                    InvokeButtonHandler(menuWindow, miShockBasedButton);
                    break;

                case MagicBasedButton:
                    InvokeButtonHandler(menuWindow, miMagicBasedButton);
                    break;

                case SpellNameButton:
                    InvokeButtonHandler(menuWindow, miNameSpellButton);
                    break;

                case Effect3Button:
                    if (EffectSlotExists(menuWindow, 2))
                        InvokeButtonHandler(menuWindow, miEffect3NamePanel);
                    break;

                case Effect2Button:
                    if (EffectSlotExists(menuWindow, 1))
                        InvokeButtonHandler(menuWindow, miEffect2NamePanel);
                    break;

                case Effect1Button:
                    if (EffectSlotExists(menuWindow, 0))
                        InvokeButtonHandler(menuWindow, miEffect1NamePanel);
                    break;

                case NextIconButton:
                    InvokeButtonHandler(menuWindow, miNextIconButton);
                    break;

                case PreviousIconButton:
                    InvokeButtonHandler(menuWindow, miPreviousIconButton);
                    break;

                case SelectIconButton:
                    InvokeButtonHandler(menuWindow, miSelectIconButton);
                    break;
            }
        }

        private void TryMoveSelector(DaggerfallSpellMakerWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            var btn = menuButton[buttonSelected];

            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.NE: next = btn.NE; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.SE: next = btn.SE; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.SW: next = btn.SW; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
                case ControllerManager.StickDir8.NW: next = btn.NW; break;
            }

            if (next < 0)
                return;

            next = ResolveEffectButtonTarget(menuWindow, previous, dir, next);
            if (next < 0)
                return;

            buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }
        private Panel GetCurrentRenderPanel(DaggerfallSpellMakerWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallSpellMakerWindow menuWindow)
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

        private void RefreshSelectorAttachment(DaggerfallSpellMakerWindow menuWindow)
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
            return top is DaggerfallSpellMakerWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallSpellMakerWindow menuWindow = top as DaggerfallSpellMakerWindow;

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

            DestroyLegend();
            DestroySelectorBox();

            legendVisible = false;
            panelRenderWindow = null;

            effectStateInitialized = false;
            lastEffect1Exists = false;
            lastEffect2Exists = false;
            lastEffect3Exists = false;
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallSpellMakerWindow menuWindow, ControllerManager cm)
        {
            if (HasEffectStateChanged(menuWindow))
            {
                HandleEffectStateChanged(menuWindow);
            }

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            if (selectorHost == null)
            {
                RefreshSelectorToCurrentButton(menuWindow);
            }
            else
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                    RefreshSelectorToCurrentButton(menuWindow);
            }

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);


            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
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
                    //editor.Toggle();
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

        private void SelectExit(DaggerfallSpellMakerWindow menuWindow)
        {
            DestroyLegend();
            menuWindow.CloseWindow();
            return;
        }

        private void InvokeButtonHandler(DaggerfallSpellMakerWindow menuWindow, MethodInfo mi)
        {
            if (menuWindow == null || mi == null)
                return;

            mi.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private bool EffectSlotExists(DaggerfallSpellMakerWindow menuWindow, int slot)
        {
            if (menuWindow == null || fiEffectEntries == null)
                return false;

            Array entries = fiEffectEntries.GetValue(menuWindow) as Array;
            if (entries == null)
                return false;

            if (slot < 0 || slot >= entries.Length)
                return false;

            object entry = entries.GetValue(slot);
            if (entry == null)
                return false;

            // EffectEntry is a struct/class with a Key member.
            var entryType = entry.GetType();

            var keyField = entryType.GetField("Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (keyField != null)
            {
                string key = keyField.GetValue(entry) as string;
                return !string.IsNullOrEmpty(key);
            }

            var keyProp = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (keyProp != null)
            {
                string key = keyProp.GetValue(entry, null) as string;
                return !string.IsNullOrEmpty(key);
            }

            return false;
        }

        private bool IsEffectButton(int button)
        {
            return button == Effect1Button || button == Effect2Button || button == Effect3Button;
        }

        private int GetEffectButtonForSlot(int slot)
        {
            switch (slot)
            {
                case 0: return Effect1Button;
                case 1: return Effect2Button;
                case 2: return Effect3Button;
                default: return -1;
            }
        }

        private int ResolveEffectButtonTarget(DaggerfallSpellMakerWindow menuWindow, int currentButton, ControllerManager.StickDir8 dir, int targetButton)
        {
            // Only normalize effect targets.
            if (!IsEffectButton(targetButton))
                return targetButton;

            // Special case:
            // Moving downward out of the effect stack should fall through to Spell Name
            // when the next lower effect does not exist, rather than snapping back upward.
            if (dir == ControllerManager.StickDir8.S)
            {
                if (currentButton == Effect1Button && targetButton == Effect2Button && !EffectSlotExists(menuWindow, 1))
                    return SpellNameButton;

                if (currentButton == Effect2Button && targetButton == Effect3Button && !EffectSlotExists(menuWindow, 2))
                    return SpellNameButton;
            }

            // Requested Effect 3 -> try 3, then 2, then 1
            if (targetButton == Effect3Button)
            {
                if (EffectSlotExists(menuWindow, 2)) return Effect3Button;
                if (EffectSlotExists(menuWindow, 1)) return Effect2Button;
                if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                return -1;
            }

            // Requested Effect 2 -> try 2, then 1
            if (targetButton == Effect2Button)
            {
                if (EffectSlotExists(menuWindow, 1)) return Effect2Button;
                if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                return -1;
            }

            // Requested Effect 1 -> only 1 is valid
            if (targetButton == Effect1Button)
            {
                if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                return -1;
            }

            return -1;
        }

        private void CaptureEffectState(DaggerfallSpellMakerWindow menuWindow)
        {
            lastEffect1Exists = EffectSlotExists(menuWindow, 0);
            lastEffect2Exists = EffectSlotExists(menuWindow, 1);
            lastEffect3Exists = EffectSlotExists(menuWindow, 2);
            effectStateInitialized = true;
        }

        private bool HasEffectStateChanged(DaggerfallSpellMakerWindow menuWindow)
        {
            bool e1 = EffectSlotExists(menuWindow, 0);
            bool e2 = EffectSlotExists(menuWindow, 1);
            bool e3 = EffectSlotExists(menuWindow, 2);

            if (!effectStateInitialized)
            {
                lastEffect1Exists = e1;
                lastEffect2Exists = e2;
                lastEffect3Exists = e3;
                effectStateInitialized = true;
                return false;
            }

            return e1 != lastEffect1Exists ||
                   e2 != lastEffect2Exists ||
                   e3 != lastEffect3Exists;
        }

        private void HandleEffectStateChanged(DaggerfallSpellMakerWindow menuWindow)
        {
            EnsureValidCurrentSelection(menuWindow);
            CaptureEffectState(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);
        }

        private void EnsureValidCurrentSelection(DaggerfallSpellMakerWindow menuWindow)
        {
            if (!IsEffectButton(buttonSelected))
                return;

            int resolved = ResolveCurrentEffectSelection(menuWindow, buttonSelected);
            if (resolved >= 0)
                buttonSelected = resolved;
            else
                buttonSelected = AddEffectButton;
        }

        private int ResolveCurrentEffectSelection(DaggerfallSpellMakerWindow menuWindow, int currentButton)
        {
            switch (currentButton)
            {
                case Effect1Button:
                    if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                    if (EffectSlotExists(menuWindow, 1)) return Effect2Button;
                    if (EffectSlotExists(menuWindow, 2)) return Effect3Button;
                    return -1;

                case Effect2Button:
                    if (EffectSlotExists(menuWindow, 1)) return Effect2Button;
                    if (EffectSlotExists(menuWindow, 2)) return Effect3Button;
                    if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                    return -1;

                case Effect3Button:
                    if (EffectSlotExists(menuWindow, 2)) return Effect3Button;
                    if (EffectSlotExists(menuWindow, 1)) return Effect2Button;
                    if (EffectSlotExists(menuWindow, 0)) return Effect1Button;
                    return -1;
            }

            return currentButton;
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallSpellMakerWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
            CaptureEffectState(menuWindow);
            EnsureValidCurrentSelection(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);
       
            //// Anchor Editor
            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallSpellMakerWindow closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallSpellMakerWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiEffectEntries = CacheField(type, "effectEntries");

            miAddEffectButton = CacheMethod(type, "AddEffectButton_OnMouseClick");
            miBuyButton = CacheMethod(type, "BuyButton_OnMouseClick");
            miNewSpellButton = CacheMethod(type, "NewSpellButton_OnMouseClick");
            miNameSpellButton = CacheMethod(type, "NameSpellButton_OnMouseClick");

            miCasterOnlyButton = CacheMethod(type, "CasterOnlyButton_OnMouseClick");
            miByTouchButton = CacheMethod(type, "ByTouchButton_OnMouseClick");
            miSingleTargetAtRangeButton = CacheMethod(type, "SingleTargetAtRangeButton_OnMouseClick");
            miAreaAroundCasterButton = CacheMethod(type, "AreaAroundCasterButton_OnMouseClick");
            miAreaAtRangeButton = CacheMethod(type, "AreaAtRangeButton_OnMouseClick");

            miFireBasedButton = CacheMethod(type, "FireBasedButton_OnMouseClick");
            miColdBasedButton = CacheMethod(type, "ColdBasedButton_OnMouseClick");
            miPoisonBasedButton = CacheMethod(type, "PoisonBasedButton_OnMouseClick");
            miShockBasedButton = CacheMethod(type, "ShockBasedButton_OnMouseClick");
            miMagicBasedButton = CacheMethod(type, "MagicBasedButton_OnMouseClick");

            miNextIconButton = CacheMethod(type, "NextIconButton_OnMouseClick");
            miPreviousIconButton = CacheMethod(type, "PreviousIconButton_OnMouseClick");
            miSelectIconButton = CacheMethod(type, "SelectIconButton_OnMouseClick");

            miEffect1NamePanel = CacheMethod(type, "Effect1NamePanel_OnMouseClick");
            miEffect2NamePanel = CacheMethod(type, "Effect2NamePanel_OnMouseClick");
            miEffect3NamePanel = CacheMethod(type, "Effect3NamePanel_OnMouseClick");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallSpellMakerWindow menuWindow, ControllerManager cm)
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
                    //new LegendOverlay.LegendRow("D-Pad", "move scroll bars"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                    //new LegendOverlay.LegendRow(cm.Action2Name, "change tone"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallSpellMakerWindow menuWindow)
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