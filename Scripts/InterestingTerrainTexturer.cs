using Unity.Jobs;
using Unity.Collections;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using Monobelisk.Compatibility;
using UnityEngine;

namespace Monobelisk
{
    public class InterestingTerrainTexturer : DefaultTerrainTexturing
    {
        /// <summary>
        /// Works exactly like the default TerrainTexturer, except that it skips the GenerateTileDataJob
        /// and uses the tileData generated during terrain sampling instead.
        /// Use the mod messaging system to obtain tileData for a map pixel in a custom TerrainTexturer.
        /// </summary>
        /// 
        /// To access tileData in a separate mod, you can use this code:
        /// <code>
        /// byte[] tData = null;
        /// ModManager.Instance.SendModMessage("Interesting Terrains", "getTileData", new int[] { mapData.mapPixelX, mapData.mapPixelY }, (string message, object data) =>
        /// {
        ///    if (message == "error")
        ///        Debug.LogError(data as string);
        ///    else
        ///        tData = data as byte[];
        /// });
        /// </code>
        public override JobHandle ScheduleAssignTilesJob(ITerrainSampler terrainSampler, ref MapPixelData mapData, JobHandle dependencies, bool march = true)
        {
            // Load tile data generated by the Terrain Sampler
            var tData = InterestingTerrains.tileDataCache.Get(mapData.mapPixelX, mapData.mapPixelY);
            NativeArray<byte> tileData = new NativeArray<byte>(tData, Allocator.TempJob);

            // Schedule the paint roads jobs if basic roads mod is enabled
            JobHandle preAssignTilesHandle = dependencies;
            if (CompatibilityUtils.BasicRoadsLoaded)
            {
                ModManager.Instance.SendModMessage("BasicRoads", "scheduleRoadsJob", new object[] { mapData, tileData, dependencies },
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
    }
}
