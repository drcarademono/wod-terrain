using UnityEngine;

namespace Monobelisk.Editor
{
    public static class Utility
    {
        public static void ApplyFieldChange(ref float field, float newValue, ref bool changed)
        {
            if (field != newValue)
            {
                field = newValue;
                changed = true;
            }
        }

        public static void ApplyFieldChange(ref int field, int newValue, ref bool changed)
        {
            if (field != newValue)
            {
                field = newValue;
                changed = true;
            }
        }

        public static void ApplyFieldChange(ref Vector2 field, Vector2 newValue, ref bool changed)
        {
            if (field != newValue)
            {
                field = newValue;
                changed = true;
            }
        }
    }
}