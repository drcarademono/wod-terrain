using System;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop;

namespace WODTerrain
{
    public readonly struct TerrainMaterialData
    {
        public Material Material { get; }
        public TerrainData TerrainData { get; }
        public Texture2D TileMapTexture { get; }
        public int WorldClimate { get; }

        internal TerrainMaterialData(Material material, TerrainData terrainData, Texture2D tileMapTexture, int worldClimate)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (terrainData == null) throw new ArgumentNullException(nameof(terrainData));
            if (tileMapTexture == null) throw new ArgumentNullException(nameof(tileMapTexture));

            Material = material;
            TerrainData = terrainData;
            TileMapTexture = tileMapTexture;
            WorldClimate = worldClimate;
        }
    }

    public interface ITerrainMaterialProvider
    {
        Material CreateMaterial();
        void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData);
    }

    public abstract class WODTerrainMaterialProvider : ITerrainMaterialProvider
    {
        /// <summary>
        /// Gets default implementation supported on current system.
        /// </summary>
        internal static WODTerrainMaterialProvider Default
        {
            get
            {
                if (WODTilemapTextureArrayTerrainMaterialProvider.IsSupported)
                    return new WODTilemapTextureArrayTerrainMaterialProvider();
                else
                    return new WODTilemapTerrainMaterialProvider();
            }
        }

        public abstract Material CreateMaterial();
        public abstract void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData);

        protected int GetGroundArchive(int worldClimate)
        {
            return 302; // Directly return 302, ignoring the world climate parameter
        }

        protected virtual (int GroundArchive, DFLocation.ClimateSettings Settings, bool IsWinter) GetClimateInfo(int worldClimate)
        {
            DFLocation.ClimateSettings climate = MapsFile.GetWorldClimateSettings(worldClimate);
            int groundArchive = 302; // Set groundArchive to always be 302
            bool isWinter = false;

            // The original logic for determining if it's winter is kept,
            // but it's no longer used to adjust the groundArchive value.
            // You can remove this if winter detection is not needed for other reasons.
            if (climate.ClimateType != DFLocation.ClimateBaseType.Desert && DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
            {
                isWinter = true;
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

            // Assign keywords based on the source material to the destination material
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

