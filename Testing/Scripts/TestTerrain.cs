using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Monobelisk
{
    [ExecuteInEditMode]
    public class TestTerrain : MonoBehaviour
    {
        public int tileCount = 1;
        public float warp = 10;

        #region Private properties
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _derivMap;
        public Vector2 offset = Vector2.zero;
        public Mesh _terrainMesh;
        public TerrainComputerParams heightmapParams;

        private float terrainWorldSize = 819.2f;
        private float terrainWorldHeight = 5000f;
        private int terrainHeightmapResolution = 129;
        #endregion

        #region Getters
        public MeshFilter MeshFilter {
            get
            {
                if (_meshFilter == null)
                    _meshFilter = GetComponent<MeshFilter>();

                return _meshFilter;
            }
        }
        public MeshRenderer MeshRenderer
        {
            get
            {
                if (_meshRenderer == null)
                    _meshRenderer = GetComponent<MeshRenderer>();

                return _meshRenderer;
            }
        }
        public Mesh TerrainMesh
        {
            get
            {
                var vCount = (terrainHeightmapResolution * tileCount);
                vCount = vCount * vCount;
                if (_terrainMesh == null || _terrainMesh.vertexCount != vCount) 
                    GenerateTerrainMesh();

                return _terrainMesh;
            }
        }
        public Texture2D DerivMap
        {
            get
            {
                if (_derivMap == null)
                {
#if UNITY_EDITOR
                    _derivMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Mods/Natural Terrains/Assets/Maps/daggerfall_deriv_map.psd");
#else
                    _derivMap = new Texture2D(1000, 500);
                    _derivMap.Apply();
#endif
                }

                return _derivMap;
            }
        }
#endregion

        private void Update() 
        {
            if (Application.isPlaying)
            {
                enabled = false;
                return;
            }

            MeshFilter.sharedMesh = TerrainMesh;
            heightmapParams.ApplyToMaterial(MeshRenderer.sharedMaterial);
            MeshRenderer.sharedMaterial.SetFloat("newHeight", terrainWorldHeight);
            MeshRenderer.sharedMaterial.SetFloat("tileSize", terrainWorldSize * tileCount);
            MeshRenderer.sharedMaterial.SetVector("offset", new Vector4(offset.x, offset.y, 0, 0));
            MeshRenderer.sharedMaterial.SetInt("res", terrainHeightmapResolution * tileCount);
            MeshRenderer.sharedMaterial.SetFloat("warp", warp);
            MeshRenderer.sharedMaterial.SetTexture("_DerivMap", DerivMap);
        }

        void GenerateTerrainMesh()
        {
            _terrainMesh = new Mesh();
            _terrainMesh.indexFormat = IndexFormat.UInt32;

            int res = terrainHeightmapResolution * tileCount;
            float size = terrainWorldSize / terrainHeightmapResolution; 

            List<Vector3> verts = new List<Vector3>(res * res);
            List<Vector3> norms = new List<Vector3>(res * res);
            List<Vector2> uvs = new List<Vector2>(res * res);
            List<int> tris = new List<int>();

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    int i0 = (x + 0) + (y + 0) * res;
                    int i1 = (x + 0) + (y + 1) * res;
                    int i2 = (x + 1) + (y + 0) * res;
                    int i3 = (x + 1) + (y + 1) * res;

                    if (x < res - 1 && y < res - 1)
                        tris.AddRange(new int[] { i0, i2, i1, i2, i3, i1 });

                    Vector2 uv = new Vector2(x, y) / new Vector2(res - 1, res - 1);

                    verts.Add(new Vector3(size * x, 0, size * y));
                    norms.Add(Vector3.up);
                    uvs.Add(uv); 
                }
            }

            /*_terrainMesh.SetVertices(verts);
            _terrainMesh.SetTriangles(tris.ToArray(), 0);
            _terrainMesh.SetUVs(0, uvs);*/
            _terrainMesh.vertices = verts.ToArray();
            _terrainMesh.triangles = tris.ToArray();
            _terrainMesh.normals = norms.ToArray();
            _terrainMesh.uv = uvs.ToArray();

            var b = _terrainMesh.bounds;
            var c = b.center;
            c.y = 2500;
            _terrainMesh.bounds = new Bounds(c, new Vector3(res, 5000, res));
            /*
            _terrainMesh.RecalculateBounds();
            _terrainMesh.RecalculateNormals();
            _terrainMesh.RecalculateTangents();*/
        }
    }
}
