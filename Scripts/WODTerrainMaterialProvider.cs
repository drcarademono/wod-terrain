using System;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop;

namespace WODTerrain
{
    public abstract class WODTerrainMaterialProvider : ITerrainMaterialProvider
    {

        public enum Climates
        {
            Ocean = 223,
            Desert = 224,
            Desert2 = 225, // seen in dak'fron
            Mountain = 226,
            Rainforest = 227,
            Swamp = 228,
            Subtropical = 229,
            MountainWoods = 230,
            Woodlands = 231,
            HauntedWoodlands = 232 // not sure where this is?
        }

        public abstract Material CreateMaterial();

        public abstract void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData);

        /// <summary>
        /// Parses climate informations and retrieves ground archive index.
        /// </summary>
        /// <param name="worldClimate">Index of world climate.</param>
        /// <returns>Texture archive index.</returns>
        protected int GetGroundArchive(int worldClimate)
        {
            return GetClimateInfo(worldClimate).GroundArchive;
        }

        /// <summary>
        /// Parses climate informations.
        /// </summary>
        /// <param name="worldClimate">Index of world climate.</param>
        /// <returns>Parsed climate informations.</returns>
        protected virtual (int GroundArchive, DFLocation.ClimateSettings Settings, bool IsWinter) GetClimateInfo(int worldClimate)
        {
            // Get current climate and ground archive using the provided method
            DFLocation.ClimateSettings climate = MapsFile.GetWorldClimateSettings(worldClimate);
            int groundArchive = climate.GroundArchive;
            bool isWinter = false;

            // Check for winter season
            if (climate.ClimateType != DFLocation.ClimateBaseType.Desert &&
                DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
            {
                // Offset to snow textures
                groundArchive++;
                isWinter = true;
            }

            // Special handling for Subtropical climate
            if (worldClimate == (int)Climates.Subtropical)
            {
                // Set the ground archive to 4 specifically for Subtropical climates
                groundArchive = 4;
            }

            return (groundArchive, climate, isWinter);
        }
    }

    public class WODTilemapTerrainMaterialProvider : WODTerrainMaterialProvider
    {
        private readonly Shader shader = Shader.Find(MaterialReader._DaggerfallTilemapShaderName);

        public override Material CreateMaterial()
        {
            return new Material(shader);
        }

        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData)
        {
            Material tileSetMaterial = DaggerfallUnity.Instance.MaterialReader.GetTerrainTilesetMaterial(GetGroundArchive(terrainMaterialData.WorldClimate));
            terrainMaterialData.Material.SetTexture(TileUniforms.TileAtlasTex, tileSetMaterial.GetTexture(TileUniforms.TileAtlasTex));
            terrainMaterialData.Material.SetTexture(TileUniforms.TilemapTex, terrainMaterialData.TileMapTexture);
            terrainMaterialData.Material.SetInt(TileUniforms.TilemapDim, MapsFile.WorldMapTileDim);
        }
    }

    public class WODTilemapTextureArrayTerrainMaterialProvider : WODTerrainMaterialProvider
    {
        private readonly Shader shader = Shader.Find(MaterialReader._DaggerfallTilemapTextureArrayShaderName);

        internal static bool IsSupported => SystemInfo.supports2DArrayTextures && DaggerfallUnity.Settings.EnableTextureArrays;

        public override Material CreateMaterial()
        {
            return new Material(shader);
        }

        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData)
        {
            Material tileMaterial = DaggerfallUnity.Instance.MaterialReader.GetTerrainTextureArrayMaterial(GetGroundArchive(terrainMaterialData.WorldClimate));

            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileNormalMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileNormalMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileParallaxMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileParallaxMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileMetallicGlossMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileMetallicGlossMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TilemapTex, terrainMaterialData.TileMapTexture);

            AssignKeyword("NORMAL_MAP", tileMaterial, terrainMaterialData.Material);
            AssignKeyword("HEIGHT_MAP", tileMaterial, terrainMaterialData.Material);
            AssignKeyword("METALLIC_GLOSS_MAP", tileMaterial, terrainMaterialData.Material);
        }

        private void AssignKeyword(string keyword, Material src, Material dst)
        {
            if (src.IsKeywordEnabled(keyword)) dst.EnableKeyword(keyword);
            else dst.DisableKeyword(keyword);
        }
    }
}

