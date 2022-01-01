using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    /// <summary>
    /// This class passes SDF data to an isosurface extraction compute shader and returns a mesh.
    /// This mesh can be passed directly to a material as a triangle and index buffer in 'Procedural' mode,
    /// or transfered to the CPU and sent to a MeshFilter in 'Mesh' mode.
    /// </summary>
    public partial class SDFGroupMeshGenerator : MonoBehaviour, ISDFGroupComponent
    {
        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]

        public struct KeyValue
        {
            public static int Stride => sizeof(uint) + sizeof(int);

            public uint Key;
            public int Value;

            public override string ToString() => $"[Key: {Key}, Value: {Value}]";
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]
        public struct OcttreeNode
        {
            public static int Stride => sizeof(float) * 5;

            public Vector3 Position;
            public float Width;
            public float Distance;

            public IEnumerable<Vector3> ChildCentres
            {
                get
                {
                    yield return Position + Width * 0.25f * (Vector3.left + Vector3.back + Vector3.down);
                    yield return Position + Width * 0.25f * (Vector3.right + Vector3.back + Vector3.down);

                    yield return Position + Width * 0.25f * (Vector3.left + Vector3.forward + Vector3.down);
                    yield return Position + Width * 0.25f * (Vector3.right + Vector3.forward + Vector3.down);

                    yield return Position + Width * 0.25f * (Vector3.left + Vector3.back + Vector3.up);
                    yield return Position + Width * 0.25f * (Vector3.right + Vector3.back + Vector3.up);

                    yield return Position + Width * 0.25f * (Vector3.left + Vector3.forward + Vector3.up);
                    yield return Position + Width * 0.25f * (Vector3.right + Vector3.forward + Vector3.up);
                }
            }

            public override string ToString() => $"[Position = {Position}, Width = {Width}, Distance = {Distance}]";
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]
        public struct CellData
        {
            public static int Stride => sizeof(int) + sizeof(float) * 3;

            public int VertexID;
            public Vector3 SurfacePoint;

            public bool HasSurfacePoint => VertexID >= 0;

            public override string ToString() => $"HasSurfacePoint = {HasSurfacePoint}" + (HasSurfacePoint ? $", SurfacePoint = {SurfacePoint}, VertexID = {VertexID}" : "");
        };

        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]
        public struct VertexData
        {
            public static int Stride => sizeof(int) * 2 + sizeof(float) * 6;

            public int Index;
            public int CellID;
            public Vector3 Vertex;
            public Vector3 Normal;

            public override string ToString() => $"Index = {Index}, CellID = {CellID}, Vertex = {Vertex}, Normal = {Normal}";
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]
        public struct TriangleData
        {
            public static int Stride => sizeof(int) * 3;

            public int P_1;
            public int P_2;
            public int P_3;

            public override string ToString() => $"P_1 = {P_1}, P_2 = {P_2}, P_3 = {P_3}";
        }

        [StructLayout(LayoutKind.Sequential)]
        [System.Serializable]
        public struct NewVertexData
        {
            public static int Stride => sizeof(int) + sizeof(float) * 6;

            public int Index;
            public Vector3 Vertex;
            public Vector3 Normal;

            public override string ToString() => $"Index = {Index}, Vertex = {Vertex}, Normal = {Normal}";
        }
    }
}