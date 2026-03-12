using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class ControllerAssistantFavoritesWindow : DaggerfallPopupWindow
    {
        private Panel mainPanel = new Panel();
        private ListBox favoritesList = new ListBox();
        private TextLabel titleLabel = new TextLabel();
        private TextLabel regionLabel = new TextLabel();

        private List<string> regionNames = new List<string>();
        private int currentRegionIndex = 0;
        private int currentSelectionIndex = 0;

        public ControllerAssistantFavoritesWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
            ParentPanel.BackgroundColor = Color.clear;
            AllowCancel = true;
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;

            base.Setup();

            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.Position = new Vector2(0, 0);
            mainPanel.Size = new Vector2(180, 90);
            mainPanel.BackgroundColor = new Color(0f, 0f, 0f, 0.9f);
            Texture2D parchment = DaggerfallUI.GetTextureFromImg("PARCH03I0.IMG", 0, false);

            mainPanel.BackgroundTexture = parchment;
            mainPanel.BackgroundTextureLayout = BackgroundLayout.StretchToFill;

            NativePanel.Components.Add(mainPanel);

            titleLabel.Text = "Favorites";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.Position = new Vector2(0, 4);
            mainPanel.Components.Add(titleLabel);

            regionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            regionLabel.Position = new Vector2(0, 12);
            mainPanel.Components.Add(regionLabel);

            Panel divider = new Panel();
            divider.Position = new Vector2(8, 18);
            divider.Size = new Vector2(164, 2);
            divider.BackgroundColor = DaggerfallUI.DaggerfallDefaultTextColor;
            mainPanel.Components.Add(divider);

            favoritesList.Position = new Vector2(8, 22);
            favoritesList.Size = new Vector2(164, 58);
            favoritesList.RowsDisplayed = 8;
            mainPanel.Components.Add(favoritesList);

            RebuildRegions();
            SetInitialRegionFromPlayerLocation();
            RefreshList();
        }

        public override void Update()
        {
            base.Update();

            if (InputManager.Instance.GetBackButtonDown())
                CloseWindow();
        }

        public void MoveSelectionUp()
        {
            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();
            if (regionFavorites.Count == 0)
                return;

            favoritesList.SelectPrevious();
            currentSelectionIndex = favoritesList.SelectedIndex;
        }

        public void MoveSelectionDown()
        {
            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();
            if (regionFavorites.Count == 0)
                return;

            favoritesList.SelectNext();
            currentSelectionIndex = favoritesList.SelectedIndex;
        }

        public void PreviousRegion()
        {
            if (regionNames.Count == 0)
                return;

            currentRegionIndex--;
            if (currentRegionIndex < 0)
                currentRegionIndex = regionNames.Count - 1;

            currentSelectionIndex = 0;
            RefreshList();
        }

        public void NextRegion()
        {
            if (regionNames.Count == 0)
                return;

            currentRegionIndex++;
            if (currentRegionIndex >= regionNames.Count)
                currentRegionIndex = 0;

            currentSelectionIndex = 0;
            RefreshList();
        }

        public string GetSelectedLocationName()
        {
            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();
            if (regionFavorites.Count == 0)
                return null;

            if (currentSelectionIndex < 0 || currentSelectionIndex >= regionFavorites.Count)
                return null;

            return regionFavorites[currentSelectionIndex].LocationName;
        }

        public string GetCurrentRegionName()
        {
            if (regionNames.Count == 0)
                return null;

            if (currentRegionIndex < 0 || currentRegionIndex >= regionNames.Count)
                return null;

            return regionNames[currentRegionIndex];
        }

        public bool DeleteSelectedFavorite()
        {
            string locationName = GetSelectedLocationName();
            string regionName = GetCurrentRegionName();

            if (string.IsNullOrEmpty(locationName) || string.IsNullOrEmpty(regionName))
                return false;

            bool removed = FavoritesStore.RemoveFavorite(locationName, regionName);
            if (!removed)
                return false;

            RefreshList();
            return true;
        }

        public bool MoveSelectedFavoriteUp()
        {
            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();
            if (regionFavorites.Count <= 1)
                return false;

            if (currentSelectionIndex <= 0 || currentSelectionIndex >= regionFavorites.Count)
                return false;

            string regionName = GetCurrentRegionName();
            if (string.IsNullOrEmpty(regionName))
                return false;

            bool moved = FavoritesStore.SwapFavoritesInRegion(regionName, currentSelectionIndex, currentSelectionIndex - 1);
            if (!moved)
                return false;

            currentSelectionIndex--;
            RefreshList();
            return true;
        }

        public bool MoveSelectedFavoriteDown()
        {
            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();
            if (regionFavorites.Count <= 1)
                return false;

            if (currentSelectionIndex < 0 || currentSelectionIndex >= regionFavorites.Count - 1)
                return false;

            string regionName = GetCurrentRegionName();
            if (string.IsNullOrEmpty(regionName))
                return false;

            bool moved = FavoritesStore.SwapFavoritesInRegion(regionName, currentSelectionIndex, currentSelectionIndex + 1);
            if (!moved)
                return false;

            currentSelectionIndex++;
            RefreshList();
            return true;
        }

        private void RebuildRegions()
        {
            regionNames.Clear();

            List<FavoriteLocation> favorites = FavoritesStore.Favorites;
            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation fav = favorites[i];
                if (fav == null || string.IsNullOrEmpty(fav.RegionName))
                    continue;

                if (!regionNames.Contains(fav.RegionName))
                    regionNames.Add(fav.RegionName);
            }

            if (regionNames.Count == 0)
            {
                currentRegionIndex = 0;
                currentSelectionIndex = 0;
                return;
            }

            if (currentRegionIndex < 0)
                currentRegionIndex = 0;

            if (currentRegionIndex >= regionNames.Count)
                currentRegionIndex = regionNames.Count - 1;
        }

        private void SetInitialRegionFromPlayerLocation()
        {
            if (regionNames.Count == 0)
                return;

            var gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
                return;

            int currentRegionIndexFromPlayer = gps.CurrentRegionIndex;

            for (int i = 0; i < FavoritesStore.Favorites.Count; i++)
            {
                FavoriteLocation fav = FavoritesStore.Favorites[i];
                if (fav == null)
                    continue;

                if (fav.RegionIndex == currentRegionIndexFromPlayer)
                {
                    for (int r = 0; r < regionNames.Count; r++)
                    {
                        if (regionNames[r] == fav.RegionName)
                        {
                            currentRegionIndex = r;
                            currentSelectionIndex = 0;
                            return;
                        }
                    }
                }
            }
        }

        private List<FavoriteLocation> GetCurrentRegionFavorites()
        {
            List<FavoriteLocation> results = new List<FavoriteLocation>();

            if (regionNames.Count == 0)
                return results;

            string regionName = regionNames[currentRegionIndex];
            List<FavoriteLocation> favorites = FavoritesStore.Favorites;

            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation fav = favorites[i];
                if (fav != null && fav.RegionName == regionName)
                    results.Add(fav);
            }

            return results;
        }

        private void EnsureSelectionVisible()
        {
            if (favoritesList.Count == 0)
                return;

            int top = favoritesList.ScrollIndex;
            int bottom = top + favoritesList.RowsDisplayed - 1;

            if (currentSelectionIndex < top)
            {
                favoritesList.ScrollIndex = currentSelectionIndex;
            }
            else if (currentSelectionIndex > bottom)
            {
                favoritesList.ScrollIndex = currentSelectionIndex - favoritesList.RowsDisplayed + 1;
            }
        }

        private void RefreshList()
        {
            favoritesList.ClearItems();
            RebuildRegions();

            // Skip empty regions automatically
            while (regionNames.Count > 0 && GetCurrentRegionFavorites().Count == 0)
            {
                regionNames.RemoveAt(currentRegionIndex);

                if (regionNames.Count == 0)
                {
                    currentRegionIndex = 0;
                    currentSelectionIndex = 0;
                    break;
                }

                if (currentRegionIndex >= regionNames.Count)
                    currentRegionIndex = regionNames.Count - 1;
            }

            if (regionNames.Count == 0)
            {
                regionLabel.Text = string.Empty;
                favoritesList.AddItem("[No favorites yet]");
                favoritesList.SelectedIndex = 0;
                return;
            }

            string currentRegionName = GetCurrentRegionName();
            regionLabel.Text = $"Region: {currentRegionName}";


            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();

            if (currentSelectionIndex < 0)
                currentSelectionIndex = 0;

            if (currentSelectionIndex >= regionFavorites.Count)
                currentSelectionIndex = regionFavorites.Count - 1;

            for (int i = 0; i < regionFavorites.Count; i++)
                favoritesList.AddItem(regionFavorites[i].LocationName);

            favoritesList.SelectedIndex = currentSelectionIndex;
            EnsureSelectionVisible();
        }
    }
}