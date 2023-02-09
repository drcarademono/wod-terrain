using UnityEngine;

namespace Monobelisk
{
    public interface NoiseParams : IniSerializable
    {
        void ApplyToCS(ComputeShader cs);
        void ApplyToMaterial(Material mat);
    }
}