using UnityEngine;

namespace Monobelisk
{
    public struct HeightmapBufferCollection
    {
        public ComputeBuffer heightmapBuffer;
        public ComputeBuffer rawNoise;
        public ComputeBuffer tilemapData;

        public ComputeBuffer shm;
        public ComputeBuffer lhm;

        public void ApplyToCS(ComputeShader cs, int kernel)
        {
            cs.SetBuffer(kernel, "heightmapBuffer", heightmapBuffer);
            cs.SetBuffer(kernel, "rawNoise", rawNoise);
            cs.SetBuffer(kernel, "tilemapData", tilemapData);
        }

        public void Dispose()
        {
            heightmapBuffer.Release();
            heightmapBuffer.Dispose();
            rawNoise.Release();
            rawNoise.Dispose();
            tilemapData.Release();
            tilemapData.Dispose();
            shm.Release();
            shm.Dispose();
            lhm.Release();
            lhm.Dispose();
        }
    }
}