using DaggerfallWorkshop;
using Unity.Jobs;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace Monobelisk
{
    public class InterestingTerrainSampler : TerrainSampler
    {
        // Declaring as class-level variables
        private Mod WOMod;
        private bool WOModEnabled;

        // Constructor
        public InterestingTerrainSampler()
        {
            // Accessing the ModManager and checking if the WOMod is enabled
            WOMod = ModManager.Instance.GetModFromGUID("2beb90e5-de58-43cf-b61c-46652f5ecbe3");
            WOModEnabled = WOMod != null && WOMod.Enabled;

            HeightmapDimension = defaultHeightmapDimension;
            MaxTerrainHeight = 5000f;
            MeanTerrainHeightScale = 5000f / 255f;
            OceanElevation = 100.01f;
            BeachElevation = 103.9f;
        }

        public override int Version
        {
            get { return 7; }
        }

        public override bool IsLocationTerrainBlended()
        {
            return true;
        }

        public override void GenerateSamples(ref MapPixelData mapPixel)
        {
            mapPixel.maxHeight = MaxTerrainHeight;
            var computer = TerrainComputer.Create(mapPixel, this);            
            computer.DispatchAndProcess(InterestingTerrains.csPrototype, ref mapPixel, InterestingTerrains.instance.csParams);
        }

        public override JobHandle ScheduleGenerateSamplesJob(ref MapPixelData mapPixel)
        {
            GenerateSamples(ref mapPixel);


            return new JobHandle();
        }
    }
}
