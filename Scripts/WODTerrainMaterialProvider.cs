using System;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop;

namespace WODTerrain
{
    public abstract class WODTerrainMaterialProvider : ITerrainMaterialProvider
    {
        public abstract Material CreateMaterial();

        // Ensure DaggerfallWorkshop.TerrainMaterialData is used to match the interface expectation.
        public abstract void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, DaggerfallWorkshop.TerrainMaterialData terrainMaterialData);

        protected int GetGroundArchive(int worldClimate)
        {
            return 302; // Always returns 302 for simplification
        }

        protected virtual (int GroundArchive, DFLocation.ClimateSettings Settings, bool IsWinter) GetClimateInfo(int worldClimate)
        {
            // Simplifies the method for always returning 302 for GroundArchive
            return (302, new DFLocation.ClimateSettings(), false);
        }
    }

    public class WODTilemapTerrainMaterialProvider : WODTerrainMaterialProvider
    {
        private readonly Shader shader = Shader.Find(MaterialReader._DaggerfallTilemapShaderName);

        public override Material CreateMaterial()
        {
            return new Material(shader);
        }

        // Use DaggerfallWorkshop.TerrainMaterialData for the method parameter
        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, DaggerfallWorkshop.TerrainMaterialData terrainMaterialData)
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

        // Use DaggerfallWorkshop.TerrainMaterialData for the method parameter
        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, DaggerfallWorkshop.TerrainMaterialData terrainMaterialData)
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

