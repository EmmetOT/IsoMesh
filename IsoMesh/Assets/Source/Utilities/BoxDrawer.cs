using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    public static class BoxDrawer // note: for some reason in current unity version this stuff is only visible in wireframe rendering modes
    {
        private static Mesh s_mesh;
        private static Material s_material;
        private static MaterialPropertyBlock s_block = new MaterialPropertyBlock();
        private static int s_lastDrawnFrame = -1;
        private static bool s_listChanged = false;

        private static readonly List<Matrix4x4> s_matrices = new List<Matrix4x4>();
        private static Matrix4x4[] s_matricesArray;

        private static readonly List<Vector3> s_vertices = new List<Vector3>
        {
            new Vector3 (-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3 (-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
        };

        private static readonly int[] s_indices =
        {
            0, 1, 1, 2, 2, 3, 3, 0, // face1
            4, 5, 5, 6, 6, 7, 7, 4, // face2
            0, 4, 1, 5, 2, 6, 3, 7  // interconnects
        };

        static BoxDrawer()
        {
            s_mesh = new Mesh();
            s_mesh.SetVertices(s_vertices);
            s_mesh.SetIndices(s_indices, MeshTopology.Lines, 0);

            s_material = new Material(Shader.Find("Standard"))
            {
                enableInstancing = true
            };
        }

        public static void Add(IEnumerable<Bounds> boundsCollection)
        {
            foreach (Bounds bounds in boundsCollection)
                Add(bounds.center, bounds.size);
        }

        public static void Add(Bounds bounds) => Add(bounds.center, bounds.size);

        public static void Add(Vector3 centre, Vector3 size)
        {
            s_matrices.Add(Matrix4x4.Translate(centre) * Matrix4x4.Scale(size));
            s_listChanged = true;
        }

        public static void Clear()
        {
            s_matrices.Clear();
            s_listChanged = true;
        }

        public static void Draw()
        {
            if (!SystemInfo.supportsInstancing)
            {
                Debug.LogError("[BVH] Cannot render BVH. Mesh instancing not supported by system");
                return;
            }

            if (s_matrices.IsNullOrEmpty() || s_lastDrawnFrame == Time.frameCount)
                return;

            s_lastDrawnFrame = Time.frameCount;

            if (s_listChanged)
            {
                s_matricesArray = s_matrices.ToArray();
                s_listChanged = false;
            }

            Graphics.DrawMeshInstanced(s_mesh, 0, s_material, s_matricesArray, Mathf.Min(s_matricesArray.Length, 1023), s_block, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }
    }
}