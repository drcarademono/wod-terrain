using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

namespace Monobelisk.Editor
{
    public static class JordanTurbulenceEditor
    {
        public static bool Draw(JordanTurbulence p)
        {
            var changed = false;

            BeginVertical(EditorStyles.helpBox);

            int oct = IntSlider("Octaves", p.octaves, 1, 12);
            float freq = FloatField("Frequency", p.frequency);
            float amp = FloatField("Amplitude", p.amplitude);
            float lacun = FloatField("Lacunarity", p.lacunarity);
            float per = FloatField("Persistence", p.persistence);
            float per1 = FloatField("Persistence 1", p.persistence1);
            Vector2 off = Vector2Field("Offset", p.offset);
            float war0 = FloatField("Warp 0", p.warp0);
            float war = FloatField("Warp", p.warp);
            float dam0 = FloatField("Damp 0", p.damp0);
            float dam = FloatField("Damp", p.damp);
            float damScale = FloatField("Damp scale", p.damp_scale);
            float mHeight = Slider("Max height", p.maxHeight, 0f, 5000f);

            Utility.ApplyFieldChange(ref p.octaves, oct, ref changed);
            Utility.ApplyFieldChange(ref p.frequency, freq, ref changed);
            Utility.ApplyFieldChange(ref p.amplitude, amp, ref changed);
            Utility.ApplyFieldChange(ref p.lacunarity, lacun, ref changed);
            Utility.ApplyFieldChange(ref p.persistence, per, ref changed);
            Utility.ApplyFieldChange(ref p.persistence1, per1, ref changed);
            Utility.ApplyFieldChange(ref p.offset, off, ref changed);
            
            Utility.ApplyFieldChange(ref p.warp, war, ref changed);
            Utility.ApplyFieldChange(ref p.warp0, war0, ref changed);
            Utility.ApplyFieldChange(ref p.damp, dam, ref changed);
            Utility.ApplyFieldChange(ref p.damp0, dam0, ref changed);
            Utility.ApplyFieldChange(ref p.damp_scale, damScale, ref changed);
            Utility.ApplyFieldChange(ref p.maxHeight, mHeight, ref changed);

            EndVertical();

            return changed;
        }
    }
}