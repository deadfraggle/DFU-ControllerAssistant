using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallConnect.Arena2;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class CAFavoritesWindowAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
        private bool wasOpen = false;

        private bool reflectionCached = false;
        private ControllerAssistantFavoritesWindow subscribedWindow = null;
        private ControllerAssistantFavoritesWindow currentWindow = null;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is ControllerAssistantFavoritesWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            ControllerAssistantFavoritesWindow menuWindow = top as ControllerAssistantFavoritesWindow;

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

        private void OnTickOpen(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            bool isAssisting =
                (cm.DPadUpPressed ||
                 cm.DPadDownPressed ||
                 cm.DPadLeftPressed ||
                 cm.DPadRightPressed ||
                 cm.RStickUpPressed ||
                 cm.RStickDownPressed ||
                 cm.RStickUpHeldSlow ||
                 cm.RStickDownHeldSlow ||
                 cm.Action1Released ||
                 cm.Action2Pressed ||
                 cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
                    SelectPreviousLocation(menuWindow);

                if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
                    SelectNextLocation(menuWindow);

                if (cm.DPadLeftPressed)
                    PreviousRegion(menuWindow);

                if (cm.DPadRightPressed)
                    NextRegion(menuWindow);

                if (cm.DPadUpPressed)
                    MoveLocationUp(menuWindow);

                if (cm.DPadDownPressed)
                    MoveLocationDown(menuWindow);

                if (cm.Action1Released)
                    PromptTravelToSelectedLocation(menuWindow);

                if (cm.Action2Pressed)
                    DeleteSelectedLocation(menuWindow);

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
                menuWindow.CloseWindow();
                return;
            }
        }

        private void OnOpened(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            currentWindow = menuWindow;

            EnsureInitialized(menuWindow);
            if (subscribedWindow != menuWindow)
            {
                if (subscribedWindow != null)
                    subscribedWindow.OnLocationDoubleClick -= FavoritesWindow_OnLocationDoubleClick;

                menuWindow.OnLocationDoubleClick += FavoritesWindow_OnLocationDoubleClick;
                subscribedWindow = menuWindow;
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("ControllerAssistantFavoritesWindow closed");
        }

        public void ResetState()
        {
            wasOpen = false;

            DestroyLegend();

            legendVisible = false;
            panelRenderWindow = null;
            currentWindow = null;

            if (subscribedWindow != null)
            {
                subscribedWindow.OnLocationDoubleClick -= FavoritesWindow_OnLocationDoubleClick;
                subscribedWindow = null;
            }
        }

        private void EnsureInitialized(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (reflectionCached)
                return;

            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Assist action helpers
        // =========================

        private void SelectPreviousLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            menuWindow.MoveSelectionUp();
        }

        private void SelectNextLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            menuWindow.MoveSelectionDown();
        }

        private void PreviousRegion(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            menuWindow.PreviousRegion();
        }

        private void NextRegion(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            menuWindow.NextRegion();
        }

        private void MoveLocationUp(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            bool moved = menuWindow.MoveSelectedFavoriteUp();
            if (moved && debugMODE)
                DaggerfallUI.AddHUDText("Favorite moved up");
        }

        private void MoveLocationDown(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            bool moved = menuWindow.MoveSelectedFavoriteDown();
            if (moved && debugMODE)
                DaggerfallUI.AddHUDText("Favorite moved down");
        }

        private void DeleteSelectedLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            string locationName = menuWindow.GetSelectedLocationName();
            string regionName = menuWindow.GetCurrentRegionName();

            if (string.IsNullOrEmpty(locationName) || string.IsNullOrEmpty(regionName))
                return;

            DaggerfallMessageBox mb = new DaggerfallMessageBox(
                DaggerfallUI.UIManager,
                DaggerfallMessageBox.CommonMessageBoxButtons.YesNo,
                string.Format("Delete favorite?\n\n{0}\n({1})", locationName, regionName),
                menuWindow);

            mb.OnButtonClick += (sender, button) =>
            {
                DaggerfallMessageBox msgBox = sender as DaggerfallMessageBox;

                if (button == DaggerfallMessageBox.MessageBoxButtons.Yes)
                {
                    bool removed = menuWindow.DeleteSelectedFavorite();
                    if (removed)
                        DaggerfallUI.AddHUDText("Favorite deleted");
                }

                if (msgBox != null)
                    msgBox.CloseWindow();
            };

            mb.Show();
        }

        private FavoriteLocation GetSelectedFavorite(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return null;

            string locationName = menuWindow.GetSelectedLocationName();
            string regionName = menuWindow.GetCurrentRegionName();

            if (string.IsNullOrEmpty(locationName) || string.IsNullOrEmpty(regionName))
                return null;

            List<FavoriteLocation> favorites = FavoritesStore.Favorites;
            if (favorites == null)
                return null;

            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation favorite = favorites[i];
                if (favorite == null)
                    continue;

                if (string.Equals(favorite.LocationName, locationName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(favorite.RegionName, regionName, StringComparison.OrdinalIgnoreCase))
                {
                    return favorite;
                }
            }

            return null;
        }
        private void FavoritesWindow_OnLocationDoubleClick(BaseScreenComponent sender, Vector2 position)
        {
            if (currentWindow == null)
                return;

            PromptTravelToSelectedLocation(currentWindow);
        }

        private void PromptTravelToSelectedLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            FavoriteLocation favorite = GetSelectedFavorite(menuWindow);
            if (favorite == null)
                return;

            if (GameManager.Instance == null || GameManager.Instance.PlayerGPS == null)
                return;

            // Don't prompt if already there
            if (GameManager.Instance.PlayerGPS.HasCurrentLocation &&
                string.Equals(GameManager.Instance.PlayerGPS.CurrentLocation.Name, favorite.LocationName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GameManager.Instance.PlayerGPS.CurrentRegionName, favorite.RegionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DaggerfallTravelMapWindow travelMap = DaggerfallUI.Instance.DfTravelMapWindow;
            if (travelMap == null)
                return;


            string entryStr = string.Format(
                "{0} in {1} province",
                favorite.LocationName,
                TextManager.Instance.GetLocalizedRegionName(
                    MapsFile.PatchRegionIndex(favorite.RegionIndex, favorite.RegionName)));

            DaggerfallMessageBox dialogBox = CreateTravelDialogBox(entryStr, menuWindow);
            dialogBox.OnButtonClick += (sender, button) =>
            {
                DaggerfallMessageBox msgBox = sender as DaggerfallMessageBox;

                if (msgBox != null)
                    msgBox.CloseWindow();

                if (button == DaggerfallMessageBox.MessageBoxButtons.Yes)
                    OpenTravelMapToFavorite(menuWindow, favorite);
            };

            DaggerfallUI.UIManager.PushWindow(dialogBox);
        }

        private DaggerfallMessageBox CreateTravelDialogBox(string entryStr, ControllerAssistantFavoritesWindow menuWindow)
        {
            TextFile.Token[] tokens = new TextFile.Token[]
            {
        TextFile.CreateTextToken("Travel to location"),
        TextFile.CreateFormatToken(TextFile.Formatting.JustifyCenter),
        TextFile.NewLineToken,

        TextFile.CreateTextToken("Do you want to open the world map to travel to:"),
        TextFile.NewLineToken,
        TextFile.NewLineToken,

        new TextFile.Token() { text = entryStr, formatting = TextFile.Formatting.TextHighlight },
        TextFile.CreateFormatToken(TextFile.Formatting.JustifyCenter),
        TextFile.NewLineToken,

        TextFile.CreateTextToken("(Note: you can cancel travel from the world map)"),
        TextFile.CreateFormatToken(TextFile.Formatting.EndOfRecord),
            };

            DaggerfallMessageBox dialogBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, menuWindow);
            dialogBox.SetHighlightColor(Color.white);
            dialogBox.SetTextTokens(tokens);
            dialogBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            dialogBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
            return dialogBox;
        }

        private void OpenTravelMapToFavorite(ControllerAssistantFavoritesWindow menuWindow, FavoriteLocation favorite)
        {
            if (menuWindow == null || favorite == null)
                return;

            DaggerfallTravelMapWindow travelMap = DaggerfallUI.Instance.DfTravelMapWindow;
            if (travelMap == null)
                return;

            DestroyLegend();
            menuWindow.CloseWindow();

            DaggerfallUI.UIManager.PushWindow(travelMap);

            DaggerfallUnity.Instance.StartCoroutine(OpenTravelMapToFavoriteDeferred(travelMap, favorite));
        }
        private System.Collections.IEnumerator OpenTravelMapToFavoriteDeferred(
            DaggerfallTravelMapWindow travelMap,
            FavoriteLocation favorite)
        {
            // Let the travel map finish opening first.
            yield return null;

            if (travelMap == null || favorite == null)
                yield break;

            int patchedRegionIndex = MapsFile.PatchRegionIndex(favorite.RegionIndex, favorite.RegionName);

            try
            {
                Type travelMapType = travelMap.GetType();

                FieldInfo fiMouseOverRegion = travelMapType.GetField(
                    "mouseOverRegion",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                MethodInfo miOpenRegionPanel = travelMapType.GetMethod(
                    "OpenRegionPanel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                MethodInfo miUpdateRegionLabel = travelMapType.GetMethod(
                    "UpdateRegionLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                MethodInfo miHandleLocationFindEvent = travelMapType.GetMethod(
                    "HandleLocationFindEvent",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (fiMouseOverRegion != null)
                    fiMouseOverRegion.SetValue(travelMap, patchedRegionIndex);

                if (miOpenRegionPanel != null)
                    miOpenRegionPanel.Invoke(travelMap, new object[] { patchedRegionIndex });

                if (miUpdateRegionLabel != null)
                    miUpdateRegionLabel.Invoke(travelMap, null);

                if (miHandleLocationFindEvent != null)
                    miHandleLocationFindEvent.Invoke(travelMap, new object[] { null, favorite.LocationName });
            }
            catch (Exception ex)
            {
                Debug.Log("[ControllerAssistant] OpenTravelMapToFavoriteDeferred failed: " + ex);
            }
        }

        private void EnsureLegendUI(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Select location"),
                    new LegendOverlay.LegendRow("D-Pad Left/Right", "Change region"),
                    new LegendOverlay.LegendRow("D-Pad Up/Down", "Move location"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Travel"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Delete"),
                };

                legend.Build("Favorites", rows);
            }
        }

        private void RefreshLegendAttachment(ControllerAssistantFavoritesWindow menuWindow)
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