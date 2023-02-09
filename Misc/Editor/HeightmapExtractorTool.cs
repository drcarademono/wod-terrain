using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class HeightmapExtractorTool : EditorWindow
{
    [MenuItem("Monobelisk/Daggerfall Heightmap Extractor")]
    public static void ShowWindow()
    {
        GetWindow<HeightmapExtractorTool>();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Extract to PNG"))
        {
            var savePath = EditorUtility.SaveFilePanel("Destination path", "", "daggerfall_heightmap.png", "PNG");

            if (savePath != "")
            {
                var contentReader = DaggerfallUnity.Instance.ContentReader;
                byte[] heightMapArray = contentReader.WoodsFileReader.Buffer.Clone() as byte[];

                var tex = new Texture2D(1000, 500);
                var colors = heightMapArray.Select(b => new Color32(b, b, b, 255)).ToArray();
                tex.SetPixels32(colors);
                tex.Apply();

                var texBytes = tex.EncodeToPNG();
                File.WriteAllBytes(savePath, texBytes);
            }
        }
    }
}
