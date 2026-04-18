using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TravelPopUpAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiBeginButton;
        private FieldInfo fiExitButton;
        private FieldInfo fiCautiousToggleButton;
        private FieldInfo fiRecklessToggleButton;
        private FieldInfo fiFootHorseToggleButton;
        private FieldInfo fiShipToggleButton;
        private FieldInfo fiInnToggleButton;
        private FieldInfo fiCampOutToggleButton;

        private MethodInfo miBeginButtonOnClickHandler;
        private MethodInfo miExitButtonOnClickHandler;
        private MethodInfo miToggleSpeedButtonOnScrollHandler;
        private MethodInfo miToggleTransportModeButtonOnScrollHandler;
        private MethodInfo miToggleSleepModeButtonOnScrollHandler;
        private MethodInfo miSpeedButtonOnClickHandler;
        private MethodInfo miTransportModeButtonOnClickHandler;
        private MethodInfo miSleepModeButtonOnClickHandler;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private bool closeDeferred = false;

        // Button & selector setup

        private DefaultSelectorBoxHost selectorHost;

        const int CautiouslyButton = 0;
        const int RecklesslyButton = 1;
        const int FootHorseButton = 2;
        const int ShipButton = 3;
        const int InnsButton = 4;
        const int CampOutButton = 5;
        const int BeginButton = 6;
        const int ExitButton = 7;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(48.9f, 49.7f, 110.3f, 10.6f), N = -1, E = FootHorseButton, S = RecklesslyButton, W = -1 }, // CautiouslyButton
            new SelectorButtonInfo { rect = new Rect(48.9f, 60.0f, 110.3f, 10.6f), N = CautiouslyButton, E = ShipButton, S = InnsButton, W = -1 }, // RecklesslyButton
            new SelectorButtonInfo { rect = new Rect(161.6f, 50.0f, 110.3f, 10.6f), N = -1, E = -1, S = ShipButton, W = CautiouslyButton }, // FootHorseButton
            new SelectorButtonInfo { rect = new Rect(161.6f, 60.1f, 110.3f, 10.6f), N = FootHorseButton, E = -1, S = CampOutButton, W = RecklesslyButton }, // ShipButton
            new SelectorButtonInfo { rect = new Rect(48.9f, 82.4f, 110.3f, 10.6f), N = RecklesslyButton, E = CampOutButton, S = BeginButton, W = -1 }, // InnsButton
            new SelectorButtonInfo { rect = new Rect(161.6f, 82.4f, 110.3f, 10.6f), N = ShipButton, E = -1, S = BeginButton, W = InnsButton }, // CampOutButton
            new SelectorButtonInfo { rect = new Rect(219.6f, 94.4f, 52.3f, 15.6f), N = CampOutButton, E = -1, S = ExitButton, W = InnsButton }, // BeginButton
            new SelectorButtonInfo { rect = new Rect(219.6f, 109.5f, 52.3f, 15.6f), N = BeginButton, E = -1, S = -1, W = InnsButton }, // ExitButton
        };


        public int buttonSelected = BeginButton;

        private void ActivateSelectedButton(DaggerfallTravelPopUp menuWindow)
        {
            switch (buttonSelected)
            {
                case CautiouslyButton:
                    ClickSpeed(menuWindow, true);
                    break;

                case RecklesslyButton:
                    ClickSpeed(menuWindow, false);
                    break;

                case FootHorseButton:
                    ClickTransport(menuWindow, false);
                    break;

                case ShipButton:
                    ClickTransport(menuWindow, true);
                    break;

                case InnsButton:
                    ClickSleep(menuWindow, true);
                    break;

                case CampOutButton:
                    ClickSleep(menuWindow, false);
                    break;

                case BeginButton:
                    SelectBegin(menuWindow);
                    break;

                case ExitButton:
                    SelectExit(menuWindow);
                    break;
            }
        }

        private void TryMoveSelector(DaggerfallTravelPopUp menuWindow, ControllerManager.StickDir8 dir)
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
        private Panel GetCurrentRenderPanel(DaggerfallTravelPopUp menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallTravelPopUp menuWindow)
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

        private void RefreshSelectorAttachment(DaggerfallTravelPopUp menuWindow)
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
            return top is DaggerfallTravelPopUp;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTravelPopUp menuWindow = top as DaggerfallTravelPopUp;

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
        private void OnTickOpen(DaggerfallTravelPopUp menuWindow, ControllerManager cm)
        {

            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            //if (selectorHost == null)
            //{
            //    RefreshSelectorToCurrentButton(menuWindow);
            //}
            //else
            //{
            //    Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            //    if (currentPanel != null)
            //        RefreshSelectorToCurrentButton(menuWindow);
            //}

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();


            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed ||
                cm.DPadLeftPressed || cm.DPadUpPressed || cm.DPadDownPressed || cm.DPadRightReleased);

            if (isAssisting)
            {
                if (cm.DPadLeftPressed)
                {
                    ToggleSpeed(menuWindow);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    return;
                }

                if (cm.DPadUpPressed)
                {
                    ToggleTransport(menuWindow);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    return;
                }

                if (cm.DPadDownPressed)
                {
                    ToggleSleepMode(menuWindow);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    return;
                }

                if (cm.DPadRightReleased)
                {
                    SelectBegin(menuWindow);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    return;
                }

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(menuWindow);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                }

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
        // Assist helpers
        // =========================

        private Button GetBeginButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiBeginButton != null ? fiBeginButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetExitButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiExitButton != null ? fiExitButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetCautiousButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiCautiousToggleButton != null ? fiCautiousToggleButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetRecklessButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiRecklessToggleButton != null ? fiRecklessToggleButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetFootHorseButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiFootHorseToggleButton != null ? fiFootHorseToggleButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetShipButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiShipToggleButton != null ? fiShipToggleButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetInnButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiInnToggleButton != null ? fiInnToggleButton.GetValue(menuWindow) as Button : null;
        }

        private Button GetCampOutButton(DaggerfallTravelPopUp menuWindow)
        {
            return fiCampOutToggleButton != null ? fiCampOutToggleButton.GetValue(menuWindow) as Button : null;
        }

        private void SelectBegin(DaggerfallTravelPopUp menuWindow)
        {
            if (miBeginButtonOnClickHandler != null)
                miBeginButtonOnClickHandler.Invoke(menuWindow, new object[] { GetBeginButton(menuWindow), Vector2.zero });
        }

        private void SelectExit(DaggerfallTravelPopUp menuWindow)
        {
            if (miExitButtonOnClickHandler != null)
                miExitButtonOnClickHandler.Invoke(menuWindow, new object[] { GetExitButton(menuWindow), Vector2.zero });
        }

        private void ToggleSpeed(DaggerfallTravelPopUp menuWindow)
        {
            if (miToggleSpeedButtonOnScrollHandler != null)
                miToggleSpeedButtonOnScrollHandler.Invoke(menuWindow, new object[] { GetCautiousButton(menuWindow) });
        }

        private void ToggleTransport(DaggerfallTravelPopUp menuWindow)
        {
            if (miToggleTransportModeButtonOnScrollHandler != null)
                miToggleTransportModeButtonOnScrollHandler.Invoke(menuWindow, new object[] { GetFootHorseButton(menuWindow) });
        }

        private void ToggleSleepMode(DaggerfallTravelPopUp menuWindow)
        {
            if (miToggleSleepModeButtonOnScrollHandler != null)
                miToggleSleepModeButtonOnScrollHandler.Invoke(menuWindow, new object[] { GetInnButton(menuWindow) });
        }
        private void ClickSpeed(DaggerfallTravelPopUp menuWindow, bool cautious)
        {
            if (miSpeedButtonOnClickHandler == null)
                return;

            var button = cautious ? GetCautiousButton(menuWindow) : GetRecklessButton(menuWindow);

            miSpeedButtonOnClickHandler.Invoke(menuWindow, new object[] { button, Vector2.zero });
        }
        private void ClickTransport(DaggerfallTravelPopUp menuWindow, bool ship)
        {
            if (miTransportModeButtonOnClickHandler == null)
                return;

            var button = ship ? GetShipButton(menuWindow) : GetFootHorseButton(menuWindow);

            miTransportModeButtonOnClickHandler.Invoke(menuWindow, new object[] { button, Vector2.zero });
        }
        private void ClickSleep(DaggerfallTravelPopUp menuWindow, bool inn)
        {
            if (miSleepModeButtonOnClickHandler == null)
                return;

            var button = inn ? GetInnButton(menuWindow) : GetCampOutButton(menuWindow);

            miSleepModeButtonOnClickHandler.Invoke(menuWindow, new object[] { button, Vector2.zero });
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallTravelPopUp menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);

        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTravelPopUp closed");
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallTravelPopUp menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiBeginButton = CacheField(type, "beginButton");
            fiExitButton = CacheField(type, "exitButton");
            fiCautiousToggleButton = CacheField(type, "cautiousToggleButton");
            fiRecklessToggleButton = CacheField(type, "recklessToggleButton");
            fiFootHorseToggleButton = CacheField(type, "footHorseToggleButton");
            fiShipToggleButton = CacheField(type, "shipToggleButton");
            fiInnToggleButton = CacheField(type, "innToggleButton");
            fiCampOutToggleButton = CacheField(type, "campOutToggleButton");

            miBeginButtonOnClickHandler = CacheMethod(type, "BeginButtonOnClickHandler");
            miExitButtonOnClickHandler = CacheMethod(type, "ExitButtonOnClickHandler");
            miToggleSpeedButtonOnScrollHandler = CacheMethod(type, "ToggleSpeedButtonOnScrollHandler");
            miToggleTransportModeButtonOnScrollHandler = CacheMethod(type, "ToggleTransportModeButtonOnScrollHandler");
            miToggleSleepModeButtonOnScrollHandler = CacheMethod(type, "ToggleSleepModeButtonOnScrollHandler");
            miSpeedButtonOnClickHandler = CacheMethod(type, "SpeedButtonOnClickHandler");
            miTransportModeButtonOnClickHandler = CacheMethod(type, "TransportModeButtonOnClickHandler");
            miSleepModeButtonOnClickHandler = CacheMethod(type, "SleepModeButtonOnClickHandler");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallTravelPopUp menuWindow, ControllerManager cm)
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
                    new LegendOverlay.LegendRow("D-Pad Left", "toggle Speed"),
                    new LegendOverlay.LegendRow("D-Pad Up", "toggle Transport"),
                    new LegendOverlay.LegendRow("D-Pad Down", "toggle Stops"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Begin"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "activate selection"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "change tone"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTravelPopUp menuWindow)
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

    }
}