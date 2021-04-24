using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteInEditMode]
public abstract class SDFObject : MonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private SDFGroup m_sdfGroup;
    public SDFGroup Group
    {
        get
        {
            if (!m_sdfGroup)
                m_sdfGroup = GetComponentInParent<SDFGroup>();

            return m_sdfGroup;
        }
    }

    [SerializeField]
    protected SDFOp m_operation;
    public SDFOp Operation => m_operation;
    
    private bool m_isDirty = false;
    private bool m_isOrderDirty = false;
    
    // used by attributes
    protected void SetDirty()
    {
        m_isDirty = true;
    }

    private int m_lastSeenSiblingIndex = -1;

    public bool IsDirty => m_isDirty;
    public bool IsOrderDirty => m_isOrderDirty;

    public void SetClean()
    {
        m_isDirty = false;
        transform.hasChanged = false;
        m_isOrderDirty = false;
    }

    protected virtual void Awake() => TryRegister();
    protected virtual void Reset() => TryRegister();
    protected virtual void OnEnable() => TryRegister();

    protected virtual void OnDisable() => TryDeregister();
    protected virtual void OnDestroy() => TryDeregister();

    protected virtual void OnValidate() => SetDirty();

    protected virtual void TryDeregister()
    {
        m_sdfGroup = GetComponentInParent<SDFGroup>();
        SetClean();
    }

    protected virtual void TryRegister()
    {
        m_lastSeenSiblingIndex = transform.GetSiblingIndex();

        m_sdfGroup = GetComponentInParent<SDFGroup>();
        SetDirty();
    }

    protected virtual void Update()
    {
        m_isDirty |= transform.hasChanged;

        int siblingIndex = transform.GetSiblingIndex();

        if (siblingIndex != m_lastSeenSiblingIndex)
        {
            if (m_lastSeenSiblingIndex != -1)
                m_isOrderDirty = true;
            
            m_lastSeenSiblingIndex = siblingIndex;
        }

        transform.hasChanged = false;
    }
}


public enum SDFOp
{
    SmoothMin, SmoothSubtract
}
