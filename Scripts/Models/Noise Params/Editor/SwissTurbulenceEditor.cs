using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

namespace Monobelisk.Editor
{
    public static class SwissTurbulenceEditor
    {
        public static bool Draw(SwissTurbulence p)
        {
            var changed = false;

            BeginVertical(EditorStyles.helpBox);

            int oct = IntSlider("Octaves", p.octaves, 1, 16);
            float freq = Slider("Frequency", p.frequency, 0.000001f, 0.5f);
            float amp = FloatField("Amplitude", p.amplitude);
            float lacun = FloatField("Lacunarity", p.lacunarity);
            float per = FloatField("Persistence", p.persistence);
            Vector2 off = Vector2Field("Offset", p.offset);
            float rOff = Slider("Ridge offset", p.ridgeOffset, 0, 1);
            float war = FloatField("Warp", p.warp);
            float mHeight = Slider("Max height", p.maxHeight, 0f, 5000f);

            Utility.ApplyFieldChange(ref p.octaves, oct, ref changed);
            Utility.ApplyFieldChange(ref p.frequency, freq, ref changed);
            Utility.ApplyFieldChange(ref p.amplitude, amp, ref changed);
            Utility.ApplyFieldChange(ref p.lacunarity, lacun, ref changed);
            Utility.ApplyFieldChange(ref p.persistence, per, ref changed);
            Utility.ApplyFieldChange(ref p.offset, off, ref changed);
            Utility.ApplyFieldChange(ref p.ridgeOffset, rOff, ref changed);
            Utility.ApplyFieldChange(ref p.warp, war, ref changed);
            Utility.ApplyFieldChange(ref p.maxHeight, mHeight, ref changed);

            EndVertical();

            return changed;
        }
    }
}