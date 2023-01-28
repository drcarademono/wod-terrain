using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

namespace Monobelisk.Editor
{
    public static class PerlinEditor
    {
        public static bool Draw(Perlin p)
        {
            var changed = false;

            BeginVertical(EditorStyles.helpBox);

            int oct = IntSlider("Octaves", p.octaves, 1, 12);
            float freq = FloatField("Frequency", p.frequency);
            float amp = FloatField("Amplitude", p.amplitude);
            float lacun = FloatField("Lacunarity", p.lacunarity);
            float per = FloatField("Persistence", p.persistence);
            Vector2 off = Vector2Field("Offset", p.offset);
            float mHeight = Slider("Max height", p.maxHeight, 0f, 5000f);

            Utility.ApplyFieldChange(ref p.octaves, oct, ref changed);
            Utility.ApplyFieldChange(ref p.frequency, freq, ref changed);
            Utility.ApplyFieldChange(ref p.amplitude, amp, ref changed);
            Utility.ApplyFieldChange(ref p.lacunarity, lacun, ref changed);
            Utility.ApplyFieldChange(ref p.persistence, per, ref changed);
            Utility.ApplyFieldChange(ref p.offset, off, ref changed);
            Utility.ApplyFieldChange(ref p.maxHeight, mHeight, ref changed);

            EndVertical();

            return changed;
        }
    }
}