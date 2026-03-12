using System;
using System.Collections.Generic;
using FullSerializer;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace gigantibyte.DFU.ControllerAssistant
{
    [fsObject("v1")]
    public class FavoriteLocation
    {
        public string LocationName;
        public string RegionName;
        public int RegionIndex;

        public FavoriteLocation() { }

        public FavoriteLocation(string locationName, string regionName, int regionIndex)
        {
            LocationName = locationName;
            RegionName = regionName;
            RegionIndex = regionIndex;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", LocationName, RegionName);
        }
    }

    [fsObject("v1")]
    public class ControllerAssistantSaveData
    {
        public List<FavoriteLocation> Favorites = new List<FavoriteLocation>();
    }

    public enum AddFavoriteResult
    {
        Added,
        Duplicate,
        NotInLocation,
        AtLimit
    }

    public class FavoritesStore : IHasModSaveData
    {
        private const int MaxFavorites = 100;

        private static FavoritesStore instance;
        private ControllerAssistantSaveData saveData = new ControllerAssistantSaveData();

        public static FavoritesStore Instance
        {
            get
            {
                if (instance == null)
                    instance = new FavoritesStore();

                return instance;
            }
        }

        public static List<FavoriteLocation> Favorites
        {
            get { return Instance.saveData.Favorites; }
        }

        public Type SaveDataType
        {
            get { return typeof(ControllerAssistantSaveData); }
        }

        public object NewSaveData()
        {
            saveData = new ControllerAssistantSaveData();
            return saveData;
        }

        public object GetSaveData()
        {
            if (saveData == null)
                saveData = new ControllerAssistantSaveData();

            if (saveData.Favorites == null)
                saveData.Favorites = new List<FavoriteLocation>();

            return saveData;
        }

        public void RestoreSaveData(object loadedData)
        {
            saveData = loadedData as ControllerAssistantSaveData;

            if (saveData == null)
                saveData = new ControllerAssistantSaveData();

            if (saveData.Favorites == null)
                saveData.Favorites = new List<FavoriteLocation>();
        }

        public static AddFavoriteResult AddCurrentLocation()
        {
            var gps = GameManager.Instance.PlayerGPS;

            if (gps == null || !gps.HasCurrentLocation)
                return AddFavoriteResult.NotInLocation;

            string locationName = gps.CurrentLocation.Name;
            string regionName = gps.CurrentRegionName;
            int regionIndex = gps.CurrentRegionIndex;

            if (string.IsNullOrEmpty(locationName) || string.IsNullOrEmpty(regionName))
                return AddFavoriteResult.NotInLocation;

            List<FavoriteLocation> favorites = Favorites;

            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation fav = favorites[i];
                if (fav != null &&
                    fav.LocationName == locationName &&
                    fav.RegionName == regionName)
                {
                    return AddFavoriteResult.Duplicate;
                }
            }

            if (favorites.Count >= MaxFavorites)
                return AddFavoriteResult.AtLimit;

            favorites.Add(new FavoriteLocation(locationName, regionName, regionIndex));
            return AddFavoriteResult.Added;
        }

        public static bool RemoveFavorite(string locationName, string regionName)
        {
            List<FavoriteLocation> favorites = Favorites;

            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation fav = favorites[i];
                if (fav != null &&
                    fav.LocationName == locationName &&
                    fav.RegionName == regionName)
                {
                    favorites.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static bool SwapFavoritesInRegion(string regionName, int indexA, int indexB)
        {
            if (indexA == indexB)
                return false;

            List<int> regionIndices = new List<int>();
            List<FavoriteLocation> favorites = Favorites;

            for (int i = 0; i < favorites.Count; i++)
            {
                FavoriteLocation fav = favorites[i];
                if (fav != null && fav.RegionName == regionName)
                    regionIndices.Add(i);
            }

            if (indexA < 0 || indexA >= regionIndices.Count || indexB < 0 || indexB >= regionIndices.Count)
                return false;

            int globalA = regionIndices[indexA];
            int globalB = regionIndices[indexB];

            FavoriteLocation temp = favorites[globalA];
            favorites[globalA] = favorites[globalB];
            favorites[globalB] = temp;
            return true;
        }
    }
}