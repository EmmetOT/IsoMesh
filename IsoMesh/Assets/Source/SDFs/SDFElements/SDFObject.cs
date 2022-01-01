using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    [ExecuteInEditMode]
    public abstract class SDFObject : SDFElement
    {
        /// <summary>
        /// A pretty arbitrary number, this is multiplied with the smoothing and added to the bounds of the object, because
        /// smoothing makes bounds all mushy and unpredictable.
        /// </summary>
        private const float SMOOTHING_TO_BOUNDS_COEF = 0.33333f;

        private Bounds m_aabb;
        public Bounds AABB => m_aabb;

        [SerializeField]
        protected SDFCombineType m_operation;
        public SDFCombineType Operation => m_operation;

        [SerializeField]
        protected bool m_flip = false;

        public abstract IEnumerable<Vector3> Corners { get; }

        protected override void OnEnable()
        {
            base.OnEnable();

            m_aabb = CalculateBounds();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            m_aabb = CalculateBounds();
        }

        protected override void OnDirty()
        {
            base.OnDirty();

            m_aabb = CalculateBounds();
        }

        public virtual Bounds CalculateBounds()
        {
            IEnumerable<Vector3> corners = Corners;
            Vector3 min = Vector3.one * float.PositiveInfinity;
            Vector3 max = Vector3.one * float.NegativeInfinity;

            foreach (Vector3 corner in corners)
            {
                Vector3 transformed = transform.TransformPoint(corner);
                min = Utils.Min(min, transformed);
                max = Utils.Max(max, transformed);
            }

            Vector3 smoothing = m_smoothing * SMOOTHING_TO_BOUNDS_COEF * Vector3.one;

            return new Bounds((min + max) * 0.5f, (max - min) + smoothing);
        }


#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            Bounds aabb = AABB;

            Handles.color = Color.yellow;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube(aabb.center, aabb.size);
        }
#endif
    }
}