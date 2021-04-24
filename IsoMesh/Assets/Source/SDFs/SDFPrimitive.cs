using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SDFPrimitive : SDFObject
{
    protected override void TryDeregister()
    {
        base.TryDeregister();
        
        Group?.Deregister(this);
    }

    protected override void TryRegister()
    {
        base.TryDeregister();

        Group?.Register(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color col = m_operation == SDFOp.SmoothSubtract ? Color.red : Color.blue;

        switch (m_type)
        {
            case SDFPrimitiveType.Cuboid:
                Gizmos.color = col;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, m_data.XYZ() * 2f);
                break;
            case SDFPrimitiveType.BoxFrame:
                Gizmos.color = col;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, m_data.XYZ() * 2f);
                break;
            default:
                Handles.color = col;
                Handles.matrix = transform.localToWorldMatrix;
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, m_data.x);
                break;
        }
    }

#endif

    [SerializeField]
    private SDFPrimitiveType m_type;
    public SDFPrimitiveType Type
    {
        get => m_type;
        set => m_type = value;
    }
    
    [SerializeField]
    private Vector4 m_data = new Vector4(1f, 1f, 1f, 0f);
    public Vector4 Data => m_data;

    [SerializeField]
    private bool m_flip = false;
    public bool Flip => m_flip;

    public GPUData GetGPUData()
    {
        return new GPUData
        {
            Type = (int)m_type,
            Data = m_data,
            Transform = transform.worldToLocalMatrix,
            Operation = (int)m_operation,
            Flip = m_flip ? -1 : 1
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct GPUData
    {
        public static int Stride => sizeof(int) * 3 + sizeof(float) * 4 + sizeof(float) * 16;

        // note: would a Matrix3x3 be enough?

        public int Type;
        public Vector4 Data;
        public Matrix4x4 Transform;
        public int Operation;
        public int Flip;

        public override string ToString() =>
            $"{(SDFPrimitiveType)Type}: Data = {Data}, Position = {Transform.ExtractTranslation()}";
    }

    #region Create Menu Items

    private static void CreateNewPrimitive(SDFPrimitiveType type)
    {
        GameObject selection = Selection.activeGameObject;

        GameObject child = new GameObject(type.ToString());
        child.transform.SetParent(selection.transform);
        child.transform.Reset();

        SDFPrimitive newPrimitive = child.AddComponent<SDFPrimitive>();
        newPrimitive.Type = type;

        Selection.activeGameObject = child;
    }

    [MenuItem("GameObject/SDFs/Sphere", false, priority: 2)]
    private static void CreateSphere(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Sphere);

    [MenuItem("GameObject/SDFs/Cuboid", false, priority: 2)]
    private static void CreateCuboid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cuboid);

    [MenuItem("GameObject/SDFs/Torus", false, priority: 2)]
    private static void CreateTorus(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Torus);

    [MenuItem("GameObject/SDFs/Frame", false, priority: 2)]
    private static void CreateFrame(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.BoxFrame);

    #endregion
}

public enum SDFPrimitiveType
{
    Sphere,
    Torus,
    Cuboid,
    BoxFrame
}
