using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
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
        //! EXAMPLES: declare only what the target window actually needs
        // private MethodInfo miActionMoveLeft;
        // private MethodInfo miActionMoveRight;
        // private MethodInfo miActionMoveForward;
        // private MethodInfo miActionMoveBackward;
        // private MethodInfo miActionMoveUpstairs;
        // private MethodInfo miActionMoveDownstairs;
        // private MethodInfo miActionResetView;
        // private MethodInfo miActionRotateLeft;
        // private MethodInfo miActionRotateRight;
        // private MethodInfo miActionCenterMapOnPlayer;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool closeDeferred = false;

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
            if (buttonSelected == ExitButton) SelectExit(menuWindow);
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

            if (next > -1)
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
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallSpellMakerWindow menuWindow, ControllerManager cm)
        {

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

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallSpellMakerWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
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

            //! REPLACE WITH ACTUAL METHOD/FIELD NAMES NEEDED BY THIS WINDOW
            // miActionMoveLeft = CacheMethod(type, "ActionMoveLeft");
            // miActionMoveRight = CacheMethod(type, "ActionMoveRight");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

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