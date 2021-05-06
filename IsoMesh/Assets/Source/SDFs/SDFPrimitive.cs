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
    [SerializeField]
    private SDFPrimitiveType m_type;
    public SDFPrimitiveType Type => m_type;

    [SerializeField]
    private Vector4 m_data = new Vector4(1f, 1f, 1f, 0f);

    [SerializeField]
    protected SDFCombineType m_operation;
    public SDFCombineType Operation => m_operation;

    [SerializeField]
    protected bool m_flip = false;

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

    public override SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1)
    {
        // note: has room for six more floats (minbounds, maxbounds)
        return new SDFGPUData
        {
            Type = (int)m_type + 1,
            Data = m_data,
            Transform = transform.worldToLocalMatrix,
            CombineType = (int)m_operation,
            Flip = m_flip ? -1 : 1
        };
    }

    #region Create Menu Items

#if UNITY_EDITOR
    private static void CreateNewPrimitive(SDFPrimitiveType type)
    {
        GameObject selection = Selection.activeGameObject;

        GameObject child = new GameObject(type.ToString());
        child.transform.SetParent(selection.transform);
        child.transform.Reset();

        SDFPrimitive newPrimitive = child.AddComponent<SDFPrimitive>();
        newPrimitive.m_type = type;

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

#endif
    #endregion
}

public enum SDFPrimitiveType
{
    Sphere,
    Torus,
    Cuboid,
    BoxFrame
}
