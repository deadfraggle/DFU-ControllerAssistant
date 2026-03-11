using System.Collections.Generic;
using DaggerfallWorkshop.Game;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class FavoriteLocation
    {
        public string LocationName;
        public string RegionName;

        public FavoriteLocation(string location, string region)
        {
            LocationName = location;
            RegionName = region;
        }

        public override string ToString()
        {
            return $"{LocationName} ({RegionName})";
        }
    }

    public static class FavoritesStore
    {
        public static List<FavoriteLocation> Favorites = new List<FavoriteLocation>();

        public static void AddCurrentLocation()
        {
            var playerGPS = GameManager.Instance.PlayerGPS;

            if (playerGPS == null || !playerGPS.HasCurrentLocation)
                return;

            string location = playerGPS.CurrentLocation.Name;
            string region = playerGPS.CurrentRegionName;

            Favorites.Add(new FavoriteLocation(location, region));
        }
    }
}