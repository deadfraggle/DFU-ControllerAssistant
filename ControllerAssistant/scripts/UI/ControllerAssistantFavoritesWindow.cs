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

            NativePanel.Components.Add(mainPanel);

            titleLabel.Text = "Favorites";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.Position = new Vector2(0, 4);
            mainPanel.Components.Add(titleLabel);

            regionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            regionLabel.Position = new Vector2(0, 12);
            mainPanel.Components.Add(regionLabel);

            favoritesList.Position = new Vector2(8, 22);
            favoritesList.Size = new Vector2(164, 58);
            favoritesList.RowsDisplayed = 6;
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
            int count = GetCurrentRegionFavorites().Count;
            if (count == 0)
                return;

            currentSelectionIndex--;
            if (currentSelectionIndex < 0)
                currentSelectionIndex = 0;

            RefreshList();
        }

        public void MoveSelectionDown()
        {
            int count = GetCurrentRegionFavorites().Count;
            if (count == 0)
                return;

            currentSelectionIndex++;
            if (currentSelectionIndex >= count)
                currentSelectionIndex = count - 1;

            RefreshList();
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

        public string GetCurrentRegionName()
        {
            if (regionNames.Count == 0)
                return string.Empty;

            if (currentRegionIndex < 0 || currentRegionIndex >= regionNames.Count)
                return string.Empty;

            return regionNames[currentRegionIndex];
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
            var gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
                return;

            int playerRegion = gps.CurrentRegionIndex;

            for (int i = 0; i < regionNames.Count; i++)
            {
                foreach (var fav in FavoritesStore.Favorites)
                {
                    if (fav.RegionName == regionNames[i] && fav.RegionIndex == playerRegion)
                    {
                        currentRegionIndex = i;
                        currentSelectionIndex = 0;
                        return;
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

        private void RefreshList()
        {
            favoritesList.ClearItems();

            RebuildRegions();

            if (regionNames.Count == 0)
            {
                regionLabel.Text = string.Empty;
                favoritesList.AddItem("[No favorites yet]");
                favoritesList.SelectedIndex = 0;
                return;
            }

            regionLabel.Text = GetCurrentRegionName();

            List<FavoriteLocation> regionFavorites = GetCurrentRegionFavorites();

            if (regionFavorites.Count == 0)
            {
                currentSelectionIndex = 0;
                favoritesList.AddItem("[No favorites in this region]");
                favoritesList.SelectedIndex = 0;
                return;
            }

            if (currentSelectionIndex < 0)
                currentSelectionIndex = 0;

            if (currentSelectionIndex >= regionFavorites.Count)
                currentSelectionIndex = regionFavorites.Count - 1;

            for (int i = 0; i < regionFavorites.Count; i++)
            {
                favoritesList.AddItem(regionFavorites[i].LocationName);
            }

            favoritesList.SelectedIndex = currentSelectionIndex;
        }
    }
}