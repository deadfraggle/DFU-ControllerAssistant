using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TransportAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
        private bool wasOpen = false;
        private float inputCooldownTimer = 0f;

        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Window close binding
        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        //private AnchorEditor editor;

        // Button & selector setup

        private DefaultSelectorBoxHost selectorHost;
        private TransportQuickButtonOverlay quickButtonOverlay;
        private Texture2D quickButtonsAtlas;

        const int FootButton = 0;
        const int HorseButton = 1;
        const int CartButton = 2;
        const int ShipButton = 3;
        const int ExitButton = 4;
        const int AddFavoriteButton = 5;
        const int ViewFavoritesButton = 6;


        public class ButtonInfoArray
        {
            public Rect rect;
            public int N = -1;
            public int S = -1;
        }

        public ButtonInfoArray[] menuButton = new ButtonInfoArray[]
        {
            new ButtonInfoArray { rect = new Rect(99.4f, 74.3f, 121.4f, 8.5f), N = ViewFavoritesButton, S = HorseButton },    // FootButton
            new ButtonInfoArray { rect = new Rect(99.4f, 83.2f, 121.4f, 8.5f), N = FootButton, S = CartButton },              // HorseButton
            new ButtonInfoArray { rect = new Rect(99.4f, 92.3f, 121.4f, 8.5f), N = HorseButton, S = ShipButton },             // CartButton
            new ButtonInfoArray { rect = new Rect(99.4f, 101.2f, 121.4f, 8.5f), N = CartButton, S = ExitButton },             // ShipButton
            new ButtonInfoArray { rect = new Rect(142.2f, 114.7f, 37.5f, 9.3f), N = ShipButton, S = AddFavoriteButton },      // ExitButton
            new ButtonInfoArray { rect = new Rect(125.0f, 136.8f, 70.0f, 10.0f), N = ExitButton, S = ViewFavoritesButton },   // AddFavoriteButton
            new ButtonInfoArray { rect = new Rect(125.0f, 148.0f, 70.0f, 10.0f), N = AddFavoriteButton, S = FootButton },     // ViewFavoritesButton
        };

        public int buttonSelected = FootButton;

        private void ActivateSelectedButton(DaggerfallTransportWindow menuWindow)
        {
            if (buttonSelected == FootButton) SelectFoot(menuWindow);
            else if (buttonSelected == HorseButton) SelectHorse(menuWindow);
            else if (buttonSelected == CartButton) SelectCart(menuWindow);
            else if (buttonSelected == ShipButton) SelectShip(menuWindow);
            else if (buttonSelected == ExitButton) SelectExit(menuWindow);
            else if (buttonSelected == AddFavoriteButton) AddCurrentLocationToFavorites(menuWindow);
            else if (buttonSelected == ViewFavoritesButton) OpenFavoritesWindow(menuWindow);
        }

        private bool IsButtonEnabled(int buttonIndex)
        {
            switch (buttonIndex)
            {
                case FootButton:
                case ExitButton:
                case AddFavoriteButton:
                case ViewFavoritesButton:
                    return true;

                case HorseButton:
                    return GameManager.Instance != null &&
                           GameManager.Instance.TransportManager != null &&
                           GameManager.Instance.TransportManager.HasHorse();

                case CartButton:
                    return GameManager.Instance != null &&
                           GameManager.Instance.TransportManager != null &&
                           GameManager.Instance.TransportManager.HasCart();

                case ShipButton:
                    return GameManager.Instance != null &&
                           GameManager.Instance.TransportManager != null &&
                           GameManager.Instance.TransportManager.ShipAvailiable();

                default:
                    return false;
            }
        }

        private int FindNextEnabledButtonInDirection(int startButton, ControllerManager.StickDir8 dir)
        {
            int current = startButton;

            // Prevent infinite loops in a malformed graph
            HashSet<int> visited = new HashSet<int>();
            visited.Add(current);

            while (true)
            {
                int next = -1;
                var btn = menuButton[current];

                switch (dir)
                {
                    case ControllerManager.StickDir8.N:
                        next = btn.N;
                        break;

                    case ControllerManager.StickDir8.S:
                        next = btn.S;
                        break;

                    default:
                        return -1;
                }

                if (next < 0)
                    return -1;

                if (visited.Contains(next))
                    return -1;

                if (IsButtonEnabled(next))
                    return next;

                visited.Add(next);
                current = next;
            }
        }

        private void TryMoveSelector(DaggerfallTransportWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            int next = FindNextEnabledButtonInDirection(buttonSelected, dir);

            if (next > -1)
                buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }
        private Panel GetCurrentRenderPanel(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallTransportWindow menuWindow)
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

        private void RefreshSelectorAttachment(DaggerfallTransportWindow menuWindow)
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

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallTransportWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallTransportWindow menuWindow = top as DaggerfallTransportWindow;

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

        private void OnTickOpen(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.Transport);

            RefreshLegendAttachment(menuWindow);
            EnsureQuickButtonOverlay(menuWindow);
            if (quickButtonOverlay != null && quickButtonOverlay.IsBuilt)
                quickButtonOverlay.SetLayout();
            RefreshSelectorAttachment(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            // Suppress vanilla "same key closes window" while assist input is active
            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
        :       cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            bool isAssisting =
                (cm.DPadUpReleased ||
                 cm.DPadRightReleased ||
                 cm.DPadDownReleased ||
                 cm.DPadLeftReleased ||
                 cm.Action1Released ||
                 cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                        //editor.Toggle();
                }

                if (cm.Action1Released)
                    ActivateSelectedButton(menuWindow);

                if (cm.DPadUpReleased && IsButtonEnabled(FootButton))
                {
                    SelectFoot(menuWindow);
                    return;
                }

                if (cm.DPadRightReleased && IsButtonEnabled(HorseButton))
                {
                    SelectHorse(menuWindow);
                    return;
                }

                if (cm.DPadDownReleased && IsButtonEnabled(CartButton))
                {
                    SelectCart(menuWindow);
                    return;
                }

                if (cm.DPadLeftReleased && IsButtonEnabled(ShipButton))
                {
                    SelectShip(menuWindow);
                    return;
                }

            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                //menuWindow.CloseWindow();
                return;
            }

            // Preserve vanilla toggle-close behavior when player is not using assist controls
            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }
        }


        // Life cycle methods

        private void OnOpened(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            EnsureQuickButtonOverlay(menuWindow);
            RefreshSelectorToCurrentButton(menuWindow);

            //if (editor == null)
            //{
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallTransportWindow closed");
        }

        public void ResetState()
        {
            wasOpen = false;
            closeDeferred = false;

            DestroyLegend();
            DestroySelectorBox();
            DestroyQuickButtonOverlay();

            legendVisible = false;
            panelRenderWindow = null;
        }

        private void EnsureInitialized(DaggerfallTransportWindow menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void SelectFoot(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            StartInputCooldown(0.2f);
            GameManager.Instance.TransportManager.TransportMode = TransportModes.Foot;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectHorse(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.HasHorse())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Horse;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectCart(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.HasCart())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Cart;
            DestroyLegend();
            menuWindow.CloseWindow();
        }

        private void SelectShip(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null || !GameManager.Instance.TransportManager.ShipAvailiable())
                return;

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Ship;
            DestroyLegend();
            menuWindow.CloseWindow();
        }
        private void SelectExit(DaggerfallTransportWindow menuWindow)
        {
            DestroyLegend();
            menuWindow.CloseWindow();
            return;
        }
        private void AddCurrentLocationToFavorites(DaggerfallTransportWindow menuWindow)
        {
            AddFavoriteResult result = FavoritesStore.AddCurrentLocation();

            DestroyLegend();

            if (menuWindow != null)
                menuWindow.CloseWindow();

            switch (result)
            {
                case AddFavoriteResult.Added:
                    DaggerfallUI.AddHUDText("Location added to favorites");
                    return;

                case AddFavoriteResult.Duplicate:
                    DaggerfallUI.AddHUDText("Location is already in favorites");
                    return;

                case AddFavoriteResult.AtLimit:
                    DaggerfallUI.AddHUDText("Favorites list is full");
                    return;

                default:
                    DaggerfallUI.AddHUDText("No current location to add");
                    return;
            }
        }
        private void OpenFavoritesWindow(DaggerfallTransportWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            DestroyLegend();
            menuWindow.CloseWindow();

            ControllerAssistantFavoritesWindow favoritesWindow = new ControllerAssistantFavoritesWindow(DaggerfallUI.UIManager);
            DaggerfallUI.UIManager.PushWindow(favoritesWindow);
        }
        private Rect[] GetQuickButtonNativeRects()
        {
            return new Rect[]
            {
                new Rect(125.0f, 136.8f, 70.0f, 10.0f), // Add Location to Favorites
                new Rect(125.0f, 148.0f, 70.0f, 10.0f), // View Favorite Locations
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
                tex.filterMode = FilterMode.Point;
            }

            quickButtonsAtlas = tex;
            return quickButtonsAtlas;
        }

        private void EnsureQuickButtonOverlay(DaggerfallTransportWindow menuWindow)
        {
            Panel panel = GetCurrentRenderPanel(menuWindow);
            if (panel == null)
                return;

            Texture2D atlas = LoadQuickButtonsAtlas();
            if (atlas == null)
                return;

            if (quickButtonOverlay == null || !quickButtonOverlay.IsAttached())
            {
                quickButtonOverlay = new TransportQuickButtonOverlay(
                    panel,
                    GetQuickButtonNativeRects(),
                    atlas,
                    delegate { buttonSelected = AddFavoriteButton; AddCurrentLocationToFavorites(menuWindow); },
                    delegate { buttonSelected = ViewFavoritesButton; OpenFavoritesWindow(menuWindow); });
                quickButtonOverlay.Build();
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

        private void EnsureLegendUI(DaggerfallTransportWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>();

                rows.Add(new LegendOverlay.LegendRow("Right Stick", "Move Selector"));
                rows.Add(new LegendOverlay.LegendRow(cm.Action1Name, "Activate"));
                rows.Add(new LegendOverlay.LegendRow("DPad Up", "Foot"));
                rows.Add(new LegendOverlay.LegendRow("DPad Right", "Horse"));
                rows.Add(new LegendOverlay.LegendRow("DPad Down", "Cart"));
                rows.Add(new LegendOverlay.LegendRow("DPad Left", "Ship"));

                legend.Build("Transport", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallTransportWindow menuWindow)
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
                DestroyQuickButtonOverlay();
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

        public void StartInputCooldown(float duration)
        {
            inputCooldownTimer = Time.unscaledTime + duration;
        }

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }
        }

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