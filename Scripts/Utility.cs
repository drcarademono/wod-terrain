using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using System.Linq;
using UnityEngine;

namespace Monobelisk
{
    public static class Utility
    {
        public static Vector2 GetTerrainVertexPosition(int mapPixelX, int mapPixelY)
        {
            var max = GetWorldPixelSize();

            return new Vector2(mapPixelX, max.y - mapPixelY) * GetTerrainVertexSize();
        }

        public static Vector2 GetTerrainUnitPosition(int mapPixelX, int mapPixelY)
        {
            var max = GetWorldPixelSize();

            return new Vector2(mapPixelX, max.y - mapPixelY) * GetTerrainUnitSize();
        }

        public static float GetTerrainVertexSize()
        {
            return 129f;
        }

        public static float GetTerrainUnitSize()
        {
            return MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;
        }

        public static Vector2 GetWorldVertexSize()
        {
            var max = GetWorldPixelSize();

            return new Vector2(max.x, max.y) * GetTerrainVertexSize();
        }

        public static Vector2 GetWorldUnitSize()
        {
            var max = GetWorldPixelSize();

            return new Vector2(max.x, max.y) * GetTerrainUnitSize();
        }

        public static Vector2Int GetWorldPixelSize()
        {
            int maxX = 999;
            int maxY = 499;

            return new Vector2Int(maxX, maxY);
        }

        public static float OneMeterInPixel()
        {
            return 1f / GetTerrainUnitSize();
        }

        public static float GetOriginalTerrainHeight()
        {
            DaggerfallUnity dfUnity;
            DaggerfallUnity.FindDaggerfallUnity(out dfUnity);

            return dfUnity.TerrainSampler.MaxTerrainHeight;
        }

        public static byte[] ToBytes(float[] floats)
        {
            return floats.Select(f =>
            {
                return (byte)(uint)(f * 255f);
            }).ToArray();
        }
    }
}