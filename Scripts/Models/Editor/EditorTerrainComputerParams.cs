using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;
using static Monobelisk.Editor.EditorPrefsNames;
using System.IO;

namespace Monobelisk.Editor
{
    [CustomEditor(typeof(TerrainComputerParams))]
    public class EditorTerrainComputerParams : UnityEditor.Editor
    {
        private static readonly string[] NOISE_LIST =
        {
            "Swiss Folded Mountains",
            "Jordan Hills",
            "Perlin (tile mask)",
            "Perlin (terrain bumps)",
            "IQ Hills",
            "Rocky Terrain",
            "Swiss Faults",
            "Perlin Dunes",
            "Mountain Variation",
            "Colored Variation",
            "Swiss Dunes",
            "Mountain Base",
            "Hill Base"
        };

        private TerrainComputerParams p;
        private bool showNoiseEditor;
        private int noiseIndex;
        private RenderTexture noisePreview;
        private ComputeShader cs;
        private int previewMapPixelX;
        private int previewMapPixelY;
        private float previewScale;
        private bool shouldDispatchCS = true;
        //private Mod mod;

        private void OnEnable()
        {
            noisePreview = new RenderTexture(2048, 2048, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            noisePreview.enableRandomWrite = true;
            noisePreview.Create();
            shouldDispatchCS = true;
            //mod = ModManager.Instance.GetModFromGUID(NaturalTerrains.GUID);

            cs = Resources.Load<ComputeShader>("NoisePreview");
        }

        private void OnDisable()
        {
            if (noisePreview != null)
            {
                noisePreview.Release();
                noisePreview = null;
            }

            cs = null;
        }

        public override void OnInspectorGUI()
        {
            p = target as TerrainComputerParams;

            showNoiseEditor = EditorPrefs.GetBool(NOISE_EDITOR_VISIBLE, true);
            showNoiseEditor = Toggle(showNoiseEditor, "Noise editor");
            EditorPrefs.SetBool(NOISE_EDITOR_VISIBLE, showNoiseEditor);

            if (showNoiseEditor)
            {
                var lastNoiseIndex = EditorPrefs.GetInt(NOISE_EDITOR_LAST_INDEX, 0);
                if (lastNoiseIndex >= NOISE_LIST.Length) lastNoiseIndex = 0;

                noiseIndex = Popup("Noise map", lastNoiseIndex, NOISE_LIST);
                if (noiseIndex != lastNoiseIndex)
                {
                    EditorPrefs.SetInt(NOISE_EDITOR_LAST_INDEX, noiseIndex);
                    shouldDispatchCS = true;
                }

                var c1 = DrawNoiseParamEditor(noiseIndex);
                var c2 = DrawPreviewSettings();

                if (GUILayout.Button("Serialize"))
                {
                    var lastDir = EditorPrefs.GetString("monobelisk_interestingterrains_lastinipath", "");
                    var dir = EditorUtility.SaveFilePanel("Save noise params to INI", lastDir, "interesting_terrains.txt", "txt");

                    if (dir.Length != 0)
                    {
                        EditorPrefs.SetString("monobelisk_interestingterrains_lastinipath", dir);
                        var content = p.ToIni();
                        File.WriteAllText(dir, content);
                        AssetDatabase.Refresh();
                    }
                }

                if (c1 || c2)
                {
                    shouldDispatchCS = true;
                }
            }

            //EndFoldoutHeaderGroup();

            if (shouldDispatchCS)
            {
                EditorUtility.SetDirty(p);
            }

            DispatchCS();
        }

        private bool DrawPreviewSettings()
        {
            var changed = false;

            BeginVertical(EditorStyles.helpBox);

            LabelField("Preview window", EditorStyles.boldLabel);

            var mpx = EditorPrefs.GetInt(NOISE_PREVIEW_X, TerrainHelper.defaultMapPixelX);
            var mpy = EditorPrefs.GetInt(NOISE_PREVIEW_Y, TerrainHelper.defaultMapPixelY);

            var _mpx = IntSlider("Map pixel X", mpx, TerrainHelper.minMapPixelX, TerrainHelper.maxMapPixelX);
            var _mpy = IntSlider("Map pixel Y", mpy, TerrainHelper.minMapPixelY, TerrainHelper.maxMapPixelY);

            /*if (GUILayout.Button("Get from player GPS"))
            {
                _mpx = DaggerfallWorkshop.PlayerGPS.
            }*/

            var scale = EditorPrefs.GetFloat(NOISE_PREVIEW_SCALE, 1f);
            var pixelSize = Monobelisk.Utility.GetWorldPixelSize();
            var scaleMax = Mathf.Max(pixelSize.x, pixelSize.y);
            var scaleMin = Monobelisk.Utility.OneMeterInPixel() * 10f;
            var _scale = Slider("Scale (1.0 = 1x1 terrain tiles)", scale, scaleMin, scaleMax);

            var prevR = EditorPrefs.GetBool(NOISE_PREVIEW_R, true);
            var prevG = EditorPrefs.GetBool(NOISE_PREVIEW_G, true);
            var prevB = EditorPrefs.GetBool(NOISE_PREVIEW_B, true);
            var prevA = EditorPrefs.GetBool(NOISE_PREVIEW_A, true);

            LabelField("Preview colors");
            //BeginHorizontal();
            var pr = Toggle("R", prevR);
            var pg = Toggle("G", prevG);
            var pb = Toggle("B", prevB);
            var pa = Toggle("A", prevA);
            //EndHorizontal();

            if (mpx != _mpx || mpy != _mpy || scale != _scale || pr != prevR || pg != prevG || pb != prevB || pa != prevA)
            {
                mpx = _mpx;
                mpy = _mpy;
                scale = _scale;
                EditorPrefs.SetInt(NOISE_PREVIEW_X, mpx);
                EditorPrefs.SetInt(NOISE_PREVIEW_Y, mpy);
                EditorPrefs.SetFloat(NOISE_PREVIEW_SCALE, scale);

                EditorPrefs.SetBool(NOISE_PREVIEW_R, pr);
                EditorPrefs.SetBool(NOISE_PREVIEW_G, pg);
                EditorPrefs.SetBool(NOISE_PREVIEW_B, pb);
                EditorPrefs.SetBool(NOISE_PREVIEW_A, pa);

                changed = true;
            }

            EndVertical();

            previewMapPixelX = mpx;
            previewMapPixelY = mpy;
            previewScale = scale;

            return changed;
        }

        public override bool HasPreviewGUI()
        {
            return showNoiseEditor;
        }

        public override void OnPreviewSettings()
        {
            LabelField(NOISE_LIST[noiseIndex], EditorStyles.boldLabel);
        }

        public override void DrawPreview(Rect previewArea)
        {
            var isWide = previewArea.width > previewArea.height;
            var isTall = previewArea.height > previewArea.width;

            var r = new Rect(previewArea);

            if (isWide)
            {
                var height = previewArea.height;
                var offset = (previewArea.width - height) * 0.5f;
                r.width = height;
                r.x += offset;
            }

            if (isTall)
            {
                var width = previewArea.width;
                var offset = (previewArea.height - width) * 0.5f;
                r.height = width;
                r.y += offset;
            }

            GUI.DrawTexture(r, noisePreview);
        }

        private bool DrawNoiseParamEditor(int i)
        {
            if (i == SWISS_FOLDED)
            {
                return SwissTurbulenceEditor.Draw(p.swissFolded);
            }
            if (i == JORDAN_FOLDED)
            {
                return JordanTurbulenceEditor.Draw(p.jordanFolded);
            }
            if (i == PERLIN_TILE)
            {
                return PerlinEditor.Draw(p.perlinTile);
            }
            if (i == PERLIN_BUMP)
            {
                return PerlinEditor.Draw(p.perlinBump);
            }
            if (i == IQ_MOUNTAIN)
            {
                return SwissTurbulenceEditor.Draw(p.iqMountain);
            }
            if (i == SWISS_CELL)
            {
                return SwissTurbulenceEditor.Draw(p.swissCell);
            }
            if (i == SWISS_FAULTS)
            {
                return SwissTurbulenceEditor.Draw(p.swissFaults);
            }
            if (i == PERLIN_DUNE)
            {
                return PerlinEditor.Draw(p.perlinDune);
            }
            if (i == MNT_VAR)
            {
                return PerlinEditor.Draw(p.mntVar);
            }
            if (i == COLOR_VAR)
            {
                return PerlinEditor.Draw(p.colorVar);
            }
            if (i == SWISS_DUNE)
            {
                return SwissTurbulenceEditor.Draw(p.swissDune);
            }
            if (i == MOUNTAIN_BASE)
            {
                return SwissTurbulenceEditor.Draw(p.mountainBase);
            }
            if (i == HILL_BASE)
            {
                return PerlinEditor.Draw(p.hillBase);
            }

            return false;
        }

        private void DispatchCS()
        {
            if (!shouldDispatchCS)
                return;

            shouldDispatchCS = false;
            p.ApplyToCS(cs);
            var k = cs.FindKernel(GetCurrentNoiseName());
            uint x, y, z;
            cs.GetKernelThreadGroupSizes(k, out x, out y, out z);
            var tSize = Monobelisk.Utility.GetTerrainVertexSize();
            var csResolution = noisePreview.width;

            var prevWeights = new Vector4
                (
                    EditorPrefs.GetBool(NOISE_PREVIEW_R, true) ? 1 : 0,
                    EditorPrefs.GetBool(NOISE_PREVIEW_G, true) ? 1 : 0,
                    EditorPrefs.GetBool(NOISE_PREVIEW_B, true) ? 1 : 0,
                    EditorPrefs.GetBool(NOISE_PREVIEW_A, true) ? 1 : 0
                );

            cs.SetFloat("originalHeight", Monobelisk.Utility.GetOriginalTerrainHeight());
            cs.SetFloat("newHeight", Constants.TERRAIN_HEIGHT);
            cs.SetInt("heightmapResolution", csResolution);
            cs.SetVector("terrainPosition", Monobelisk.Utility.GetTerrainVertexPosition(previewMapPixelX, previewMapPixelY));
            cs.SetVector("terrainSize", new Vector2(tSize, tSize) * previewScale);
            cs.SetVector("worldSize", Monobelisk.Utility.GetWorldVertexSize());
            cs.SetVector("prevWeights", prevWeights);
            cs.SetTexture(k, "Result", noisePreview);

            cs.Dispatch(k, csResolution / (int)x, csResolution / (int)y, (int)z);
        }

        private string GetCurrentNoiseName()
        {
            if (noiseIndex == SWISS_FOLDED)
            {
                return p.swissFolded.name;
            }
            if (noiseIndex == JORDAN_FOLDED)
            {
                return p.jordanFolded.name;
            }
            if (noiseIndex == PERLIN_TILE)
            {
                return p.perlinTile.name;
            }
            if (noiseIndex == PERLIN_BUMP)
            {
                return p.perlinBump.name;
            }
            if (noiseIndex == IQ_MOUNTAIN)
            {
                return p.iqMountain.name;
            }
            if (noiseIndex == SWISS_CELL)
            {
                return p.swissCell.name;
            }
            if (noiseIndex == SWISS_FAULTS)
            {
                return p.swissFaults.name;
            }
            if (noiseIndex == PERLIN_DUNE)
            {
                return p.perlinDune.name;
            }
            if (noiseIndex == MNT_VAR)
            {
                return p.mntVar.name;
            }
            if (noiseIndex == COLOR_VAR)
            {
                return p.colorVar.name;
            }
            if (noiseIndex == SWISS_DUNE)
            {
                return p.swissDune.name;
            }
            if (noiseIndex == MOUNTAIN_BASE)
            {
                return p.mountainBase.name;
            }
            if (noiseIndex == HILL_BASE)
            {
                return p.hillBase.name;
            }

            return "";
        }

        private static readonly int SWISS_FOLDED = 0;
        private static readonly int JORDAN_FOLDED = 1;
        private static readonly int PERLIN_TILE = 2;
        private static readonly int PERLIN_BUMP = 3;
        private static readonly int IQ_MOUNTAIN = 4;
        private static readonly int SWISS_CELL = 5;
        private static readonly int SWISS_FAULTS = 6;
        private static readonly int PERLIN_DUNE = 7;
        private static readonly int MNT_VAR = 8;
        private static readonly int COLOR_VAR = 9;
        private static readonly int SWISS_DUNE = 10;
        private static readonly int MOUNTAIN_BASE = 11;
        private static readonly int HILL_BASE = 12;
    }
}