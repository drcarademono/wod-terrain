// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Hazelnut, Daniel87
//
// Notes:
//

using UnityEngine;
using System;
using DaggerfallConnect.Arena2;
using Unity.Jobs;
using Unity.Collections;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace WildernessOverhaul
{
    public class WOTerrainTexturing : ITerrainTexturing
    {
        // Use same seed to ensure continuous tiles
        const int seed = 417028;
        const byte water = 0;
        const byte dirt = 1;
        const byte grass = 2;
        const byte stone = 3;

        static bool interestingErodedTerrainEnabled;
        readonly bool basicRoadsEnabled;

        // Order: Deser1, Desert2, Mountains, Rainforest, Swamp,
        // Subtropics, Mountain Woods, Woodland, Haunted Woods, Ocean
        static float[] frequency = {0.02f, 0.02f, 0.025f, 0.035f, 0.02f, 0.02f, 0.035f, 0.035f, 0.035f, 0.1f};
        static float[] amplitude = {0.3f, 0.3f, 0.3f, 0.4f, 0.3f, 0.3f, 0.4f, 0.4f, 0.4f, 0.95f};
        static float[] persistance = {0.5f, 0.55f, 0.95f, 0.8f, 0.5f, 0.5f, 0.8f, 0.5f, 0.8f, 0.3f};
        static int octaves = 5;
        static float[] upperWaterSpread = {-1.0f, -1.0f, 0.0f, 0.0f, -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f};
        static float[] lowerGrassSpread = {0.4f, 0.35f, 0.35f, 0.35f, 0.4f, 0.4f, 0.35f, 0.35f, 0.35f, 0.35f};
        static float[] upperGrassSpread = {0.5f, 0.5f, 0.95f, 0.95f, 0.5f, 0.5f, 0.95f, 0.95f, 0.95f, 0.95f};

        public static float treeLine = UnityEngine.Random.Range(0.675f, 0.69f);

        protected static readonly int tileDataDim = MapsFile.WorldMapTileDim + 1;
        protected static readonly int assignTilesDim = MapsFile.WorldMapTileDim;

        protected byte[] lookupTable;
        static int[][] lookupRegistry;

        static MapPixelData currentMapData;

        public WOTerrainTexturing(bool ITEnabled, bool basicRoadsEnabled)
        {
            interestingErodedTerrainEnabled = ITEnabled;
            this.basicRoadsEnabled = basicRoadsEnabled;
            CreateLookupTable();
            Debug.Log("[WOTerrainTexturing] Hi mom! - carademono}");
        }

        // Turn off the normal water tile conversion in DFU, do it in this mod instead.
        public bool ConvertWaterTiles()
        {
            return false;
        }

        public virtual JobHandle ScheduleAssignTilesJob(ITerrainSampler terrainSampler, ref MapPixelData mapData, JobHandle dependencies, bool march = true)
        {
            // Cache tile data to minimise noise sampling during march.
            NativeArray<byte> tileData = new NativeArray<byte>(tileDataDim * tileDataDim, Allocator.TempJob);
            currentMapData = mapData;

            GenerateTileDataJob tileDataJob = new GenerateTileDataJob
            {
                heightmapData = mapData.heightmapData,
                tileData = tileData,
                tdDim = tileDataDim,
                hDim = terrainSampler.HeightmapDimension,
                maxTerrainHeight = terrainSampler.MaxTerrainHeight,
                oceanElevation = terrainSampler.OceanElevation,
                beachElevation = terrainSampler.BeachElevation,
                mapPixelX = mapData.mapPixelX,
                mapPixelY = mapData.mapPixelY,
                worldClimate = mapData.worldClimate,
            };
            JobHandle tileDataHandle = tileDataJob.Schedule(tileDataDim * tileDataDim, 64, dependencies);

            // Schedule the paint roads jobs if basic roads mod is enabled
            JobHandle preAssignTilesHandle = tileDataHandle;
            if (basicRoadsEnabled)
            {
                ModManager.Instance.SendModMessage("BasicRoads", "scheduleRoadsJob", new object[] { mapData, tileData, tileDataHandle },
                    (string message, object data) =>
                    {
                        if (message == "error")
                            Debug.LogError(data as string);
                        else
                            preAssignTilesHandle = (JobHandle)data;
                    });
            }

            // Assign tile data to terrain
            NativeArray<byte> lookupData = new NativeArray<byte>(lookupTable, Allocator.TempJob);
            AssignTilesJob assignTilesJob = new AssignTilesJob
            {
                lookupTable = lookupData,
                tileData = tileData,
                tilemapData = mapData.tilemapData,
                tdDim = tileDataDim,
                tDim = assignTilesDim,
                march = march,
                locationRect = mapData.locationRect,
            };
            JobHandle assignTilesHandle = assignTilesJob.Schedule(assignTilesDim * assignTilesDim, 64, preAssignTilesHandle);

            // Add both working native arrays to disposal list.
            mapData.nativeArrayList.Add(tileData);
            mapData.nativeArrayList.Add(lookupData);

            return assignTilesHandle;
        }

        protected struct AssignTilesJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<byte> tileData;
            [ReadOnly]
            public NativeArray<byte> lookupTable;

            public NativeArray<byte> tilemapData;

            public int tdDim;
            public int tDim;
            public bool march;
            public Rect locationRect;

            public void Execute(int index)
            {
                int x = JobA.Row(index, tDim);
                int y = JobA.Col(index, tDim);

                // Do nothing if in location rect as texture already set,
                if (tilemapData[index] != 0)
                {
                    // Convert 0xFF to water now rather than let DFU do it
                    if (tilemapData[index] == byte.MaxValue)
                        tilemapData[index] = 0;
                    return;
                }

                // Assign tile texture
                if (march)
                {
                    int bl = tileData[JobA.Idx(x, y, tdDim)];
                    int br = tileData[JobA.Idx(x + 1, y, tdDim)];
                    int tr = tileData[JobA.Idx(x + 1, y + 1, tdDim)];
                    int tl = tileData[JobA.Idx(x, y + 1, tdDim)];

                    tilemapData[index] = lookupTable[FindTileIndex(lookupRegistry, bl, br, tr, tl)];
                }
                else
                {
                    tilemapData[index] = tileData[JobA.Idx(x, y, tdDim)];
                }
            }
        }

        protected struct GenerateTileDataJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<float> heightmapData;

            public NativeArray<byte> tileData;

            public int hDim;
            public int tdDim;
            public int tDim;
            public float maxTerrainHeight;
            public float oceanElevation;
            public float beachElevation;
            public int mapPixelX;
            public int mapPixelY;
            public int worldClimate;

            // Gets noise value
            private float NoiseWeight(float worldX, float worldY, float height)
            {
                float persistanceRnd = 0.95f;
                int climateNum = 9;
                switch (worldClimate) {
                    case (int)MapsFile.Climates.Desert:
                        climateNum = 0;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 2) - 0.25f;
                        break;
                    case (int)MapsFile.Climates.Desert2:
                        climateNum = 1;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 2) - 0.25f;
                        break;
                    case (int)MapsFile.Climates.Mountain:
                        climateNum = 2;
                        if ((height / maxTerrainHeight) + JobRand.Next(-5, 5) / 1000f > treeLine)
                            persistanceRnd = persistance[climateNum] - treeLine + ((height / maxTerrainHeight) * 1.2f);
                        else
                            persistanceRnd = persistance[climateNum] - (1 - (height / maxTerrainHeight)) * 0.4f;
                        break;
                    case (int)MapsFile.Climates.Rainforest:
                        climateNum = 3;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 1.5f) - 0.35f;
                        break;
                    case (int)MapsFile.Climates.Swamp:
                        climateNum = 4;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 3) - 0.25f;
                        break;
                    case (int)MapsFile.Climates.Subtropical:
                        climateNum = 5;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 2f) - 0.25f;
                        break;
                    case (int)MapsFile.Climates.MountainWoods:
                        climateNum = 6;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 1.5f) - 0.35f;
                        break;
                    case (int)MapsFile.Climates.Woodlands:
                        climateNum = 7;
                        if (interestingErodedTerrainEnabled)
                            persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 2.5f);
                        else
                            persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 1.4f) - 0.30f;
                        break;
                    case (int)MapsFile.Climates.HauntedWoodlands:
                        climateNum = 8;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 1.5f) - 0.35f;
                        break;
                    case (int)MapsFile.Climates.Ocean:
                        climateNum = 9;
                        persistanceRnd = persistance[climateNum] + ((height / maxTerrainHeight) * 1.5f) - 0.35f;
                        break;
                }
                return GetNoise(worldX, worldY, frequency[climateNum], amplitude[climateNum], persistanceRnd, octaves, seed);
            }

            // Sets texture by range
            private byte GetWeightedRecord(float weight, float upperWaterSpread = 0.0f, float lowerGrassSpread = 0.35f, float upperGrassSpread = 0.95f)
            {
                if (weight < upperWaterSpread)
                    return water;
                else if (weight >= upperWaterSpread && weight < lowerGrassSpread)
                    return dirt;
                else if (weight >= lowerGrassSpread && weight < upperGrassSpread)
                    return grass;
                else
                    return stone;
            }

            // Noise function
            private float GetNoise(
              float x,
              float y,
              float frequency,
              float amplitude,
              float persistance,
              int octaves,
              int seed = 0)
            {
                float finalValue = 0f;
                for (int i = 0; i < octaves; ++i)
                {
                    finalValue += Mathf.PerlinNoise(seed + (x * frequency), seed + (y * frequency)) * amplitude;
                    frequency *= 2.0f;
                    amplitude *= persistance;
                }
                return Mathf.Clamp(finalValue, -1, 1);
            }

            public void Execute(int index)
            {
                int x = JobA.Row(index, tdDim);
                int y = JobA.Col(index, tdDim);
                int uB = heightmapData.Length;

                // Height sample for ocean and beach tiles
                int hx = (int)Mathf.Clamp(hDim * ((float)x / (float)tdDim), 0, hDim - 1);
                int hy = (int)Mathf.Clamp(hDim * ((float)y / (float)tdDim), 0, hDim - 1);
                float height = heightmapData[JobA.Idx(hy, hx, hDim)] * maxTerrainHeight;

                // Ocean and Beach texture
                if (height < oceanElevation - 0.001f) {
                    tileData[index] = water;
                    return;
                }

                // Adds a little +/- randomness to threshold so beach line isn't too regular
                // Thinner beach where a rock face is diving down steep into water
                if (height < beachElevation - 3) {
                    tileData[index] = dirt;
                    return;
                }

                // Rock Mountain Face
                int rnd = JobRand.Next(75,90);
                if (SteepnessWithinLimits(true, Mathf.Clamp(rnd - ((height / maxTerrainHeight) / rnd),40f,90f), heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = stone;
                    return;
                }

                // Adds a little +/- randomness to threshold so beach line isn't too regular
                if (height <= beachElevation + (JobRand.Next(100000000, 250000000) / 10000000f)) {
                    tileData[index] = dirt;
                    return;
                }

                // Get latitude and longitude of this tile
                int latitude = (int)(mapPixelX * MapsFile.WorldMapTileDim + x);
                int longitude = (int)(MapsFile.MaxWorldTileCoordZ - mapPixelY * MapsFile.WorldMapTileDim + y);

                // Set texture tile using weighted noise
                float weight = 0;
                weight += NoiseWeight(latitude, longitude, height);


                int climateNum = 9;
                switch (worldClimate)
                {
                    case (int)MapsFile.Climates.Desert:
                        climateNum = 0;
                        break;
                    case (int)MapsFile.Climates.Desert2:
                        climateNum = 1;
                        break;
                    case (int)MapsFile.Climates.Mountain:
                        climateNum = 2;
                        break;
                    case (int)MapsFile.Climates.Rainforest:
                        climateNum = 3;
                        break;
                    case (int)MapsFile.Climates.Swamp:
                        climateNum = 4;
                        break;
                    case (int)MapsFile.Climates.Subtropical:
                        climateNum = 5;
                        break;
                    case (int)MapsFile.Climates.MountainWoods:
                        climateNum = 6;
                        break;
                    case (int)MapsFile.Climates.Woodlands:
                        climateNum = 7;
                        break;
                    case (int)MapsFile.Climates.HauntedWoodlands:
                        climateNum = 8;
                        break;
                    case (int)MapsFile.Climates.Ocean:
                        climateNum = 9;
                        break;
                }
                tileData[index] = GetWeightedRecord(weight, upperWaterSpread[climateNum], lowerGrassSpread[climateNum], upperGrassSpread[climateNum]);

                // Check for lowest local point in desert to place oasis
                if (worldClimate == (int)MapsFile.Climates.Desert2 && LowestPointFound(30, heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = water;
                    return;
                }

                if (worldClimate == (int)MapsFile.Climates.Desert && LowestPointFound(80, heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = water;
                    return;
                }



                /* rnd = JobRand.Next(25,35);
                if (tileData[index] == stone && SteepnessWithinLimits(false, Mathf.Clamp(rnd - ((height / maxTerrainHeight) / rnd),40f,90f), heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = grass;
                    return;
                } */

                // Max angle for dirt patches
                rnd = JobRand.Next(20,25);
                if (tileData[index] == dirt && SteepnessWithinLimits(false, rnd, heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = dirt;
                    return;
                }

                if (tileData[index] == dirt && !SteepnessWithinLimits(false, rnd, heightmapData, maxTerrainHeight, hx, hy, hDim, uB, index, tdDim, tileData)) {
                    tileData[index] = grass;
                    return;
                }
            }
        }

        static bool LowestPointFound(int chance, NativeArray<float> heightmapData, float maxTerrainHeight, int hx, int hy, int hDim, int upperBound, int index, int tdDim, NativeArray<byte> tileData)
        {
            int newChance = (int)(chance - (chance * heightmapData[JobA.Idx(hy, hx, hDim)] * 2));

            if (tileData[index] != dirt ||
              JobA.Row(index, tdDim) - 5 <= 0 ||
              JobA.Col(index, tdDim) + 5 >= tdDim ||
              JobA.Row(index, tdDim) + 5 >= tdDim ||
              JobA.Col(index, tdDim) - 5 <= 0)
            {
                return false;
            }
            else
            {
                float thisHeight = heightmapData[JobA.Idx(hy, hx, hDim)] * maxTerrainHeight;
                if (JobA.Idx(hy - 5, hx - 5, hDim) < 0 ||
                    JobA.Idx(hy - 5, hx - 5, hDim) > upperBound ||
                    JobA.Idx(hy - 5, hx + 5, hDim) < 0 ||
                    JobA.Idx(hy - 5, hx + 5, hDim) > upperBound ||
                    JobA.Idx(hy + 5, hx - 5, hDim) < 0 ||
                    JobA.Idx(hy + 5, hx - 5, hDim) > upperBound ||
                    JobA.Idx(hy + 5, hx + 5, hDim) < 0 ||
                    JobA.Idx(hy + 5, hx + 5, hDim) > upperBound)
                {
                    return false;
                }
                else
                {
                    for (int a = -4; a < 5; a++)
                    {
                        for (int b = -4; b < 5; b++)
                        {
                            if ((a != 0 && b != 0) && heightmapData[JobA.Idx(hy + a, hx + b, hDim)] * maxTerrainHeight < thisHeight + (0.0000035f * Mathf.Abs(a)) + (0.0000035f * Mathf.Abs(b)))
                            {
                                return false;
                            }
                        }
                    }
                    return (JobRand.Next(0, 100) <= newChance);
                }
            }
        }

        static bool SteepnessWithinLimits(bool bigger, float steepness, NativeArray<float> heightmapData, float maxTerrainHeight, int hx, int hy, int hDim, int upperBound, int index, int tdDim, NativeArray<byte> tileData)
        {
            int offsetX = 0;
            int offsetY = 0;
            if (JobA.Col(index, tdDim) + 1 >= tdDim)
            {
                offsetY = -1;
            }
            if (JobA.Row(index, tdDim) + 1 >= tdDim)
            {
                offsetX = -1;
            }

            float minSmpl = 0;
            float maxSmpl = 0;
            float smpl = minSmpl = maxSmpl = heightmapData[JobA.Idx(hy, hx, hDim)] * maxTerrainHeight;
            for (int a = 0; a <= 1; a++)
            {
                for (int b = 0; b <= 1; b++)
                {
                    smpl = heightmapData[JobA.Idx(hy + a + offsetY, hx + b + offsetX, hDim)] * maxTerrainHeight;

                    if (smpl < minSmpl)
                    {
                        minSmpl = smpl;
                    }
                    if (smpl > maxSmpl)
                    {
                        maxSmpl = smpl;
                    }
                }
            }

            float diff = (maxSmpl - minSmpl) * 10f;

            if (bigger) {
                return (diff >= steepness);
            } else {
                return (diff <= steepness);
            }
        }

        // Encodes a byte with Daggerfall tile neighbours
        static int FindTileIndex(int[][] array, int bl, int br, int tr, int tl)
        {
            int[] testArray = new int[4]{bl,br,tr,tl};
            for (int i = 0; i < array.Length; ++i) {
                if(array[i][0] == testArray[0] && array[i][1] == testArray[1] && array[i][2] == testArray[2] && array[i][3] == testArray[3]) {
                    return i;
                }
            }
            Debug.LogErrorFormat("Couldnt find index. Setting tile to water.");
            return 0;
        }

        // Encodes a byte with Daggerfall tile lookup
        static byte MakeLookup(int index, bool rotate, bool flip)
        {
            if (index > 63)
                throw new IndexOutOfRangeException("Index out of range. Valid range 0-63");
            if (rotate) index += 64;
            if (flip) index += 128;

            return (byte)index;
        }

        void CreateLookupTable() {
            lookupTable = new byte[113];
            lookupRegistry = new int[113][];

            // Water
            lookupTable[0] = MakeLookup(0, false, false);
            lookupRegistry[0] = new int[4]{0,0,0,0};

            // Water - Dirt
            lookupTable[1] = MakeLookup(5, true, false);
            lookupTable[2] = MakeLookup(5, false, true);
            lookupTable[3] = MakeLookup(5, true, true);
            lookupTable[4] = MakeLookup(5, false, false);
            lookupRegistry[1] = new int[4]{1,0,0,0};
            lookupRegistry[2] = new int[4]{0,1,0,0};
            lookupRegistry[3] = new int[4]{0,0,1,0};
            lookupRegistry[4] = new int[4]{0,0,0,1};

            lookupTable[5] = MakeLookup(6, true, false);
            lookupTable[6] = MakeLookup(6, false, true);
            lookupTable[7] = MakeLookup(6, true, true);
            lookupTable[8] = MakeLookup(6, false, false);
            lookupRegistry[5] = new int[4]{1,1,0,0};
            lookupRegistry[6] = new int[4]{0,1,1,0};
            lookupRegistry[7] = new int[4]{0,0,1,1};
            lookupRegistry[8] = new int[4]{1,0,0,1};

            lookupTable[9] = MakeLookup(7, false, true);
            lookupTable[10] = MakeLookup(7, true, true);
            lookupTable[11] = MakeLookup(7, false, false);
            lookupTable[12] = MakeLookup(7, true, false);
            lookupRegistry[9] = new int[4]{1,1,1,0};
            lookupRegistry[10] = new int[4]{0,1,1,1};
            lookupRegistry[11] = new int[4]{1,0,1,1};
            lookupRegistry[12] = new int[4]{1,1,0,1};

            lookupTable[13] = MakeLookup(48, false, false);
            lookupTable[14] = MakeLookup(48, true, false);
            lookupRegistry[13] = new int[4]{0,1,0,1};
            lookupRegistry[14] = new int[4]{1,0,1,0};

            lookupTable[15] = MakeLookup(1, false, false);
            lookupRegistry[15] = new int[4]{1,1,1,1};

            // Dirt - Grass
            lookupTable[16] = MakeLookup(10, true, false);
            lookupTable[17] = MakeLookup(10, false, true);
            lookupTable[18] = MakeLookup(10, true, true);
            lookupTable[19] = MakeLookup(10, false, false);
            lookupRegistry[16] = new int[4]{2,1,1,1};
            lookupRegistry[17] = new int[4]{1,2,1,1};
            lookupRegistry[18] = new int[4]{1,1,2,1};
            lookupRegistry[19] = new int[4]{1,1,1,2};

            lookupTable[20] = MakeLookup(11, true, false);
            lookupTable[21] = MakeLookup(11, false, true);
            lookupTable[22] = MakeLookup(11, true, true);
            lookupTable[23] = MakeLookup(11, false, false);
            lookupRegistry[20] = new int[4]{2,2,1,1};
            lookupRegistry[21] = new int[4]{1,2,2,1};
            lookupRegistry[22] = new int[4]{1,1,2,2};
            lookupRegistry[23] = new int[4]{2,1,1,2};

            lookupTable[24] = MakeLookup(12, false, true);
            lookupTable[25] = MakeLookup(12, true, true);
            lookupTable[26] = MakeLookup(12, false, false);
            lookupTable[27] = MakeLookup(12, true, false);
            lookupRegistry[24] = new int[4]{2,2,2,1};
            lookupRegistry[25] = new int[4]{1,2,2,2};
            lookupRegistry[26] = new int[4]{2,1,2,2};
            lookupRegistry[27] = new int[4]{2,2,1,2};

            lookupTable[28] = MakeLookup(51, true, false);
            lookupTable[29] = MakeLookup(51, false, false);
            lookupRegistry[28] = new int[4]{2,1,2,1};
            lookupRegistry[29] = new int[4]{1,2,1,2};

            lookupTable[30] = MakeLookup(2, false, false);
            lookupRegistry[30] = new int[4]{2,2,2,2};

            // Grass - Stone
            lookupTable[31] = MakeLookup(15, true, false);
            lookupTable[32] = MakeLookup(15, false, true);
            lookupTable[33] = MakeLookup(15, true, true);
            lookupTable[34] = MakeLookup(15, false, false);
            lookupRegistry[31] = new int[4]{3,2,2,2};
            lookupRegistry[32] = new int[4]{2,3,2,2};
            lookupRegistry[33] = new int[4]{2,2,3,2};
            lookupRegistry[34] = new int[4]{2,2,2,3};

            lookupTable[35] = MakeLookup(16, true, false);
            lookupTable[36] = MakeLookup(16, false, true);
            lookupTable[37] = MakeLookup(16, true, true);
            lookupTable[38] = MakeLookup(16, false, false);
            lookupRegistry[35] = new int[4]{3,3,2,2};
            lookupRegistry[36] = new int[4]{2,3,3,2};
            lookupRegistry[37] = new int[4]{2,2,3,3};
            lookupRegistry[38] = new int[4]{3,2,2,3};

            lookupTable[39] = MakeLookup(17, false, true);
            lookupTable[40] = MakeLookup(17, true, true);
            lookupTable[41] = MakeLookup(17, false, false);
            lookupTable[42] = MakeLookup(17, true, false);
            lookupRegistry[39] = new int[4]{3,3,3,2};
            lookupRegistry[40] = new int[4]{2,3,3,3};
            lookupRegistry[41] = new int[4]{3,2,3,3};
            lookupRegistry[42] = new int[4]{3,3,2,3};

            lookupTable[43] = MakeLookup(53, true, false);
            lookupTable[44] = MakeLookup(53, false, false);
            lookupRegistry[43] = new int[4]{3,2,3,2};
            lookupRegistry[44] = new int[4]{2,3,2,3};

            lookupTable[45] = MakeLookup(3, false, false);
            lookupRegistry[45] = new int[4]{3,3,3,3};

            // Dirt - Stone
            lookupTable[46] = MakeLookup(25, true, false);
            lookupTable[47] = MakeLookup(25, false, true);
            lookupTable[48] = MakeLookup(25, true, true);
            lookupTable[49] = MakeLookup(25, false, false);
            lookupRegistry[46] = new int[4]{3,1,1,1};
            lookupRegistry[47] = new int[4]{1,3,1,1};
            lookupRegistry[48] = new int[4]{1,1,3,1};
            lookupRegistry[49] = new int[4]{1,1,1,3};

            lookupTable[50] = MakeLookup(26, true, false);
            lookupTable[51] = MakeLookup(26, false, true);
            lookupTable[52] = MakeLookup(26, true, true);
            lookupTable[53] = MakeLookup(26, false, false);
            lookupRegistry[50] = new int[4]{3,3,1,1};
            lookupRegistry[51] = new int[4]{1,3,3,1};
            lookupRegistry[52] = new int[4]{1,1,3,3};
            lookupRegistry[53] = new int[4]{3,1,1,3};

            lookupTable[54] = MakeLookup(27, false, true);
            lookupTable[55] = MakeLookup(27, true, true);
            lookupTable[56] = MakeLookup(27, false, false);
            lookupTable[57] = MakeLookup(27, true, false);
            lookupRegistry[54] = new int[4]{3,3,3,1};
            lookupRegistry[55] = new int[4]{1,3,3,3};
            lookupRegistry[56] = new int[4]{3,1,3,3};
            lookupRegistry[57] = new int[4]{3,3,1,3};

            lookupTable[58] = MakeLookup(52, true, false);
            lookupTable[59] = MakeLookup(52, false, false);
            lookupRegistry[58] = new int[4]{3,1,3,1};
            lookupRegistry[59] = new int[4]{1,3,1,3};

            //Dirt - Grass - Stone
            lookupTable[60] = MakeLookup(39, true, false);
            lookupTable[61] = MakeLookup(39, false, true);
            lookupTable[62] = MakeLookup(39, true, true);
            lookupTable[63] = MakeLookup(39, false, false);
            lookupRegistry[60] = new int[4]{1,1,2,3};
            lookupRegistry[61] = new int[4]{3,1,1,2};
            lookupRegistry[62] = new int[4]{2,3,1,1};
            lookupRegistry[63] = new int[4]{1,2,3,1};

            lookupTable[64] = MakeLookup(56, true, false);
            lookupTable[65] = MakeLookup(56, false, true);
            lookupTable[66] = MakeLookup(56, true, true);
            lookupTable[67] = MakeLookup(56, false, false);
            lookupRegistry[64] = new int[4]{1,1,3,2};
            lookupRegistry[65] = new int[4]{2,1,1,3};
            lookupRegistry[66] = new int[4]{3,2,1,1};
            lookupRegistry[67] = new int[4]{1,3,2,1};

            lookupTable[68] = MakeLookup(42, true, false);
            lookupTable[69] = MakeLookup(42, false, true);
            lookupTable[70] = MakeLookup(42, true, true);
            lookupTable[71] = MakeLookup(42, false, false);
            lookupRegistry[68] = new int[4]{2,2,1,3};
            lookupRegistry[69] = new int[4]{3,2,2,1};
            lookupRegistry[70] = new int[4]{1,3,2,2};
            lookupRegistry[71] = new int[4]{2,1,3,2};

            lookupTable[72] = MakeLookup(57, true, false);
            lookupTable[73] = MakeLookup(57, false, true);
            lookupTable[74] = MakeLookup(57, true, true);
            lookupTable[75] = MakeLookup(57, false, false);
            lookupRegistry[72] = new int[4]{2,2,3,1};
            lookupRegistry[73] = new int[4]{1,2,2,3};
            lookupRegistry[74] = new int[4]{3,1,2,2};
            lookupRegistry[75] = new int[4]{2,3,1,2};

            lookupTable[76] = MakeLookup(45, true, false);
            lookupTable[77] = MakeLookup(45, false, true);
            lookupTable[78] = MakeLookup(45, true, true);
            lookupTable[79] = MakeLookup(45, false, false);
            lookupRegistry[76] = new int[4]{3,3,1,2};
            lookupRegistry[77] = new int[4]{2,3,3,1};
            lookupRegistry[78] = new int[4]{1,2,3,3};
            lookupRegistry[79] = new int[4]{3,1,2,3};

            lookupTable[80] = MakeLookup(58, true, false);
            lookupTable[81] = MakeLookup(58, false, true);
            lookupTable[82] = MakeLookup(58, true, true);
            lookupTable[83] = MakeLookup(58, false, false);
            lookupRegistry[80] = new int[4]{3,3,2,1};
            lookupRegistry[81] = new int[4]{1,3,3,2};
            lookupRegistry[82] = new int[4]{2,1,3,3};
            lookupRegistry[83] = new int[4]{3,2,1,3};

            //Dirt - Stone & Grass
            lookupTable[84] = MakeLookup(59, true, false);
            lookupTable[85] = MakeLookup(59, false, true);
            lookupTable[86] = MakeLookup(59, true, true);
            lookupTable[87] = MakeLookup(59, false, false);
            lookupRegistry[84] = new int[4]{3,1,2,1};
            lookupRegistry[85] = new int[4]{1,3,1,2};
            lookupRegistry[86] = new int[4]{2,1,3,1};
            lookupRegistry[87] = new int[4]{1,2,1,3};

            //Dirt - Water & Grass
            lookupTable[88] = MakeLookup(60, true, false);
            lookupTable[89] = MakeLookup(60, false, true);
            lookupTable[90] = MakeLookup(60, true, true);
            lookupTable[91] = MakeLookup(60, false, false);
            lookupRegistry[88] = new int[4]{2,1,0,1};
            lookupRegistry[89] = new int[4]{1,2,1,0};
            lookupRegistry[90] = new int[4]{0,1,2,1};
            lookupRegistry[91] = new int[4]{1,0,1,2};

            //Dirt - Water & Stone
            lookupTable[92] = MakeLookup(61, true, false);
            lookupTable[93] = MakeLookup(61, false, true);
            lookupTable[94] = MakeLookup(61, true, true);
            lookupTable[95] = MakeLookup(61, false, false);
            lookupRegistry[92] = new int[4]{3,1,0,1};
            lookupRegistry[93] = new int[4]{1,3,1,0};
            lookupRegistry[94] = new int[4]{0,1,3,1};
            lookupRegistry[95] = new int[4]{1,0,1,3};

            //Grass - Dirt & Stone
            lookupTable[96] = MakeLookup(62, true, false);
            lookupTable[97] = MakeLookup(62, false, true);
            lookupTable[98] = MakeLookup(62, true, true);
            lookupTable[99] = MakeLookup(62, false, false);
            lookupRegistry[96] = new int[4]{3,2,1,2};
            lookupRegistry[97] = new int[4]{2,3,2,1};
            lookupRegistry[98] = new int[4]{1,2,3,2};
            lookupRegistry[99] = new int[4]{2,1,2,3};

            //Stone - Dirt & Grass
            lookupTable[100] = MakeLookup(63, true, false);
            lookupTable[101] = MakeLookup(63, false, true);
            lookupTable[102] = MakeLookup(63, true, true);
            lookupTable[103] = MakeLookup(63, false, false);
            lookupRegistry[100] = new int[4]{2,3,1,3};
            lookupRegistry[101] = new int[4]{3,2,3,1};
            lookupRegistry[102] = new int[4]{1,3,2,3};
            lookupRegistry[103] = new int[4]{3,1,3,2};

            //Road - Dirt & Grass
            lookupTable[104] = MakeLookup(46, false, false);
            lookupRegistry[104] = new int[4]{46,46,46,46};

            lookupTable[105] = MakeLookup(47, true, false);
            lookupTable[106] = MakeLookup(47, false, true);
            lookupTable[107] = MakeLookup(47, true, true);
            lookupTable[108] = MakeLookup(47, false, false);
            lookupRegistry[105] = new int[4]{1,47,46,47};
            lookupRegistry[106] = new int[4]{47,1,47,46};
            lookupRegistry[107] = new int[4]{46,47,1,47};
            lookupRegistry[108] = new int[4]{47,46,47,1};

            lookupTable[109] = MakeLookup(55, true, false);
            lookupTable[110] = MakeLookup(55, false, true);
            lookupTable[111] = MakeLookup(55, true, true);
            lookupTable[112] = MakeLookup(55, false, false);
            lookupRegistry[109] = new int[4]{2,55,46,55};
            lookupRegistry[110] = new int[4]{55,2,55,46};
            lookupRegistry[111] = new int[4]{46,55,2,55};
            lookupRegistry[112] = new int[4]{55,46,55,2};
        }
    }
}
