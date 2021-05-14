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
    [ExecuteInEditMode]
    public class SDFGroupMeshGenerator : MonoBehaviour, ISDFGroupComponent
    {
        #region Fields and Properties

        private static class Properties
        {
            public static readonly int PointsPerSide_Int = Shader.PropertyToID("_PointsPerSide");
            public static readonly int CellSize_Float = Shader.PropertyToID("_CellSize");

            public static readonly int ConstrainToCellUnits_Float = Shader.PropertyToID("_ConstrainToCellUnits");
            public static readonly int BinarySearchIterations_Int = Shader.PropertyToID("_BinarySearchIterations");
            public static readonly int MaxAngleCosine_Float = Shader.PropertyToID("_MaxAngleCosine");
            public static readonly int VisualNormalSmoothing = Shader.PropertyToID("_VisualNormalSmoothing");
            public static readonly int GradientDescentIterations_Int = Shader.PropertyToID("_GradientDescentIterations");

            public static readonly int NudgeVerticesToAverageNormalScalar_Float = Shader.PropertyToID("_NudgeVerticesToAverageNormalScalar");
            public static readonly int NudgeMaxMagnitude_Float = Shader.PropertyToID("_NudgeMaxMagnitude");

            public static readonly int QEFSweeps_Int = Shader.PropertyToID("_QEFSweeps");
            public static readonly int QEFPseudoInverseThreshold_Float = Shader.PropertyToID("_QEFPseudoInverseThreshold");

            public static readonly int Settings_StructuredBuffer = Shader.PropertyToID("_Settings");
            public static readonly int Transform_Matrix4x4 = Shader.PropertyToID("_GroupTransform");

            public static readonly int SDFData_StructuredBuffer = Shader.PropertyToID("_SDFData");
            public static readonly int SDFDataCount_Int = Shader.PropertyToID("_SDFDataCount");

            public static readonly int Samples_RWBuffer = Shader.PropertyToID("_Samples");
            public static readonly int VertexData_AppendBuffer = Shader.PropertyToID("_VertexDataPoints");
            public static readonly int CellData_RWBuffer = Shader.PropertyToID("_CellDataPoints");
            public static readonly int Counter_RWBuffer = Shader.PropertyToID("_Counter");
            public static readonly int TriangleData_AppendBuffer = Shader.PropertyToID("_TriangleDataPoints");
            public static readonly int VertexData_StructuredBuffer = Shader.PropertyToID("_VertexDataPoints_Structured");
            public static readonly int TriangleData_StructuredBuffer = Shader.PropertyToID("_TriangleDataPoints_Structured");

            public static readonly int MeshVertices_RWBuffer = Shader.PropertyToID("_MeshVertices");
            public static readonly int MeshNormals_RWBuffer = Shader.PropertyToID("_MeshNormals");
            public static readonly int MeshTriangles_RWBuffer = Shader.PropertyToID("_MeshTriangles");
            public static readonly int MeshUVs_RWBuffer = Shader.PropertyToID("_MeshUVs");

            public static readonly int IntermediateVertexBuffer_AppendBuffer = Shader.PropertyToID("_IntermediateVertexBuffer");
            public static readonly int IntermediateVertexBuffer_StructuredBuffer = Shader.PropertyToID("_IntermediateVertexBuffer_Structured");

            public static readonly int ProceduralArgs_RWBuffer = Shader.PropertyToID("_ProceduralArgs");
        }

        private struct Kernels
        {
            public const string MapKernelName = "SurfaceNets_Map";
            private const string GenerateVerticesKernelName = "SurfaceNets_GenerateVertices";
            private const string NumberVerticesKernelName = "SurfaceNets_NumberVertices";
            private const string GenerateTrianglesKernelName = "SurfaceNets_GenerateTriangles";
            public const string BuildIndexBufferKernelName = "SurfaceNets_BuildIndexBuffer";
            private const string AddIntermediateVerticesToIndexBufferKernelName = "SurfaceNets_AddIntermediateVerticesToIndexBuffer";

            public int Map { get; }
            public int GenerateVertices { get; }
            public int NumberVertices { get; }
            public int GenerateTriangles { get; }
            public int BuildIndexBuffer { get; }
            public int AddIntermediateVerticesToIndexBuffer { get; }

            public Kernels(ComputeShader shader)
            {
                Map = shader.FindKernel(MapKernelName);
                GenerateVertices = shader.FindKernel(GenerateVerticesKernelName);
                NumberVertices = shader.FindKernel(NumberVerticesKernelName);
                GenerateTriangles = shader.FindKernel(GenerateTrianglesKernelName);
                BuildIndexBuffer = shader.FindKernel(BuildIndexBufferKernelName);
                AddIntermediateVerticesToIndexBuffer = shader.FindKernel(AddIntermediateVerticesToIndexBufferKernelName);
            }
        }

        private static Kernels m_kernels;

        // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
        private readonly int[] m_counterArray = new int[] { 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1 };
        private NativeArray<int> m_outputCounterNativeArray;
        private readonly int[] m_proceduralArgsArray = new int[] { 0, 1, 0, 0, 0 };
        private float[] m_samplesDebugArray;

        private const int VERTEX_COUNTER = 0;
        private const int TRIANGLE_COUNTER = 3;
        private const int VERTEX_COUNTER_DIV_64 = 6;
        private const int TRIANGLE_COUNTER_DIV_64 = 9;
        private const int INTERMEDIATE_VERTEX_COUNTER = 12;
        private const int INTERMEDIATE_VERTEX_COUNTER_DIV_64 = 15;

        //public const string SurfaceNetsKeyword = "ISOSURFACETYPE_SURFACE_NETS";
        public const string DualContouringKeyword = "ISOSURFACETYPE_DUAL_CONTOURING";
        public const string InterpolationKeyword = "EDGEINTERSECTIONTYPE_INTERPOLATION";
        //public const string BinarySearchKeyword = "EDGEINTERSECTIONTYPE_BINARY_SEARCH";
        public const string ApplyGradientDescentKeyword = "APPLY_GRADIENT_DESCENT";
        public const string OverrideQEFSettingsKeyword = "OVERRIDE_QEF_SETTINGS";

        private const string ComputeShaderResourceName = "Compute_IsoSurfaceExtraction";
        private const string DefaultMeshMaterial = "SDF_DefaultMaterial";

        private ComputeBuffer m_samplesBuffer;
        private ComputeBuffer m_cellDataBuffer;
        private ComputeBuffer m_vertexDataBuffer;
        private ComputeBuffer m_triangleDataBuffer;
        private ComputeBuffer m_meshVerticesBuffer;
        private ComputeBuffer m_meshNormalsBuffer;
        private ComputeBuffer m_meshTrianglesBuffer;
        private ComputeBuffer m_meshUVsBuffer;
        private ComputeBuffer m_intermediateVertexBuffer;
        private ComputeBuffer m_counterBuffer;
        private ComputeBuffer m_proceduralArgsBuffer;

        private NativeArray<Vector3> m_nativeArrayVertices;
        private NativeArray<Vector3> m_nativeArrayNormals;
        private NativeArray<Vector2> m_nativeArrayUVs;
        private NativeArray<int> m_nativeArrayTriangles;

        private VertexData[] m_vertices;
        private TriangleData[] m_triangles;
        private CellData[] m_cells;

        private const int MESH_ARRAY_START_SIZE = 40000;

        [SerializeField]
        private ComputeShader m_computeShader;
        private ComputeShader ComputeShader
        {
            get
            {
                if (m_computeShader)
                    return m_computeShader;

                m_computeShader = Resources.Load<ComputeShader>(ComputeShaderResourceName);

                return m_computeShader;
            }
        }


        private ComputeShader m_computeShaderInstance;

        [SerializeField]
        private SDFGroup m_group;
        public SDFGroup Group
        {
            get
            {
                if (m_group)
                    return m_group;

                if (TryGetComponent(out m_group))
                    return m_group;

                if (transform.parent.TryGetComponent(out m_group))
                    return m_group;

                return null;

            }
        }

        [SerializeField]
        [HideInInspector]
        private GameObject m_meshFilterGameObject;

        [SerializeField]
        [HideInInspector]
        private MeshFilter m_meshFilter;
        private MeshFilter MeshFilter
        {
            get
            {
                if (m_meshFilter)
                    return m_meshFilter;

                if (TryGetOrCreateMeshGameObject())
                {
                    m_meshFilter = m_meshFilterGameObject.GetOrAddComponent<MeshFilter>();
                    return m_meshFilter;
                }

                return null;
            }
        }

        [SerializeField]
        [HideInInspector]
        private MeshCollider m_meshCollider;
        private MeshCollider MeshCollider
        {
            get
            {
                if (!TryGetOrCreateMeshGameObject())
                    return null;

                if (m_meshCollider || m_meshFilterGameObject.TryGetComponent(out m_meshCollider))
                    return m_meshCollider;

                return null;
            }
        }

        [SerializeField]
        [HideInInspector]
        private MeshRenderer m_meshRenderer;
        public MeshRenderer MeshRenderer
        {
            get
            {
                if (!TryGetOrCreateMeshGameObject())
                    return null;

                if (m_meshRenderer || m_meshFilterGameObject.TryGetComponent(out m_meshRenderer))
                    return m_meshRenderer;

                return null;
            }
        }

        private Mesh m_mesh;

        private Bounds m_bounds;

        [SerializeField]
        private bool m_autoUpdate = true;

        [SerializeField]
        private OutputMode m_outputMode = OutputMode.Procedural;
        public OutputMode OutputMode => m_outputMode;

        [SerializeField]
        private bool m_isAsynchronous = false;

        [SerializeField]
        private Material m_proceduralMaterial;
        private MaterialPropertyBlock m_propertyBlock;

        [SerializeField]
        private CellSizeMode m_cellSizeMode = CellSizeMode.Fixed;
        public CellSizeMode CellSizeMode => m_cellSizeMode;

        [SerializeField]
        private float m_cellSize = 0.2f;

        public float CellSize
        {
            get
            {
                if (m_cellSizeMode == CellSizeMode.Density)
                    return m_volumeSize / m_cellDensity;

                return m_cellSize;
            }
        }

        [SerializeField]
        private int m_cellCount = 50;

        public int CellCount
        {
            get
            {
                if (m_cellSizeMode == CellSizeMode.Density)
                    return Mathf.FloorToInt(m_volumeSize * m_cellDensity);

                return m_cellCount;
            }
        }

        [SerializeField]
        private float m_volumeSize = 5f;
        public float VolumeSize => m_volumeSize;

        [SerializeField]
        private float m_cellDensity = 1f;
        public float CellDensity => m_cellDensity;

        [SerializeField]
        private float m_maxAngleTolerance = 20f;
        public float MaxAngleTolerance => m_maxAngleTolerance;

        [SerializeField]
        private float m_visualNormalSmoothing = 1e-5f;
        public float VisualNormalSmoothing => m_visualNormalSmoothing;

        [SerializeField]
        private IsosurfaceExtractionType m_isosurfaceExtractionType = IsosurfaceExtractionType.SurfaceNets;
        public IsosurfaceExtractionType IsosurfaceExtractionType => m_isosurfaceExtractionType;

        [SerializeField]
        private float m_constrainToCellUnits = 0f;
        public float ConstrainToCellUnits => m_constrainToCellUnits;

        [SerializeField]
        private bool m_overrideQEFSettings = false;
        public bool OverrideQEFSettings => m_overrideQEFSettings;

        [SerializeField]
        private int m_qefSweeps = 5;
        public int QefSweeps => m_qefSweeps;

        [SerializeField]
        private float m_qefPseudoInverseThreshold = 1e-2f;
        public float QefPseudoInverseThreshold => m_qefPseudoInverseThreshold;

        [SerializeField]
        private EdgeIntersectionType m_edgeIntersectionType = EdgeIntersectionType.Interpolation;
        public EdgeIntersectionType EdgeIntersectionType => m_edgeIntersectionType;

        [SerializeField]
        private int m_binarySearchIterations = 5;
        public int BinarySearchIterations => m_binarySearchIterations;

        [SerializeField]
        private bool m_applyGradientDescent = false;
        public bool ApplyGradientDescent => m_applyGradientDescent;

        [SerializeField]
        private int m_gradientDescentIterations = 10;
        public int GradientDescentIterations => m_gradientDescentIterations;

        [SerializeField]
        private float m_nudgeVerticesToAverageNormalScalar = 0.01f;
        public float NudgeVerticesToAverageNormalScalar => m_nudgeVerticesToAverageNormalScalar;

        [SerializeField]
        private float m_nudgeMaxMagnitude = 1f;
        public float NudgeMaxMagnitude => m_nudgeMaxMagnitude;

        private bool m_initialized = false;

        public int SamplesPerSide => CellCount + 1;
        public int TotalSampleCount
        {
            get
            {
                int samplesPerSide = CellCount + 1;
                return samplesPerSide * samplesPerSide * samplesPerSide;
            }
        }

        [SerializeField]
        private bool m_showGrid = false;
        public bool ShowGrid => m_showGrid;

        [SerializeField]
        [HideInInspector]
        // this bool is toggled off/on whenever the Unity callbacks OnEnable/OnDisable are called.
        // note that this doesn't always give the same result as "enabled" because OnEnable/OnDisable are
        // called during recompiles etc. you can basically read this bool as "is recompiling"
        private bool m_isEnabled = false;

        // tracks the number of the most recent async call, to prevent earlier calls from overwriting the result of later calls
        private uint m_currentAsyncCallNumber = 0;

        #endregion

        #region MonoBehaviour Callbacks

        private void Reset()
        {
            m_initialized = false;
            OnOutputModeChanged();
        }

        private void OnEnable()
        {
            m_isEnabled = true;
            m_initialized = false;

            if (Group.IsReady)
            {
                InitializeComputeShaderSettings();
                Group.RequestUpdate(onlySendBufferOnChange: false);
            }

#if UNITY_EDITOR
            Undo.undoRedoPerformed += OnUndo;
#endif
        }

        private void OnDisable()
        {
            m_isEnabled = false;
            ReleaseUnmanagedMemory();

#if UNITY_EDITOR
            Undo.undoRedoPerformed -= OnUndo;
#endif
        }

        private void OnUndo()
        {
            if (m_initialized)
            {
                m_initialized = false;
                InitializeComputeShaderSettings();
                m_group.RequestUpdate();
            }
        }

        #endregion

        #region Mesh Stuff

        private void Update()
        {
            if (transform.hasChanged && Group.IsReady && !Group.IsEmpty && Group.IsRunning)
            {
                SendTransformToGPU();
                UpdateMesh();
            }

            transform.hasChanged = false;

            if (m_meshFilterGameObject)
                m_meshFilterGameObject.transform.hasChanged = false;
        }

        private void LateUpdate()
        {
            if (!m_initialized || !Group.IsReady || Group.IsEmpty)
                return;

            if (m_outputMode == OutputMode.Procedural)
            {
                if (!m_proceduralMaterial)
                {
                    Debug.LogError("Can't draw procedural, material is missing.");
                    return;
                }

                if (m_proceduralArgsBuffer == null || !m_proceduralArgsBuffer.IsValid())
                {
                    Debug.LogError("Can't draw procedural, buffer is invalid.");
                    return;
                }

                if (m_propertyBlock == null)
                {
                    Debug.LogError("Can't draw procedural, material property block is missing.");
                    return;
                }

                Graphics.DrawProceduralIndirect(m_proceduralMaterial, m_bounds, MeshTopology.Triangles, m_proceduralArgsBuffer, properties: m_propertyBlock);
            }
        }

        public void UpdateMesh()
        {
            if (!m_initialized || !Group.IsReady || Group.IsEmpty)
                return;

            Dispatch();

            if (m_outputMode == OutputMode.MeshFilter)
            {
                if (m_isAsynchronous)
                    StartCoroutine(Cr_GetMeshDataFromGPUAsync());
                else
                    GetMeshDataFromGPU();
            }
        }

        private void GetCounts(NativeArray<int> counter, out int vertexCount, out int triangleCount)
        {
            vertexCount = counter[VERTEX_COUNTER] + counter[INTERMEDIATE_VERTEX_COUNTER];
            triangleCount = counter[TRIANGLE_COUNTER];
        }

        private void ReallocateNativeArrays(int vertexCount, int triangleCount, ref NativeArray<Vector3> vertices, ref NativeArray<Vector3> normals, ref NativeArray<Vector2> uvs, ref NativeArray<int> indices)
        {
            // to avoid lots of allocations here, i only create new arrays when
            // 1) there's no array to begin with
            // 2) the number of items to store is greater than the size of the current array
            // 3) the size of the current array is greater than the size of the entire buffer
            void ReallocateArrayIfNeeded<T>(ref NativeArray<T> array, int count) where T : struct
            {
                if (array == null || !array.IsCreated || array.Length < count)
                {
                    if (array != null && array.IsCreated)
                        array.Dispose();

                    array = new NativeArray<T>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
            }

            ReallocateArrayIfNeeded(ref vertices, vertexCount);
            ReallocateArrayIfNeeded(ref normals, vertexCount);
            ReallocateArrayIfNeeded(ref uvs, vertexCount);
            ReallocateArrayIfNeeded(ref indices, triangleCount * 3);
        }

        private void GetMeshDataFromGPU()
        {
            AsyncGPUReadbackRequest counterRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_outputCounterNativeArray, m_counterBuffer);
            counterRequest.WaitForCompletion();

            GetCounts(m_outputCounterNativeArray, out int vertexCount, out int triangleCount);

            if (triangleCount > 0)
            {
                ReallocateNativeArrays(vertexCount, triangleCount, ref m_nativeArrayVertices, ref m_nativeArrayNormals, ref m_nativeArrayUVs, ref m_nativeArrayTriangles);
                
                AsyncGPUReadbackRequest vertexRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayVertices, m_meshVerticesBuffer, m_nativeArrayVertices.Length * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest normalRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayNormals, m_meshNormalsBuffer, m_nativeArrayNormals.Length * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest uvRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayUVs, m_meshUVsBuffer, m_nativeArrayUVs.Length * sizeof(float) * 2, 0);
                AsyncGPUReadbackRequest triangleRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayTriangles, m_meshTrianglesBuffer, m_nativeArrayTriangles.Length * sizeof(int), 0);

                AsyncGPUReadback.WaitAllRequests();
            }

            SetMeshData(m_nativeArrayVertices, m_nativeArrayNormals, m_nativeArrayUVs, m_nativeArrayTriangles, vertexCount, triangleCount);
        }
        
        private IEnumerator Cr_GetMeshDataFromGPUAsync()
        {
            uint asyncCallNumber = ++m_currentAsyncCallNumber;

            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;

            NativeArray<int> counter = new NativeArray<int>(m_counterBuffer.count, Allocator.Persistent);
            
            AsyncGPUReadbackRequest counterRequest = AsyncGPUReadback.RequestIntoNativeArray(ref counter, m_counterBuffer);

            yield return new WaitUntil(() => counterRequest.done);

            GetCounts(counter, out int vertexCount, out int triangleCount);
            
            counter.Dispose();

            if (triangleCount > 0)
            {
                NativeArray<Vector3> vertices = new NativeArray<Vector3>(m_meshVerticesBuffer.count, Allocator.Persistent);
                NativeArray<Vector3> normals = new NativeArray<Vector3>(m_meshNormalsBuffer.count, Allocator.Persistent);
                NativeArray<Vector2> uvs = new NativeArray<Vector2>(m_meshUVsBuffer.count, Allocator.Persistent);
                NativeArray<int> indices = new NativeArray<int>(m_meshTrianglesBuffer.count, Allocator.Persistent);
                
                AsyncGPUReadbackRequest vertexRequest = AsyncGPUReadback.RequestIntoNativeArray(ref vertices, m_meshVerticesBuffer);
                AsyncGPUReadbackRequest normalRequest = AsyncGPUReadback.RequestIntoNativeArray(ref normals, m_meshNormalsBuffer);
                AsyncGPUReadbackRequest uvRequest = AsyncGPUReadback.RequestIntoNativeArray(ref uvs, m_meshUVsBuffer);
                AsyncGPUReadbackRequest triangleRequest = AsyncGPUReadback.RequestIntoNativeArray(ref indices, m_meshTrianglesBuffer);

                yield return new WaitUntil(() => vertexRequest.done && normalRequest.done && uvRequest.done && triangleRequest.done);

                if (asyncCallNumber == m_currentAsyncCallNumber)
                    SetMeshData(vertices, normals, uvs, indices, vertexCount, triangleCount);
                
                vertices.Dispose();
                normals.Dispose();
                uvs.Dispose();
                indices.Dispose();
            }
            else
            {
                if (MeshRenderer)
                    MeshRenderer.enabled = false;

                if (MeshCollider)
                    MeshCollider.enabled = false;
            }
        }

        private void SetMeshData(NativeArray<Vector3> vertices, NativeArray<Vector3> normals, NativeArray<Vector2> uvs, NativeArray<int> indices, int vertexCount, int triangleCount)
        {
            if (triangleCount == 0)
            {
                if (MeshRenderer)
                    MeshRenderer.enabled = false;

                if (MeshCollider)
                    MeshCollider.enabled = false;
            }
            else
            {
                if (MeshRenderer)
                    MeshRenderer.enabled = true;

                if (MeshCollider)
                    MeshCollider.enabled = true;

                if (m_mesh == null)
                {
                    m_mesh = new Mesh()
                    {
                        indexFormat = IndexFormat.UInt32
                    };
                }
                else
                {
                    m_mesh.Clear();
                }

                m_mesh.SetVertices(vertices, 0, vertexCount);
                m_mesh.SetNormals(normals, 0, vertexCount);
                m_mesh.SetUVs(0, uvs, 0, vertexCount);
                m_mesh.SetIndices(indices, 0, triangleCount * 3, MeshTopology.Triangles, 0, calculateBounds: true);

                MeshFilter.mesh = m_mesh;

                if (MeshCollider)
                    MeshCollider.sharedMesh = m_mesh;
            }
        }

        private bool TryGetOrCreateMeshGameObject()
        {
            if (m_outputMode != OutputMode.MeshFilter)
                return false;

            if (m_meshFilterGameObject)
                return true;

            m_meshFilterGameObject = new GameObject("Mesh");
            m_meshFilterGameObject.transform.SetParent(transform);
            m_meshFilterGameObject.transform.Reset();

            return true;
        }

        #endregion

        #region Internal Compute Shader Stuff + Other Boring Boilerplate Methods

        /// <summary>
        /// Do all the initial setup. This function should only be called once per 'session' because it does a lot of
        /// setup for buffers of constant size.
        /// </summary>
        private void InitializeComputeShaderSettings()
        {
            if (m_initialized || !m_isEnabled)
                return;

            ReleaseUnmanagedMemory();

            bool wasAutoUpdating = m_autoUpdate;
            m_autoUpdate = false;

            m_initialized = true;

            m_computeShaderInstance = Instantiate(ComputeShader);

            SendTransformToGPU();

            ResendKeywords();

            m_kernels = new Kernels(ComputeShader);

            m_outputCounterNativeArray = new NativeArray<int>(m_counterArray.Length, Allocator.Persistent);

            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
            m_counterBuffer = new ComputeBuffer(m_counterArray.Length, sizeof(int), ComputeBufferType.IndirectArguments);
            m_proceduralArgsBuffer = new ComputeBuffer(m_proceduralArgsArray.Length, sizeof(int), ComputeBufferType.IndirectArguments);

            m_computeShaderInstance.SetBuffer(m_kernels.NumberVertices, Properties.Counter_RWBuffer, m_counterBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.Counter_RWBuffer, m_counterBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateVertices, Properties.Counter_RWBuffer, m_counterBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.NumberVertices, Properties.Counter_RWBuffer, m_counterBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.BuildIndexBuffer, Properties.Counter_RWBuffer, m_counterBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.Counter_RWBuffer, m_counterBuffer);

            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.ProceduralArgs_RWBuffer, m_proceduralArgsBuffer);

            CreateVariableBuffers();

            // ensuring all these setting variables are sent to the gpu.
            OnCellSizeChanged();
            OnConstrainToCellUnitsChanged();
            OnBinarySearchIterationsChanged();
            OnVisualNormalSmoothingChanged();
            OnMaxAngleToleranceChanged();
            OnGradientDescentIterationsChanged();
            OnNudgeSettingsChanged();
            OnOutputModeChanged();

            // set autoUpdate back to its value before this function was called.
            // we temporarily disable autoupdate during this function
            m_autoUpdate = wasAutoUpdating;
        }

        /// <summary>
        /// Create the buffers which will need to be recreated and reset if certain settings change, such as cell count.
        /// </summary>
        private void CreateVariableBuffers()
        {
            int countCubed = TotalSampleCount;

            m_computeShaderInstance.SetInt(Properties.PointsPerSide_Int, SamplesPerSide);

            m_vertices = new VertexData[countCubed];
            m_triangles = new TriangleData[countCubed];
            m_cells = new CellData[countCubed];

            m_samplesBuffer?.Dispose();
            m_cellDataBuffer?.Dispose();
            m_vertexDataBuffer?.Dispose();
            m_triangleDataBuffer?.Dispose();

            m_meshVerticesBuffer?.Dispose();
            m_meshNormalsBuffer?.Dispose();
            m_meshTrianglesBuffer?.Dispose();
            m_meshUVsBuffer?.Dispose();

            m_intermediateVertexBuffer?.Dispose();

            m_samplesBuffer = new ComputeBuffer(countCubed, sizeof(float), ComputeBufferType.Structured);
            m_cellDataBuffer = new ComputeBuffer(countCubed, CellData.Stride, ComputeBufferType.Structured);
            m_vertexDataBuffer = new ComputeBuffer(countCubed, VertexData.Stride, ComputeBufferType.Append);
            m_triangleDataBuffer = new ComputeBuffer(countCubed, TriangleData.Stride, ComputeBufferType.Append);

            m_meshVerticesBuffer = new ComputeBuffer(countCubed * 3, sizeof(float) * 3, ComputeBufferType.Structured);
            m_meshNormalsBuffer = new ComputeBuffer(countCubed * 3, sizeof(float) * 3, ComputeBufferType.Structured);
            m_meshTrianglesBuffer = new ComputeBuffer(countCubed * 3, sizeof(int), ComputeBufferType.Structured);
            m_meshUVsBuffer = new ComputeBuffer(countCubed * 3, sizeof(float) * 2, ComputeBufferType.Structured);

            m_intermediateVertexBuffer = new ComputeBuffer(countCubed * 3, NewVertexData.Stride, ComputeBufferType.Append);

            if (m_proceduralMaterial)
            {
                m_propertyBlock = new MaterialPropertyBlock();
                m_propertyBlock.SetBuffer(Properties.MeshVertices_RWBuffer, m_meshVerticesBuffer);
                m_propertyBlock.SetBuffer(Properties.MeshTriangles_RWBuffer, m_meshTrianglesBuffer);
                m_propertyBlock.SetBuffer(Properties.MeshNormals_RWBuffer, m_meshNormalsBuffer);
                m_propertyBlock.SetBuffer(Properties.MeshUVs_RWBuffer, m_meshUVsBuffer);
            }

            UpdateMapKernels(Properties.Samples_RWBuffer, m_samplesBuffer);

            m_computeShaderInstance.SetBuffer(m_kernels.GenerateVertices, Properties.CellData_RWBuffer, m_cellDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.NumberVertices, Properties.CellData_RWBuffer, m_cellDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.CellData_RWBuffer, m_cellDataBuffer);

            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.MeshVertices_RWBuffer, m_meshVerticesBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.MeshNormals_RWBuffer, m_meshNormalsBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.MeshTriangles_RWBuffer, m_meshTrianglesBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.MeshUVs_RWBuffer, m_meshUVsBuffer);

            m_computeShaderInstance.SetBuffer(m_kernels.BuildIndexBuffer, Properties.MeshTriangles_RWBuffer, m_meshTrianglesBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.BuildIndexBuffer, Properties.IntermediateVertexBuffer_AppendBuffer, m_intermediateVertexBuffer);

            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.MeshVertices_RWBuffer, m_meshVerticesBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.MeshNormals_RWBuffer, m_meshNormalsBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.MeshTriangles_RWBuffer, m_meshTrianglesBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.MeshUVs_RWBuffer, m_meshUVsBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.AddIntermediateVerticesToIndexBuffer, Properties.IntermediateVertexBuffer_StructuredBuffer, m_intermediateVertexBuffer);

            m_bounds = new Bounds
            {
                extents = Vector3.one * CellCount * CellSize
            };

            ResetCounters();
            SetVertexData();
            SetTriangleData();
        }

        /// <summary>
        /// Buffers and NativeArrays are unmanaged and Unity will cry if we don't do this.
        /// </summary>
        private void ReleaseUnmanagedMemory()
        {
            if (m_outputCounterNativeArray != null && m_outputCounterNativeArray.IsCreated)
                m_outputCounterNativeArray.Dispose();

            if (m_nativeArrayVertices != null && m_nativeArrayVertices.IsCreated)
                m_nativeArrayVertices.Dispose();

            if (m_nativeArrayNormals != null && m_nativeArrayNormals.IsCreated)
                m_nativeArrayNormals.Dispose();

            if (m_nativeArrayUVs != null && m_nativeArrayUVs.IsCreated)
                m_nativeArrayUVs.Dispose();

            if (m_nativeArrayTriangles != null && m_nativeArrayTriangles.IsCreated)
                m_nativeArrayTriangles.Dispose();
            
            m_counterBuffer?.Dispose();
            m_proceduralArgsBuffer?.Dispose();

            m_samplesBuffer?.Dispose();
            m_cellDataBuffer?.Dispose();
            m_vertexDataBuffer?.Dispose();
            m_triangleDataBuffer?.Dispose();

            m_meshVerticesBuffer?.Dispose();
            m_meshNormalsBuffer?.Dispose();
            m_meshTrianglesBuffer?.Dispose();
            m_meshUVsBuffer?.Dispose();

            m_intermediateVertexBuffer?.Dispose();

            m_initialized = false;

            if (m_computeShaderInstance)
                DestroyImmediate(m_computeShaderInstance);
        }

        /// <summary>
        /// Send a buffer to all kernels which use the map function.
        /// </summary>
        private void UpdateMapKernels(int id, ComputeBuffer buffer)
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (buffer == null || !buffer.IsValid())
            {
                Debug.Log("Attempting to pass null buffer to map kernels.");
                return;
            }

            m_computeShaderInstance.SetBuffer(m_kernels.Map, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateVertices, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, id, buffer);
        }

        /// <summary>
        /// Sets the vertex data as empty and then sends it back to all three kernels? TODO: get rid of this
        /// </summary>
        private void SetVertexData()
        {
            m_vertexDataBuffer.SetData(m_vertices);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateVertices, Properties.VertexData_AppendBuffer, m_vertexDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.NumberVertices, Properties.VertexData_StructuredBuffer, m_vertexDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.VertexData_StructuredBuffer, m_vertexDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.BuildIndexBuffer, Properties.VertexData_StructuredBuffer, m_vertexDataBuffer);
        }

        /// <summary>
        /// Sets the triangle data as empty and then sends it back? TODO: get rid of this
        /// </summary>
        private void SetTriangleData()
        {
            m_triangleDataBuffer.SetData(m_triangles);
            m_computeShaderInstance.SetBuffer(m_kernels.GenerateTriangles, Properties.TriangleData_AppendBuffer, m_triangleDataBuffer);
            m_computeShaderInstance.SetBuffer(m_kernels.BuildIndexBuffer, Properties.TriangleData_StructuredBuffer, m_triangleDataBuffer);
        }

        public void Run()
        {
            // this only happens when unity is recompiling. this is annoying and hacky but it works.
            if (m_counterBuffer == null || !m_counterBuffer.IsValid())
                return;

            if (m_autoUpdate)
            {
                if (!m_initialized)
                    InitializeComputeShaderSettings();

                if (m_outputMode == OutputMode.MeshFilter)
                    UpdateMesh();
                else
                    Dispatch();
            }
        }

        /// <summary>
        /// Set all the shader keywords. This is called by a lot of attributes as well as during the intitialization.
        /// </summary>
        private void ResendKeywords()
        {
            OnQEFSettingsOverrideChanged();
            OnIsosurfaceExtractionTypeChanged();
            OnEdgeIntersectionTypeChanged();
            OnApplyGradientDescentChanged();
        }

        /// <summary>
        /// Dispatch all the compute kernels in the correct order. Basically... do the thing.
        /// </summary>
        private void Dispatch()
        {
            // this only happens when unity is recompiling. this is annoying and hacky but it works.
            if (m_counterBuffer == null || !m_counterBuffer.IsValid())
                return;

            ResetCounters();

            DispatchMap();
            DispatchGenerateVertices();
            DispatchNumberVertices();
            DispatchGenerateTriangles();
            DispatchBuildIndexBuffer();
            DispatchAddIntermediateVerticesToIndexBuffer();
        }

        /// <summary>
        /// Reset count of append buffers.
        /// </summary>
        private void ResetCounters()
        {
            m_counterBuffer.SetData(m_counterArray);

            m_vertexDataBuffer?.SetCounterValue(0);
            m_triangleDataBuffer?.SetCounterValue(0);

            m_meshVerticesBuffer?.SetCounterValue(0);
            m_meshNormalsBuffer?.SetCounterValue(0);
            m_meshTrianglesBuffer?.SetCounterValue(0);

            m_intermediateVertexBuffer?.SetCounterValue(0);

            m_proceduralArgsBuffer?.SetData(m_proceduralArgsArray);
        }

        private void DispatchMap()
        {
            UpdateMapKernels(Properties.Settings_StructuredBuffer, Group.SettingsBuffer);

            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.Map, out uint x, out uint y, out uint z);
            m_computeShaderInstance.Dispatch(m_kernels.Map, Mathf.CeilToInt(SamplesPerSide / (float)x), Mathf.CeilToInt(SamplesPerSide / (float)y), Mathf.CeilToInt(SamplesPerSide / (float)z));
        }

        private void DispatchGenerateVertices()
        {
            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.GenerateVertices, out uint x, out uint y, out uint z);
            m_computeShaderInstance.Dispatch(m_kernels.GenerateVertices, Mathf.CeilToInt(SamplesPerSide / (float)x), Mathf.CeilToInt(SamplesPerSide / (float)y), Mathf.CeilToInt(SamplesPerSide / (float)z));
        }

        private void DispatchNumberVertices()
        {
            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
            m_computeShaderInstance.DispatchIndirect(m_kernels.NumberVertices, m_counterBuffer, VERTEX_COUNTER_DIV_64 * sizeof(int));
        }

        private void DispatchGenerateTriangles()
        {
            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
            m_computeShaderInstance.DispatchIndirect(m_kernels.GenerateTriangles, m_counterBuffer, VERTEX_COUNTER_DIV_64 * sizeof(int));
        }

        private void DispatchBuildIndexBuffer()
        {
            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
            m_computeShaderInstance.DispatchIndirect(m_kernels.BuildIndexBuffer, m_counterBuffer, TRIANGLE_COUNTER_DIV_64 * sizeof(int));
        }

        private void DispatchAddIntermediateVerticesToIndexBuffer()
        {
            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, triangle count / 64, 1, 1, intermediate vertex count, 1, 1, intermediate vertex count / 64, 1, 1]
            m_computeShaderInstance.DispatchIndirect(m_kernels.AddIntermediateVerticesToIndexBuffer, m_counterBuffer, INTERMEDIATE_VERTEX_COUNTER_DIV_64 * sizeof(int));
        }

        private void SendTransformToGPU()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetMatrix(Properties.Transform_Matrix4x4, transform.localToWorldMatrix);
        }

        public void OnCellCountChanged()
        {
            m_bounds = new Bounds
            {
                extents = Vector3.one * CellCount * CellSize
            };

            if (!m_initialized || !m_isEnabled)
                return;

            CreateVariableBuffers();

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnCellSizeChanged()
        {
            m_bounds = new Bounds
            {
                extents = Vector3.one * CellCount * CellSize
            };

            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.CellSize_Float, CellSize);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnConstrainToCellUnitsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.ConstrainToCellUnits_Float, m_constrainToCellUnits);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnVisualNormalSmoothingChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.VisualNormalSmoothing, m_visualNormalSmoothing);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnMaxAngleToleranceChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.MaxAngleCosine_Float, Mathf.Cos(m_maxAngleTolerance * Mathf.Deg2Rad));

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnGradientDescentIterationsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetInt(Properties.GradientDescentIterations_Int, m_gradientDescentIterations);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnBinarySearchIterationsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetInt(Properties.BinarySearchIterations_Int, m_binarySearchIterations);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnIsosurfaceExtractionTypeChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_isosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                m_computeShaderInstance.EnableKeyword(DualContouringKeyword);
            else
                m_computeShaderInstance.DisableKeyword(DualContouringKeyword);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnEdgeIntersectionTypeChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_edgeIntersectionType == EdgeIntersectionType.BinarySearch)
                m_computeShaderInstance.DisableKeyword(InterpolationKeyword);
            else
                m_computeShaderInstance.EnableKeyword(InterpolationKeyword);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void SetNudgeSettings(float nudgeVerticesToAverageNormalScalar, float nudgeMaxMagnitude)
        {
            m_nudgeVerticesToAverageNormalScalar = nudgeVerticesToAverageNormalScalar;
            m_nudgeMaxMagnitude = nudgeMaxMagnitude;

            OnNudgeSettingsChanged();
        }

        public void OnNudgeSettingsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.NudgeVerticesToAverageNormalScalar_Float, m_nudgeVerticesToAverageNormalScalar);
            m_computeShaderInstance.SetFloat(Properties.NudgeMaxMagnitude_Float, m_nudgeMaxMagnitude);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnApplyGradientDescentChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_applyGradientDescent)
                m_computeShaderInstance.EnableKeyword(ApplyGradientDescentKeyword);
            else
                m_computeShaderInstance.DisableKeyword(ApplyGradientDescentKeyword);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnQEFSettingsOverrideChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.QEFPseudoInverseThreshold_Float, m_qefPseudoInverseThreshold);
            m_computeShaderInstance.SetInt(Properties.QEFSweeps_Int, m_qefSweeps);

            if (m_overrideQEFSettings)
                m_computeShaderInstance.EnableKeyword(OverrideQEFSettingsKeyword);
            else
                m_computeShaderInstance.DisableKeyword(OverrideQEFSettingsKeyword);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void SetOutputMode(OutputMode outputMode)
        {
            m_outputMode = outputMode;
            OnOutputModeChanged();
        }

        public void OnOutputModeChanged()
        {
            if (m_outputMode == OutputMode.MeshFilter && TryGetOrCreateMeshGameObject())
            {
                m_meshFilterGameObject.SetActive(true);

                if (MeshRenderer)
                    MeshRenderer.enabled = !Group.IsEmpty;

                Group.RequestUpdate(onlySendBufferOnChange: false);
            }
            else if (m_outputMode == OutputMode.Procedural)
            {
                if (m_meshFilterGameObject)
                    m_meshFilterGameObject.SetActive(false);
            }
        }

        public void OnDensitySettingChanged()
        {
            OnCellSizeChanged();
            CreateVariableBuffers();
        }

        #endregion

        #region SDF Group Methods

        public void UpdateDataBuffer(ComputeBuffer computeBuffer, int count)
        {
            if (!m_isEnabled)
                return;

            if (!m_initialized)
                InitializeComputeShaderSettings();

            UpdateMapKernels(Properties.SDFData_StructuredBuffer, computeBuffer);
            m_computeShaderInstance.SetInt(Properties.SDFDataCount_Int, count);

            if (m_autoUpdate)
                UpdateMesh();
        }

        //public void UpdateGlobalMeshDataBuffers(ComputeBuffer samplesBuffer, ComputeBuffer packedUVsBuffer)
        //{
        //    if (!m_isEnabled)
        //        return;

        //    if (!m_initialized)
        //        InitializeComputeShaderSettings();

        //    UpdateMapKernels(Properties.MeshSamples_StructuredBuffer, samplesBuffer);
        //    UpdateMapKernels(Properties.MeshPackedUVs_StructuredBuffer, packedUVsBuffer);

        //    if (m_autoUpdate)
        //        UpdateMesh();
        //}

        public void UpdateSettingsBuffer(ComputeBuffer computeBuffer)
        {
            if (!m_isEnabled)
                return;

            if (!m_initialized)
                InitializeComputeShaderSettings();

            UpdateMapKernels(Properties.Settings_StructuredBuffer, computeBuffer);

            if (m_autoUpdate)
                UpdateMesh();
        }

        public void OnEmpty()
        {
            if (MeshRenderer)
                MeshRenderer.enabled = false;
        }

        public void OnNotEmpty()
        {
            if (MeshRenderer)
                MeshRenderer.enabled = m_outputMode == OutputMode.MeshFilter;
        }

        public void OnPrimitivesChanged()
        {
            if (m_autoUpdate)
                UpdateMesh();
        }

        #endregion

        #region Grid Helper Functions

        public Vector3 CellCoordinateToVertex(int x, int y, int z)
        {
            float gridSize = (float)(SamplesPerSide - 1f);
            float bound = (gridSize / 2f) * CellSize;

            float xPos = Mathf.LerpUnclamped(-bound, bound, x / gridSize);
            float yPos = Mathf.LerpUnclamped(-bound, bound, y / gridSize);
            float zPos = Mathf.LerpUnclamped(-bound, bound, z / gridSize);

            return new Vector3(xPos, yPos, zPos);
        }

        public Vector3 CellCoordinateToVertex(Vector3Int vec) =>
            CellCoordinateToVertex(vec.x, vec.y, vec.z);

        public Vector3Int IndexToCellCoordinate(int index)
        {
            int z = index / (SamplesPerSide * SamplesPerSide);
            index -= (z * SamplesPerSide * SamplesPerSide);
            int y = index / SamplesPerSide;
            int x = index % SamplesPerSide;

            return new Vector3Int(x, y, z);
        }

        public Vector3 IndexToVertex(int index)
        {
            Vector3Int coords = IndexToCellCoordinate(index);
            return CellCoordinateToVertex(coords.x, coords.y, coords.z);
        }

        public int CellCoordinateToIndex(int x, int y, int z) =>
            (x + y * SamplesPerSide + z * SamplesPerSide * SamplesPerSide);

        public int CellCoordinateToIndex(Vector3Int vec) =>
            CellCoordinateToIndex(vec.x, vec.y, vec.z);

        #endregion

        #region Structs

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
        [SerializeField]
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
        [SerializeField]
        public struct TriangleData
        {
            public static int Stride => sizeof(int) * 3;

            public int P_1;
            public int P_2;
            public int P_3;

            public override string ToString() => $"P_1 = {P_1}, P_2 = {P_2}, P_3 = {P_3}";
        }

        [StructLayout(LayoutKind.Sequential)]
        [SerializeField]
        public struct NewVertexData
        {
            public static int Stride => sizeof(int) + sizeof(float) * 6;

            public int Index;
            public Vector3 Vertex;
            public Vector3 Normal;

            public override string ToString() => $"Index = {Index}, Vertex = {Vertex}, Normal = {Normal}";
        }


        #endregion
    }

    public enum IsosurfaceExtractionType { SurfaceNets, DualContouring };
    public enum EdgeIntersectionType { Interpolation, BinarySearch };

    public enum CellSizeMode { Fixed, Density };
    public enum OutputMode { MeshFilter, Procedural };
}