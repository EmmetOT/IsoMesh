using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Sirenix.OdinInspector;
using System.Linq;

/// <summary>
/// An SDF group is a collection of sdf primitives, meshes, and operations which mutually interact.
/// This class is not responsible for rendering the result or indeed doing anything with it. Instead, it dispatches
/// the resulting buffers to SDF Components. These components must be a child of the group and implement the ISDFComponent interface.
/// </summary>
[ExecuteInEditMode]
public class SDFGroup : MonoBehaviour
{
    #region Fields and Properties

    public const float MIN_SMOOTHING = 0.00001f;

    /// <summary>
    /// Whether this group is actively updating.
    /// </summary>
    public bool IsRunning => m_isRunning;

    [SerializeField]
    private bool m_isRunning = true;
    public void SetRunning(bool isRunning) => m_isRunning = isRunning;

    private bool m_isReady = false;
    private bool m_forceUpdateNextFrame = false;

    /// <summary>
    /// Whether this group is fully set up, e.g. all the buffers are created.
    /// </summary>
    public bool IsReady => m_isReady;
    
    [SerializeField]
    private float m_smoothing = 0.2f;
    public float Smoothing => m_smoothing;
    public void SetSmoothing(float smoothing)
    {
        m_smoothing = smoothing;
        OnSettingsChanged();
    }
    
    [SerializeField]
    private float m_normalSmoothing = 0.015f;
    public float NormalSmoothing => m_normalSmoothing;
    public void SetNormalSmoothing(float normalSmoothing)
    {
        m_normalSmoothing = normalSmoothing;
        OnSettingsChanged();
    }
    
    private List<ISDFGroupComponent> m_sdfComponents = new List<ISDFGroupComponent>();

    private ComputeBuffer m_primitiveDataBuffer;
    public ComputeBuffer PrimitivesBuffer => m_primitiveDataBuffer;

    private ComputeBuffer m_localMeshDataBuffer;

    private ComputeBuffer m_settingsBuffer;
    public ComputeBuffer SettingsBuffer => m_settingsBuffer;

    private Settings[] m_settingsArray = new Settings[1];

    private List<SDFPrimitive> m_sdfPrimitives = new List<SDFPrimitive>();
    private List<SDFMesh> m_localSDFMeshes = new List<SDFMesh>();

    private static readonly List<SDFMesh> m_globalSDFMeshes = new List<SDFMesh>();
    private static readonly Dictionary<int, int> m_meshSdfSampleStartIndices = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> m_meshSdfUVStartIndices = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> m_meshCounts = new Dictionary<int, int>();
    private static readonly List<float> m_meshSamples = new List<float>();
    private static readonly List<float> m_meshPackedUVs = new List<float>();

    private static ComputeBuffer m_meshSamplesBuffer;
    private static ComputeBuffer m_meshPackedUVsBuffer;

    private readonly List<SDFPrimitive.GPUData> m_primitivesData = new List<SDFPrimitive.GPUData>();
    private readonly List<SDFMesh.GPUData> m_localMeshData = new List<SDFMesh.GPUData>();

    public bool IsEmpty => m_localSDFMeshes.IsNullOrEmpty() && m_sdfPrimitives.IsNullOrEmpty();//

    // dirty flags. we only need one for the primitives, but two for the meshes. this is because
    // i want to avoid doing a 'full update' of the meshes unless i really need to.

    private static bool m_isGlobalMeshDataDirty = true;

    private bool m_isPrimitivesListDirty = true;
    private bool m_isLocalMeshDataDirty = true;

    #endregion

    #region Registration

    public void Register(SDFPrimitive sdfObject)
    {
        if (m_sdfPrimitives.Contains(sdfObject))
            return;

        bool wasEmpty = IsEmpty;

        m_sdfPrimitives.Add(sdfObject);
        m_isPrimitivesListDirty = true;

        // this is almost certainly overkill, but i like the kind of guaranteed stability
        ClearNulls(m_sdfPrimitives);

        if (wasEmpty && !IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnNotEmpty();

        RequestUpdate();
    }

    public void Deregister(SDFPrimitive sdfObject)
    {
        bool wasEmpty = IsEmpty;

        if (m_sdfPrimitives.Remove(sdfObject))
            m_isPrimitivesListDirty = true;

        // this is almost certainly overkill, but i like the kind of guaranteed stability
        ClearNulls(m_sdfPrimitives);

        if (!wasEmpty && IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnEmpty();

        RequestUpdate();
    }

    public void Register(SDFMesh sdfMesh)
    {
        if (m_localSDFMeshes.Contains(sdfMesh))
            return;

        // check if this is a totally new mesh that no group has seen
        if (!m_globalSDFMeshes.Contains(sdfMesh))
        {
            m_globalSDFMeshes.Add(sdfMesh);
            m_isGlobalMeshDataDirty = true;
        }

        // keep track of how many groups contain a local reference to this sdfmesh
        if (!m_meshCounts.ContainsKey(sdfMesh.ID))
            m_meshCounts.Add(sdfMesh.ID, 0);

        m_meshCounts[sdfMesh.ID]++;

        bool wasEmpty = IsEmpty;

        m_localSDFMeshes.Add(sdfMesh);

        // this is almost certainly overkill, but i like the kind of guaranteed stability
        ClearNulls(m_localSDFMeshes);

        if (wasEmpty && !IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnNotEmpty();

        m_isLocalMeshDataDirty = true;

        RequestUpdate();
    }

    public void Deregister(SDFMesh sdfMesh)
    {
        if (!m_localSDFMeshes.Remove(sdfMesh))
            return;

        m_isLocalMeshDataDirty = true;

        // if this was the only group referencing this sdfmesh, we can remove it from the global buffer too
        if (m_meshCounts.ContainsKey(sdfMesh.ID))
        {
            m_meshCounts[sdfMesh.ID]--;

            if (m_meshCounts[sdfMesh.ID] <= 0 && m_globalSDFMeshes.Remove(sdfMesh))
                m_isGlobalMeshDataDirty = true;
        }

        bool wasEmpty = IsEmpty;

        // this is almost certainly overkill, but i like the kind of guaranteed stability
        ClearNulls(m_localSDFMeshes);

        if (!wasEmpty && IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnEmpty();

        RequestUpdate();
    }

    public bool IsRegistered(SDFMesh mesh) => !m_localSDFMeshes.IsNullOrEmpty() && m_localSDFMeshes.Contains(mesh);

    #endregion

    #region MonoBehaviour Callbacks

    private void OnEnable()
    {
        m_isGlobalMeshDataDirty = true;
        m_isPrimitivesListDirty = true;
        m_isLocalMeshDataDirty = true;

        RequestUpdate(onlySendBufferOnChange: false);
        m_forceUpdateNextFrame = true;
    }

    private void Start()
    {
        m_isGlobalMeshDataDirty = true;
        m_isPrimitivesListDirty = true;
        m_isLocalMeshDataDirty = true;

        RequestUpdate(onlySendBufferOnChange: false);
        m_forceUpdateNextFrame = true;
    }

    private void OnDisable()
    {
        m_isReady = false;

        m_primitiveDataBuffer?.Dispose();
        m_localMeshDataBuffer?.Dispose();
        m_settingsBuffer?.Dispose();
    }

    private void OnApplicationQuit()
    {
        // static buffers can't be cleared in ondisable or something,
        // because lots of objects might be using them
        m_meshSamplesBuffer?.Dispose();
        m_meshPackedUVsBuffer?.Dispose();
    }

    private void LateUpdate()
    {
        if (!m_isRunning)
            return;

        if (!m_isReady)
            RequestUpdate();

        bool isPrimitiveListOrderDirty = false;
        bool nullHit = false;
        for (int i = 0; i < m_sdfPrimitives.Count; i++)
        {
            bool isNull = !m_sdfPrimitives[i];

            nullHit |= isNull;

            if (!isNull)
            {
                m_isPrimitivesListDirty |= m_sdfPrimitives[i].IsDirty;
                isPrimitiveListOrderDirty |= m_sdfPrimitives[i].IsOrderDirty;
            }
        }

        // todo: can improve this so it doesn't make allocations
        if (isPrimitiveListOrderDirty)
            m_sdfPrimitives = m_sdfPrimitives.OrderBy(p => p.transform.GetSiblingIndex()).ToList();

        if (nullHit)
            ClearNulls(m_sdfPrimitives);

        // todo: reorder meshes, same as primitives
        nullHit = false;
        for (int i = 0; i < m_localSDFMeshes.Count; i++)
        {
            bool isNull = !m_localSDFMeshes[i];

            nullHit |= isNull;

            if (!isNull)
            {
                m_isLocalMeshDataDirty |= m_localSDFMeshes[i].IsDirty;
            }
        }

        if (nullHit)
            ClearNulls(m_localSDFMeshes);

        bool changed = false;

        if (m_forceUpdateNextFrame || m_isPrimitivesListDirty || transform.hasChanged)
        {
            changed = true;
            RebuildPrimitiveData();
        }

        if (m_forceUpdateNextFrame || m_isGlobalMeshDataDirty || m_isLocalMeshDataDirty || transform.hasChanged)
        {
            changed = true;
            RebuildLocalMeshData();
        }

        m_forceUpdateNextFrame = false;
        transform.hasChanged = false;

        if (changed && !IsEmpty)
        {
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].Run();
        }
    }

    #endregion

    #region Buffer Updating

    /// <summary>
    /// Request a complete buffer rebuild.
    /// </summary>
    /// <param name="onlySendBufferOnChange">Whether to invoke the components and inform them the buffer has changed. This is only really necessary when the size changes.</param>
    public void RequestUpdate(bool onlySendBufferOnChange = true)
    {
        // blocking readiness because we're updating 
        // all the information at once, we don't want groups to start acting
        // on the info immediately
        m_isReady = false;

        m_sdfComponents.Clear();
        m_sdfComponents.AddRange(GetComponentsInChildren<ISDFGroupComponent>());

        if (IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnEmpty();
        else
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].OnNotEmpty();

        RebuildLocalMeshData(onlySendBufferOnChange);
        RebuildPrimitiveData(onlySendBufferOnChange);
        OnSettingsChanged();

        m_isReady = true;

        if (!IsEmpty)
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].Run();
    }

    /// <summary>
    /// Some mesh data is shared across all instances, such as the sample and UV information as well as the start indices in those static buffers
    /// for all meshes. Returns true if the static buffers have been changed and need to be resent to the groups.
    /// </summary>
    /// <param name="onlySendBufferOnChange">Whether to invoke the components and inform them the buffer has changed. This is only really necessary when the size changes.</param>
    private static bool RebuildGlobalMeshData(bool onlySendBufferOnChange = true)
    {
        int previousMeshSamplesCount = m_meshSamples.Count;
        int previousMeshUVsCount = m_meshPackedUVs.Count;

        m_meshSamples.Clear();
        m_meshPackedUVs.Clear();

        m_meshSdfSampleStartIndices.Clear();
        m_meshSdfUVStartIndices.Clear();

        for (int i = m_globalSDFMeshes.Count - 1; i >= 0; --i)
        {
            if (m_globalSDFMeshes[i] == null || m_globalSDFMeshes[i].Asset == null)
                m_globalSDFMeshes.RemoveAt(i);
        }

        // loop over each mesh, adding its samples/uvs to the sample buffer
        // and taking note of where each meshes samples start in the buffer.
        // check for repeats so we don't add the same mesh to the samples buffer twice
        for (int i = 0; i < m_globalSDFMeshes.Count; i++)
        {
            SDFMesh mesh = m_globalSDFMeshes[i];

            // ignore meshes which are in the list but not present in any group
            if (m_meshCounts.TryGetValue(mesh.ID, out int count) && count <= 0)
                continue;

            mesh.Asset.GetDataArrays(out float[] samples, out float[] packedUVs);

            if (!m_meshSdfSampleStartIndices.ContainsKey(mesh.ID))
            {
                int startIndex = m_meshSamples.Count;
                m_meshSamples.AddRange(samples);
                m_meshSdfSampleStartIndices.Add(mesh.ID, startIndex);
            }

            if (mesh.Asset.HasUVs && !m_meshSdfUVStartIndices.ContainsKey(mesh.ID))
            {
                int startIndex = m_meshPackedUVs.Count;
                m_meshPackedUVs.AddRange(packedUVs);
                m_meshSdfUVStartIndices.Add(mesh.ID, startIndex);
            }
        }

        bool newBuffers = false;

        if (m_meshSamplesBuffer == null || !m_meshSamplesBuffer.IsValid() || previousMeshSamplesCount != m_meshSamples.Count)
        {
            m_meshSamplesBuffer?.Dispose();
            m_meshSamplesBuffer = new ComputeBuffer(Mathf.Max(1, m_meshSamples.Count), sizeof(float), ComputeBufferType.Structured);
            newBuffers = true;
        }

        if (m_meshSamples.Count > 0)
            m_meshSamplesBuffer.SetData(m_meshSamples);

        if (m_meshPackedUVsBuffer == null || !m_meshPackedUVsBuffer.IsValid() || previousMeshUVsCount != m_meshPackedUVs.Count)
        {
            m_meshPackedUVsBuffer?.Dispose();
            m_meshPackedUVsBuffer = new ComputeBuffer(Mathf.Max(1, m_meshPackedUVs.Count), sizeof(float), ComputeBufferType.Structured);
            newBuffers = true;
        }

        if (m_meshPackedUVs.Count > 0)
            m_meshPackedUVsBuffer.SetData(m_meshPackedUVs);

        m_isGlobalMeshDataDirty = false;

        return newBuffers;
    }

    /// <summary>
    /// Each group has its own list of sdf meshes it's actually monitoring. We need to store their transforms as well the start indices of their
    /// sample and uv data in the static list.
    /// </summary>
    /// <param name="onlySendBufferOnChange">Whether to invoke the components and inform them the buffer has changed. This is only really necessary when the size changes.</param>
    private void RebuildLocalMeshData(bool onlySendBufferOnChange = true)
    {
        m_isLocalMeshDataDirty = false;

        bool globalBuffersChanged = false;
        if (m_meshSamplesBuffer == null || !m_meshSamplesBuffer.IsValid() || m_meshPackedUVsBuffer == null || !m_meshPackedUVsBuffer.IsValid() || m_isGlobalMeshDataDirty)
            globalBuffersChanged = RebuildGlobalMeshData(onlySendBufferOnChange);

        int previousCount = m_localMeshData.Count;
        m_localMeshData.Clear();

        for (int i = 0; i < m_localSDFMeshes.Count; i++)
        {
            SDFMesh mesh = m_localSDFMeshes[i];

            if (!mesh)
                continue;

            mesh.SetClean();

            if (!m_meshSdfSampleStartIndices.TryGetValue(mesh.ID, out int sampleStartIndex))
            {
                Debug.LogError("Local SDF Mesh " + mesh.Asset.name + " has ID " + mesh.ID + " which isn't present in the static sample dictionary!", mesh.Asset);
                continue;
            }

            int uvStartIndex = -1;

            if (mesh.Asset.HasUVs)
                m_meshSdfUVStartIndices.TryGetValue(mesh.ID, out uvStartIndex);

            SDFMesh.GPUData data = mesh.GetGPUData(sampleStartIndex, uvStartIndex);
            m_localMeshData.Add(data);
        }

        bool sendBuffer = !onlySendBufferOnChange;
        if (m_localMeshDataBuffer == null || !m_localMeshDataBuffer.IsValid() || previousCount != m_localMeshData.Count)
        {
            sendBuffer = true;

            m_localMeshDataBuffer?.Dispose();
            m_localMeshDataBuffer = new ComputeBuffer(Mathf.Max(1, m_localMeshData.Count), SDFMesh.GPUData.Stride, ComputeBufferType.Structured);
        }

        if (sendBuffer)
        {
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].UpdateMeshMetadataBuffer(m_localMeshDataBuffer, m_localMeshData.Count);
        }

        if (m_localMeshData.Count > 0)
            m_localMeshDataBuffer.SetData(m_localMeshData);

        if (!onlySendBufferOnChange || globalBuffersChanged)
        {
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].UpdateMeshSamplesBuffer(m_meshSamplesBuffer, m_meshPackedUVsBuffer);
        }
    }

    /// <summary>
    /// Repopulate the data relating to SDF primitives (spheres, toruses, cuboids etc).
    /// </summary>
    /// <param name="onlySendBufferOnChange">Whether to invoke the components and inform them the buffer has changed. This is only really necessary when the size changes.</param>
    private void RebuildPrimitiveData(bool onlySendBufferOnChange = true)
    {
        m_isPrimitivesListDirty = false;

        int previousCount = m_primitivesData.Count;

        m_primitivesData.Clear();

        for (int i = 0; i < m_sdfPrimitives.Count; i++)
        {
            SDFPrimitive primitive = m_sdfPrimitives[i];
            primitive.SetClean();

            m_primitivesData.Add(primitive.GetGPUData());
        }

        bool sendBuffer = !onlySendBufferOnChange;
        if (m_primitiveDataBuffer == null || !m_primitiveDataBuffer.IsValid() || previousCount != m_primitivesData.Count)
        {
            sendBuffer = true;

            m_primitiveDataBuffer?.Dispose();
            m_primitiveDataBuffer = new ComputeBuffer(Mathf.Max(1, m_primitivesData.Count), SDFPrimitive.GPUData.Stride, ComputeBufferType.Structured);
        }

        if (sendBuffer)
        {
            for (int i = 0; i < m_sdfComponents.Count; i++)
                m_sdfComponents[i].UpdatePrimitivesDataBuffer(m_primitiveDataBuffer, m_primitivesData.Count);
        }

        m_primitiveDataBuffer.SetData(m_primitivesData);
    }

    #endregion

    #region Events

    /// <summary>
    /// To be called whenever the settings universal to this group change.
    /// </summary>
    public void OnSettingsChanged()
    {
        m_settingsArray[0] = new Settings()
        {
            Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing),
            NormalSmoothing = Mathf.Max(MIN_SMOOTHING, m_normalSmoothing)
        };

        if (m_settingsBuffer == null || !m_settingsBuffer.IsValid())
        {
            m_settingsBuffer?.Dispose();
            m_settingsBuffer = new ComputeBuffer(1, Settings.Stride, ComputeBufferType.Structured);
        }
        
        for (int i = 0; i < m_sdfComponents.Count; i++)
            m_sdfComponents[i].UpdateSettingsBuffer(m_settingsBuffer);

        m_settingsBuffer.SetData(m_settingsArray);
    }


    #endregion

    #region Structs

    public struct Settings
    {
        public static int Stride => sizeof(float) * 2;

        public float Smoothing;     // the input to the smooth min function
        public float NormalSmoothing;   // the 'epsilon' value for computing the gradient, affects how smoothed out the normals are
    }

    #endregion

    #region Helper Methods

    private void ClearNulls<T>(List<T> list) where T : MonoBehaviour
    {
        for (int i = list.Count - 1; i >= 0; --i)
            if (!list[i])
                list.RemoveAt(i);
    }

    #endregion
}