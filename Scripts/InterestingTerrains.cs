using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Monobelisk.Compatibility;

namespace Monobelisk
{
    public class InterestingTerrains : MonoBehaviour
    {
        public static readonly TileDataCache tileDataCache = new TileDataCache();

        public static InterestingTerrains instance;
        public static Mod Mod { get; private set; }

        public static Settings settings = new Settings();
        public TerrainComputerParams csParams;
        public static Texture2D biomeMap;
        public static Texture2D derivMap;
        public static Texture2D portMap;
        public static Texture2D roadMap;
        public static Texture2D tileableNoise;
        public static ComputeShader csPrototype;
        public static ComputeShader mainHeightComputer;
        public static ComputeShader interpolateHeightmapShader;

        public static int terrainStep = 4;

        #region Invoke
        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            Mod = initParams.Mod;
            var go = new GameObject(Mod.Title);
            instance = go.AddComponent<InterestingTerrains>();

            GameManager.Instance.StreamingWorld.TerrainScale = 1f;

            LoadAssetsAndParams();

            ModMessageHandler.Init();

            ConsoleHandler.RegisterConsoleCommands();
        }

        private static void LoadAssetsAndParams()
        {
            Mod.LoadAllAssetsFromBundle();

            biomeMap = Mod.GetAsset<Texture2D>("daggerfall_heightmap");
            derivMap = Mod.GetAsset<Texture2D>("daggerfall_deriv_map");
            portMap = Mod.GetAsset<Texture2D>("daggerfall_port_map");
            roadMap = Mod.GetAsset<Texture2D>("daggerfall_road_map");
            tileableNoise = Mod.GetAsset<Texture2D>("tileable_noise");
            csPrototype = Mod.GetAsset<ComputeShader>("TerrainComputer");
            mainHeightComputer = Mod.GetAsset<ComputeShader>("MainHeightmapComputer");
            interpolateHeightmapShader = Mod.GetAsset<ComputeShader>("InterpolateHeightmap");

#if UNITY_EDITOR
            instance.csParams = ScriptableObject.CreateInstance<TerrainComputerParams>();
#else
            instance.csParams = new TerrainComputerParams();
#endif

            var paramIni = Mod.GetAsset<TextAsset>("interesting_terrains");
            var ini = new IniParser.Parser.IniDataParser().Parse(paramIni.text);
            instance.csParams.FromIniData(ini);

            TerrainComputer.InitializeWoodsFileHeightmap();
        }
        #endregion

        private void Awake()
        {
            DaggerfallUnity.Instance.TerrainSampler = new InterestingTerrainSampler();

            //DaggerfallUnity.Instance.TerrainTexturing = new WOTerrainTexturing();

            DaggerfallTerrain.OnPromoteTerrainData += tileDataCache.UncacheTileData;

            Mod.IsReady = true;
            Camera.main.farClipPlane = 10000f;
        }

        private void Start()
        {
            if (CompatibilityUtils.BasicRoadsLoaded)
                BasicRoadsUtils.Init();

            //DaggerfallUnity.Instance.TerrainTexturing = new WildernessOverhaul.WOTerrainTexturing(true, true);
        }

        private void OnDestroy()
        {
            TerrainComputer.Cleanup();
        }

        public IEnumerator ClearNoonRoutine()
        {
            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.Hour = 12;
            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.Minute = 0;
            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.Second = 0;
            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.Day += 1;

            yield return new WaitForSeconds(0.1f);

            Wenzil.Console.Console.ExecuteCommand("set_weather", "0");
            Wenzil.Console.Console.ExecuteCommand("killall");
        }
    }
}

