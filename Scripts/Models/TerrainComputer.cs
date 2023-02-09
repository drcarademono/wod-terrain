using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static DaggerfallWorkshop.StreamingWorld;

namespace Monobelisk
{

    public struct DoubleInt
    {
        public int Item1;
        public int Item2;
    }

    public struct TerrainComputer
    {
        public static byte[] originalHeightmapBuffer;
        public static byte[] alteredHeightmapBuffer;
        public static ComputeBuffer locationHeightData = new ComputeBuffer(289, sizeof(float) * 3);
        public static Texture2D baseHeightmap;

        public Vector2 terrainPosition;
        public Vector2 terrainSize;
        public int heightmapResolution;
        public Rect locationRect;
        public HeightmapBufferCollection heightmapBuffers;
        private InterestingTerrainSampler sampler;

        private static readonly Dictionary<DoubleInt, Rect> locationRectCache = new Dictionary<DoubleInt, Rect>();

        /// <summary>
        /// Create a TerrainComputer for a specific mapData/terrain instance.
        /// </summary>
        /// <param name="mapPixelData"></param>
        /// <param name="sampler"></param>
        /// <returns></returns>
        public static TerrainComputer Create(MapPixelData mapPixelData, InterestingTerrainSampler sampler)
        {
            var tSize = Utility.GetTerrainVertexSize();

            return new TerrainComputer()
            {
                sampler = sampler,
                heightmapBuffers = BufferIO.CreateHeightmapBuffers(),
                heightmapResolution = (int)InterestingTerrains.settings.heightmapResolution,
                locationRect = mapPixelData.hasLocation
                    ? mapPixelData.locationRect
                    : new Rect(-10, -10, 1, 1),
                terrainPosition = Utility.GetTerrainVertexPosition(mapPixelData.mapPixelX, mapPixelData.mapPixelY),
                terrainSize = new Vector2(tSize, tSize)
            };
        }

        /// <summary>
        /// Replaces the heightmap buffer in the WoodsFile with a 1000x500 version of the Interesting Terrains heightmap.
        /// </summary>
        public static void InitializeWoodsFileHeightmap()
        {
            var woodsFile = DaggerfallUnity.Instance.ContentReader.WoodsFileReader;
            var original = woodsFile.Buffer;
            originalHeightmapBuffer = new byte[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                originalHeightmapBuffer[i] = original[i];
            }

            var alteredHeights = new ComputeBuffer(original.Length, sizeof(float));

            var cs = UnityEngine.Object.Instantiate(InterestingTerrains.mainHeightComputer);
            var k = cs.FindKernel("CSMain");

            cs.SetFloat("newHeight", Constants.TERRAIN_HEIGHT);
            cs.SetFloat("maxTerrainHeight", 2308.5f);
            cs.SetFloat("scaledOceanElevation", 27.2f);
            cs.SetFloat("baseHeightScale", 8f);
            cs.SetFloat("noiseMapScale", 4f);
            cs.SetFloat("extraNoiseScale", 10f);
            cs.SetVector("terrainSize", new Vector2(WoodsFile.MapWidth, WoodsFile.MapHeight));
            cs.SetVector("terrainPosition", Vector2.zero);
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetBuffer(k, "Result", alteredHeights);
            InterestingTerrains.instance.csParams.ApplyToCS(cs);

            cs.Dispatch(k, WoodsFile.MapWidth / 10, WoodsFile.MapHeight / 5, 1);

            var floatHeights = new float[original.Length];
            alteredHeights.GetData(floatHeights);

            alteredHeightmapBuffer = Utility.ToBytes(floatHeights);
            woodsFile.Buffer = alteredHeightmapBuffer;

            baseHeightmap = new Texture2D(WoodsFile.MapWidth, WoodsFile.MapHeight, TextureFormat.ARGB32, false, true);
            baseHeightmap.SetPixels32(ToBasemap(alteredHeightmapBuffer));
            baseHeightmap.Apply();

            alteredHeights.Release();
            alteredHeights.Dispose();
        }

        public static void Cleanup()
        {
            locationHeightData.Release();
            locationHeightData.Dispose();
        }

        /// <summary>
        /// Initializes and runs a TerrainComputer GPU job, then processes the generated data.
        /// </summary>
        /// <param name="csPrototype"></param>
        /// <param name="mapData"></param>
        /// <param name="csParams"></param>
        public void DispatchAndProcess(ComputeShader csPrototype, ref MapPixelData mapData, TerrainComputerParams csParams)
        {
            var woodsFile = DaggerfallUnity.Instance.ContentReader.WoodsFileReader;
            var cs = UnityEngine.Object.Instantiate(csPrototype);
            var k = cs.FindKernel("TerrainComputer");
            uint _x, _y, _z;
            cs.GetKernelThreadGroupSizes(k, out _x, out _y, out _z);
            
            int res = heightmapResolution + 1;

            DaggerfallUnity dfUnity;
            DaggerfallUnity.FindDaggerfallUnity(out dfUnity);
            int searchSize = 16;
            var locations = new List<Rect>();

            int x, y;

            for (x = -searchSize; x <= searchSize; x++)
            {
                for (y = -searchSize; y <= searchSize; y++)
                {
                    var mpx = mapData.mapPixelX + x;
                    var mpy = mapData.mapPixelY + y;
                    var key = new DoubleInt() { Item1 = mpx, Item2 = mpy };

                    if (locationRectCache.ContainsKey(key))
                    {
                        locations.Add(locationRectCache[key]);
                        continue;
                    }

                    var mapPixelPos = new DFPosition(mpx, mpy);
                    var mapPixelData = TerrainHelper.GetMapPixelData(dfUnity.ContentReader, mpx, mpy);

                    if (!mapPixelData.hasLocation)
                    {
                        continue;
                    }

                    var location = dfUnity.ContentReader.MapFileReader.GetLocation(mapPixelData.mapRegionIndex, mapPixelData.mapLocationIndex);

                    var locationRect = GetLocationRect(location);
                    if (locationRect.width == 0 || locationRect.height == 0)
                    {
                        continue;
                    }
                    locationRect = ExpandInEachDirection(locationRect, 1);

                    locationRectCache.Add(key, locationRect);
                    locations.Add(locationRect);
                }
            }

            x = (int)_x;
            y = (int)_y;

            cs.SetVector("terrainPosition", terrainPosition);
            cs.SetVector("terrainSize", terrainSize);
            cs.SetInt("heightmapResolution", heightmapResolution);
            cs.SetVector("locationPosition", locationRect.min);
            cs.SetVector("locationSize", locationRect.size);
            cs.SetVectorArray("locationPositions", locations.Select(r => new Vector4(r.min.x, r.min.y)).ToArray());
            cs.SetVectorArray("locationSizes", locations.Select(r => new Vector4(r.size.x, r.size.y)).ToArray());
            cs.SetInt("locationCount", locations.Count);
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetTexture(k, "tileableNoise", InterestingTerrains.tileableNoise);
            cs.SetFloat("originalHeight", Utility.GetOriginalTerrainHeight());
            cs.SetFloat("newHeight", Constants.TERRAIN_HEIGHT);
            cs.SetTexture(k, "mapPixelHeights", baseHeightmap);
            cs.SetBuffer(k, "heightmapBuffer", heightmapBuffers.heightmapBuffer);
            cs.SetBuffer(k, "rawNoise", heightmapBuffers.rawNoise);
            cs.SetBuffer(k, "locationHeightData", locationHeightData);
            cs.SetVector("worldSize", Utility.GetWorldVertexSize());

            var rd = Compatibility.BasicRoadsUtils.GetRoadData(mapData.mapPixelX, mapData.mapPixelY);
            cs.SetVectorArray("NW_NE_SW_SE", rd.NW_NE_SW_SE);
            cs.SetVectorArray("N_E_S_W", rd.N_E_S_W);

            csParams.ApplyToCS(cs);

            woodsFile.Buffer = originalHeightmapBuffer;
            HandleBaseMapSampleParams(ref mapData, ref cs, k);
            woodsFile.Buffer = alteredHeightmapBuffer;

            cs.Dispatch(k, res / x, res / y, 1);

            k = cs.FindKernel("TilemapComputer");
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetBuffer(k, "heightmapBuffer", heightmapBuffers.heightmapBuffer);
            cs.SetBuffer(k, "tilemapData", heightmapBuffers.tilemapData);
            cs.SetBuffer(k, "rawNoise", heightmapBuffers.rawNoise);

            cs.Dispatch(k, res / x, res / y, 1);

            BufferIO.ProcessBufferValuesAndDispose(heightmapBuffers, ref mapData);
        }

        private static Color32[] ToBasemap(byte[] heightBuffer)
        {
            var basemap = new Color32[heightBuffer.Length];

            for (int x = 0; x < WoodsFile.MapWidth; x++)
            {
                for(int y = 0; y < WoodsFile.MapHeight; y++)
                {
                    var idx = x + y * WoodsFile.MapWidth;
                    var sampleIdx = x + (WoodsFile.MapHeight - 1 - y) * WoodsFile.MapWidth;

                    var b = heightBuffer[sampleIdx];
                    basemap[idx] = new Color32(b, b, b, 255);
                }
            }

            return basemap;
        }

        void HandleBaseMapSampleParams(ref MapPixelData mapPixel, ref ComputeShader cs, int k)
        {
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

            // Divisor ensures continuous 0-1 range of height samples
            float div = (sampler.HeightmapDimension - 1) / 3f;

            // Read neighbouring height samples for this map pixel
            int mx = mapPixel.mapPixelX;
            int my = mapPixel.mapPixelY;
            int sDim = 4;
            var shmByte = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValuesRange1Dim(mx - 2, my - 2, sDim);
            var shm = new float[shmByte.Length];
            int i;
            for (i = 0; i < shm.Length; i++)
            {
                
                shm[i] = Convert.ToSingle(shmByte[i]);
            }

            // Convert & flatten large height samples 2d array into 1d native array.
            byte[,] lhm2 = dfUnity.ContentReader.WoodsFileReader.GetLargeHeightMapValuesRange(mx - 1, my, 3);
            float[] lhm = new float[lhm2.Length];
            int lDim = lhm2.GetLength(0);
            i = 0;
            for (int y = 0; y < lDim; y++)
                for (int x = 0; x < lDim; x++)
                    lhm[i++] = Convert.ToSingle(lhm2[x, y]);

            // Extract height samples for all chunks
            int hDim = sampler.HeightmapDimension;

            // Create buffers with extracted heightmap data
            heightmapBuffers.shm = new ComputeBuffer(shm.Length, sizeof(float));
            heightmapBuffers.shm.SetData(shm);

            heightmapBuffers.lhm = new ComputeBuffer(lhm.Length, sizeof(float));
            heightmapBuffers.lhm.SetData(lhm);

            // Assign properties to CS
            cs.SetBuffer(k, "shm", heightmapBuffers.shm);
            cs.SetBuffer(k, "lhm", heightmapBuffers.lhm);
            cs.SetInt("sd", sDim);
            cs.SetInt("ld", lDim);
            cs.SetInt("hDim", hDim);
            cs.SetFloat("div", div);
            cs.SetInt("mapPixelX", mapPixel.mapPixelX);
            cs.SetInt("mapPixelY", mapPixel.mapPixelY);
            cs.SetFloat("maxTerrainHeight", 2308.5f);
            cs.SetFloat("baseHeightScale", 8f);
            cs.SetFloat("noiseMapScale", 4f);
            cs.SetFloat("extraNoiseScale", 10f);
            cs.SetFloat("scaledOceanElevation", 27.2f);
        }

        static Rect ExpandInEachDirection(Rect src, int amount)
        {
            var amt = new Vector2(amount, amount);
            var pos = src.position;
            var size = src.size;

            pos -= amt;
            size += amt * 2;

            return new Rect(pos, size);
        }

        static Rect GetLocationRect(DFLocation currentLocation)
        {
            var locationRect = DaggerfallLocation.GetLocationRect(currentLocation);

            var min = WorldCoordToTerrainPosition(locationRect.min);
            var max = WorldCoordToTerrainPosition(locationRect.max);

            return new Rect()
            {
                xMin = min.x,
                yMin = min.y,
                xMax = max.x,
                yMax = max.y
            };
        }

        static Vector2 WorldCoordToTerrainPosition(Vector2 worldCoords)
        {
            var posX = worldCoords.x / 32768 * Utility.GetTerrainVertexSize();
            var posY = worldCoords.y / 32768 * Utility.GetTerrainVertexSize();

            return new Vector2(posX, posY);
        }
    }
}