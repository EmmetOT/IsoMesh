using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    public class SDFMesh : SDFObject
    {
        public int ID => m_asset.GetInstanceID();

        [SerializeField]
        private SDFMeshAsset m_asset;
        public SDFMeshAsset Asset => m_asset;

        public override IEnumerable<Vector3> Corners
        {
            get
            {
                if (!m_asset)
                    yield break;

                Vector3 minBounds = m_asset.MinBounds - Vector3.one * m_asset.Padding;
                Vector3 maxBounds = m_asset.MaxBounds - Vector3.one * m_asset.Padding;

                yield return new Vector3(minBounds.x, minBounds.y, minBounds.z);
                yield return new Vector3(maxBounds.x, minBounds.y, minBounds.z);
                yield return new Vector3(minBounds.x, maxBounds.y, minBounds.z);
                yield return new Vector3(maxBounds.x, maxBounds.y, minBounds.z);
                yield return new Vector3(minBounds.x, minBounds.y, maxBounds.z);
                yield return new Vector3(maxBounds.x, minBounds.y, maxBounds.z);
                yield return new Vector3(minBounds.x, maxBounds.y, maxBounds.z);
                yield return new Vector3(maxBounds.x, maxBounds.y, maxBounds.z);
            }
        }

        public override Bounds CalculateBounds()
        {
            if (!m_asset)
                return new Bounds(transform.position, Vector3.zero);

            return base.CalculateBounds();
        }

        protected override void TryRegister()
        {
            if (!m_asset)
                return;

            base.TryRegister();

            if (Group)
                Group.Register(this);
        }

        protected override void TryDeregister()
        {
            if (!m_asset)
                return;

            base.TryRegister();

            if (Group)
                Group.Deregister(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (Group && !Group.IsRegistered(this) && m_asset)
                TryRegister();
        }

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex, int uvStartIndex = -1)
        {
            return new SDFGPUData
            {
                Type = 0,
                Data = new Vector4(m_asset.Size, sampleStartIndex, uvStartIndex),
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                MinBounds = AABB.min,
                MaxBounds = AABB.max,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing)
            };
        }

        #region Create Menu Items

        [MenuItem("GameObject/SDFs/Mesh", false, priority: 2)]
        private static void CreateSDFMesh(MenuCommand menuCommand)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject("Mesh");
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFMesh newMesh = child.AddComponent<SDFMesh>();

            Selection.activeGameObject = child;
        }

        #endregion

    }
}