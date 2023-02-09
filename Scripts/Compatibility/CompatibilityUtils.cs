using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Linq;

namespace Monobelisk.Compatibility
{
    public static class CompatibilityUtils
    {
        internal const string BASIC_ROADS = "BasicRoads";

        private static string[] _loadedMods;
        private static string[] LoadedMods
        {
            get
            {
                if (_loadedMods == null)
                {
                    _loadedMods = ModManager.Instance.GetAllModTitles();
                }

                return _loadedMods;
            }
        }

        public static bool BasicRoadsLoaded =>
            LoadedMods.Contains(BASIC_ROADS);
    }
}