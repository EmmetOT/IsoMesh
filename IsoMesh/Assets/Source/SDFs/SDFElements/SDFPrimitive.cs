using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    [ExecuteInEditMode]
    public class SDFPrimitive : SDFObject
    {
        [SerializeField]
        private SDFPrimitiveType m_type;
        public SDFPrimitiveType Type => m_type;

        [SerializeField]
        private Vector4 m_data = new Vector4(1f, 1f, 1f, 0f);

        private static IEnumerable<Vector3> EnumerateCorners(float x, float y, float z)
        {
            yield return new Vector3(-x, -y, -z);
            yield return new Vector3(x, -y, -z);
            yield return new Vector3(-x, y, -z);
            yield return new Vector3(x, y, -z);
            yield return new Vector3(-x, -y, z);
            yield return new Vector3(x, -y, z);
            yield return new Vector3(-x, y, z);
            yield return new Vector3(x, y, z);
        }

        private IEnumerable<Vector3> UntransformedCubeBoundsCorners => EnumerateCorners(m_data.x + m_data.w, m_data.y + m_data.w, m_data.z + m_data.w);
        private IEnumerable<Vector3> UntransformedBoxFrameBoundsCorners => EnumerateCorners(m_data.x, m_data.y, m_data.z);
        private IEnumerable<Vector3> UntransformedTorusBoundsCorners => EnumerateCorners(m_data.x + m_data.y, m_data.y, m_data.x + m_data.y);
        private IEnumerable<Vector3> UntransformedCylinderBoundsCorners => EnumerateCorners(m_data.x, m_data.y, m_data.x);
        private IEnumerable<Vector3> UntransformedSphereBoundsCorners => EnumerateCorners(m_data.x, m_data.x, m_data.x);

        public override IEnumerable<Vector3> Corners => m_type switch
        {
            SDFPrimitiveType.Torus => UntransformedTorusBoundsCorners,
            SDFPrimitiveType.Cuboid => UntransformedCubeBoundsCorners,
            SDFPrimitiveType.BoxFrame => UntransformedBoxFrameBoundsCorners,
            SDFPrimitiveType.Cylinder => UntransformedCylinderBoundsCorners,
            _ => UntransformedSphereBoundsCorners,
        };

        public override Bounds CalculateBounds()
        {
            // small optimization since the bounds calc for spheres is so simple
            if (m_type == SDFPrimitiveType.Sphere)
                return new Bounds(transform.position, (Vector3.one * m_data.x * 2f));

            return base.CalculateBounds();
        }

        protected override void TryDeregister()
        {
            base.TryDeregister();

            if (Group)
                Group.Deregister(this);
        }

        protected override void TryRegister()
        {
            base.TryDeregister();

            if (Group)
                Group.Register(this);
        }

        public float SphereRadius
        {
            get
            {
                if (m_type == SDFPrimitiveType.Sphere)
                    return m_data.x;

                return 0f;
            }
        }

        public void SetSphereRadius(float radius)
        {
            if (m_type == SDFPrimitiveType.Sphere)
            {
                m_data = m_data.SetX(Mathf.Max(0f, radius));
                SetDirty();
            }
        }

        // note: has room for six more floats (minbounds, maxbounds)
        public override SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1)
        {
            return new SDFGPUData
            {
                Type = (int)m_type + 1,
                Data = m_data,
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing),
                MinBounds = AABB.min,
                MaxBounds = AABB.max
            };
        }

        #region Create Menu Items

#if UNITY_EDITOR
        private static void CreateNewPrimitive(SDFPrimitiveType type, Vector4 startData)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject(type.ToString());
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFPrimitive newPrimitive = child.AddComponent<SDFPrimitive>();
            newPrimitive.m_type = type;
            newPrimitive.m_data = startData;
            newPrimitive.SetDirty();

            Selection.activeGameObject = child;
        }

        [MenuItem("GameObject/SDFs/Sphere", false, priority: 2)]
        private static void CreateSphere(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Sphere, new Vector4(1f, 0f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Cuboid", false, priority: 2)]
        private static void CreateCuboid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cuboid, new Vector4(1f, 1f, 1f, 0f));

        [MenuItem("GameObject/SDFs/Torus", false, priority: 2)]
        private static void CreateTorus(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Torus, new Vector4(1f, 0.5f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Frame", false, priority: 2)]
        private static void CreateFrame(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.BoxFrame, new Vector4(1f, 1f, 1f, 0.2f));

        [MenuItem("GameObject/SDFs/Cylinder", false, priority: 2)]
        private static void CreateCylinder(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cylinder, new Vector4(1f, 1f, 0f, 0f));

#endif
        #endregion
    }

    public enum SDFPrimitiveType
    {
        Sphere,
        Torus,
        Cuboid,
        BoxFrame,
        Cylinder
    }
}