using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SDFMesh : SDFObject
{
    public int ID => m_asset.GetInstanceID();

    [SerializeField]
    private SDFMeshAsset m_asset;
    public SDFMeshAsset Asset => m_asset;

    protected override void TryRegister()
    {
        if (!m_asset)
            return;

        base.TryRegister();

        Group?.Register(this);
    }

    protected override void TryDeregister()
    {
        if (!m_asset)
            return;

        base.TryRegister();

        Group?.Deregister(this);
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
            Type = -1,
            Data = new Vector4(m_asset.Size, sampleStartIndex, uvStartIndex),
            Transform = transform.worldToLocalMatrix,
            Operation = (int)m_operation,
            Flip = m_flip ? -1 : 1,
            MinBounds = m_asset.MinBounds,
            MaxBounds = m_asset.MaxBounds
        };
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!m_asset)
            return;

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.matrix = transform.localToWorldMatrix;
        UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        UnityEditor.Handles.DrawWireCube(Vector3.zero, (m_asset.MaxBounds - m_asset.MinBounds));
    }
#endif

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
