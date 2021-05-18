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

            public static readonly int MeshTransform_Matrix4x4 = Shader.PropertyToID("_MeshTransform");
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

        private const int VERTEX_COUNTER = 0;
        private const int TRIANGLE_COUNTER = 3;
        private const int VERTEX_COUNTER_DIV_64 = 6;
        private const int TRIANGLE_COUNTER_DIV_64 = 9;
        private const int INTERMEDIATE_VERTEX_COUNTER = 12;
        private const int INTERMEDIATE_VERTEX_COUNTER_DIV_64 = 15;

        public const string DualContouringKeyword = "ISOSURFACETYPE_DUAL_CONTOURING";
        public const string InterpolationKeyword = "EDGEINTERSECTIONTYPE_INTERPOLATION";
        public const string ApplyGradientDescentKeyword = "APPLY_GRADIENT_DESCENT";
        public const string OverrideQEFSettingsKeyword = "OVERRIDE_QEF_SETTINGS";

        private const string ComputeShaderResourceName = "Compute_IsoSurfaceExtraction";

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
        private GameObject m_meshFilterGameObject;

        [SerializeField]
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

        private MaterialPropertyBlock m_propertyBlock;

        [SerializeField]
        private MainSettings m_mainSettings = new MainSettings();

        [SerializeField]
        private VoxelSettings m_voxelSettings = new VoxelSettings();

        [SerializeField]
        private AlgorithmSettings m_algorithmSettings = new AlgorithmSettings();

        private bool m_initialized = false;

        [SerializeField]
        private bool m_showGrid = false;
        public bool ShowGrid => m_showGrid;
        
        private bool m_isEnabled = false;

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
            if ((transform.hasChanged || (TryGetOrCreateMeshGameObject() && m_meshFilterGameObject.transform.hasChanged)) && Group.IsReady && !Group.IsEmpty && Group.IsRunning)
            {
                if (TryGetOrCreateMeshGameObject())
                    m_meshFilterGameObject.transform.hasChanged = false;

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

            if (m_mainSettings.OutputMode == OutputMode.Procedural)
                Graphics.DrawProceduralIndirect(m_mainSettings.ProceduralMaterial, m_bounds, MeshTopology.Triangles, m_proceduralArgsBuffer, properties: m_propertyBlock);
        }

        public void UpdateMesh()
        {
            if (!m_initialized || !Group.IsReady || Group.IsEmpty)
                return;

            if (m_mainSettings.OutputMode == OutputMode.MeshFilter)
            {
                if (m_mainSettings.IsAsynchronous)
                {
                    if (!m_isCoroutineRunning)
                        StartCoroutine(Cr_GetMeshDataFromGPUAsync());
                }
                else
                {
                    GetMeshDataFromGPU();
                }
            }
            else
            {
                Dispatch();
            }
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
            Dispatch();

            if (m_outputCounterNativeArray == null || !m_outputCounterNativeArray.IsCreated)
                m_outputCounterNativeArray = new NativeArray<int>(m_counterBuffer.count, Allocator.Persistent);

            AsyncGPUReadbackRequest counterRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_outputCounterNativeArray, m_counterBuffer);
            counterRequest.WaitForCompletion();

            GetCounts(m_outputCounterNativeArray, out int vertexCount, out int triangleCount);

            if (triangleCount > 0)
            {
                ReallocateNativeArrays(vertexCount, triangleCount, ref m_nativeArrayVertices, ref m_nativeArrayNormals, ref m_nativeArrayUVs, ref m_nativeArrayTriangles);

                int vertexRequestSize = Mathf.Min(m_nativeArrayVertices.Length, m_meshVerticesBuffer.count, vertexCount);
                int triangleRequestSize = Mathf.Min(m_nativeArrayTriangles.Length, m_meshTrianglesBuffer.count, triangleCount * 3);

                AsyncGPUReadbackRequest vertexRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayVertices, m_meshVerticesBuffer, vertexRequestSize * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest normalRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayNormals, m_meshNormalsBuffer, vertexRequestSize * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest uvRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayUVs, m_meshUVsBuffer, vertexRequestSize * sizeof(float) * 2, 0);
                AsyncGPUReadbackRequest triangleRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayTriangles, m_meshTrianglesBuffer, triangleRequestSize * sizeof(int), 0);

                AsyncGPUReadback.WaitAllRequests();

                SetMeshData(m_nativeArrayVertices, m_nativeArrayNormals, m_nativeArrayUVs, m_nativeArrayTriangles, vertexCount, triangleCount);
            }
            else
            {
                if (MeshRenderer)
                    MeshRenderer.enabled = false;

                if (MeshCollider)
                    MeshCollider.enabled = false;
            }
        }

        private bool m_isCoroutineRunning = false;

        /// <summary>
        /// This is the asynchronous version of <see cref="GetMeshDataFromGPU"/>. Use it as a coroutine. It uses a member variable to prevent duplicates from running at the same time.
        /// </summary>
        private IEnumerator Cr_GetMeshDataFromGPUAsync()
        {
            if (m_isCoroutineRunning)
                yield break;

            m_isCoroutineRunning = true;

            Dispatch();

            if (m_outputCounterNativeArray == null || !m_outputCounterNativeArray.IsCreated)
                m_outputCounterNativeArray = new NativeArray<int>(m_counterBuffer.count, Allocator.Persistent);

            AsyncGPUReadbackRequest counterRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_outputCounterNativeArray, m_counterBuffer);

            while (!counterRequest.done)
                yield return null;

            GetCounts(m_outputCounterNativeArray, out int vertexCount, out int triangleCount);

            if (triangleCount > 0)
            {
                ReallocateNativeArrays(vertexCount, triangleCount, ref m_nativeArrayVertices, ref m_nativeArrayNormals, ref m_nativeArrayUVs, ref m_nativeArrayTriangles);

                int vertexRequestSize = Mathf.Min(m_nativeArrayVertices.Length, m_meshVerticesBuffer.count, vertexCount);
                int triangleRequestSize = Mathf.Min(m_nativeArrayTriangles.Length, m_meshTrianglesBuffer.count, triangleCount * 3);

                AsyncGPUReadbackRequest vertexRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayVertices, m_meshVerticesBuffer, vertexRequestSize * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest normalRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayNormals, m_meshNormalsBuffer, vertexRequestSize * sizeof(float) * 3, 0);
                AsyncGPUReadbackRequest uvRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayUVs, m_meshUVsBuffer, vertexRequestSize * sizeof(float) * 2, 0);
                AsyncGPUReadbackRequest triangleRequest = AsyncGPUReadback.RequestIntoNativeArray(ref m_nativeArrayTriangles, m_meshTrianglesBuffer, triangleRequestSize * sizeof(int), 0);

                while (!vertexRequest.done && !normalRequest.done && !uvRequest.done && !triangleRequest.done)
                    yield return null;

                SetMeshData(m_nativeArrayVertices, m_nativeArrayNormals, m_nativeArrayUVs, m_nativeArrayTriangles, vertexCount, triangleCount);
            }
            else
            {
                if (MeshRenderer)
                    MeshRenderer.enabled = false;

                if (MeshCollider)
                    MeshCollider.enabled = false;
            }

            m_isCoroutineRunning = false;
        }

        private void SetMeshData(NativeArray<Vector3> vertices, NativeArray<Vector3> normals, NativeArray<Vector2> uvs, NativeArray<int> indices, int vertexCount, int triangleCount)
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

        private bool TryGetOrCreateMeshGameObject()
        {
            if (m_mainSettings.OutputMode != OutputMode.MeshFilter)
                return false;

            if (m_meshFilterGameObject)
                return true;

            m_meshFilterGameObject = new GameObject(name + " Generated Mesh");
            m_meshFilterGameObject.transform.SetParent(transform);
            m_meshFilterGameObject.transform.Reset();

            return true;
        }

        public void DereferenceMeshObject()
        {
            m_meshFilterGameObject = null;
            m_meshFilter = null;
            m_meshRenderer = null;
        }

        /// <summary>
        /// Read the mesh counter buffer output and convert it into a simple vertex and triangle count.
        /// </summary>
        private void GetCounts(NativeArray<int> counter, out int vertexCount, out int triangleCount)
        {
            vertexCount = counter[VERTEX_COUNTER] + counter[INTERMEDIATE_VERTEX_COUNTER];
            triangleCount = counter[TRIANGLE_COUNTER];
        }

        #endregion

        #region Internal Compute Shader Stuff + Other Boring Boilerplate Methods

        private bool m_isInitializing = false;

        /// <summary>
        /// Do all the initial setup. This function should only be called once per 'session' because it does a lot of
        /// setup for buffers of constant size.
        /// </summary>
        private void InitializeComputeShaderSettings()
        {
            if (m_initialized || !m_isEnabled)
            {
                Debug.Log("Returning before initializing.");
                Debug.Log("m_initialized = " + m_initialized);
                Debug.Log("m_isEnabled = " + m_isEnabled);
                return;
            }

            ReleaseUnmanagedMemory();

            //bool wasAutoUpdating = m_mainSettings.AutoUpdate;
            //m_autoUpdate = false;

            m_isInitializing = true;
            m_initialized = true;

            m_computeShaderInstance = Instantiate(ComputeShader);

            SendTransformToGPU();

            ResendKeywords();

            m_kernels = new Kernels(ComputeShader);

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
            //m_autoUpdate = wasAutoUpdating;
            m_isInitializing = false;
        }

        /// <summary>
        /// Create the buffers which will need to be recreated and reset if certain settings change, such as cell count.
        /// </summary>
        private void CreateVariableBuffers()
        {
            int countCubed = m_voxelSettings.TotalSampleCount;

            m_computeShaderInstance.SetInt(Properties.PointsPerSide_Int, m_voxelSettings.SamplesPerSide);

            if (m_vertices.IsNullOrEmpty() || m_vertices.Length != countCubed)
                m_vertices = new VertexData[countCubed];

            if (m_triangles.IsNullOrEmpty() || m_triangles.Length != countCubed)
                m_triangles = new TriangleData[countCubed];

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

            if (m_mainSettings.ProceduralMaterial)
            {
                if (m_propertyBlock == null)
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

            m_bounds = new Bounds { extents = m_voxelSettings.Extents };

            ResetCounters();
            SetVertexData();
            SetTriangleData();
        }

        /// <summary>
        /// Buffers and NativeArrays are unmanaged and Unity will cry if we don't do this.
        /// </summary>
        private void ReleaseUnmanagedMemory()
        {
            StopAllCoroutines();
            m_isCoroutineRunning = false;

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

            // need to do this because some of the below native arrays might be 'in use' by requests
            AsyncGPUReadback.WaitAllRequests();

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

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
            {
                if (!m_initialized)
                    InitializeComputeShaderSettings();

                UpdateMesh();
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
            m_computeShaderInstance.Dispatch(m_kernels.Map, Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)x), Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)y), Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)z));
        }

        private void DispatchGenerateVertices()
        {
            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.GenerateVertices, out uint x, out uint y, out uint z);
            m_computeShaderInstance.Dispatch(m_kernels.GenerateVertices, Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)x), Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)y), Mathf.CeilToInt(m_voxelSettings.SamplesPerSide / (float)z));
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

            if (TryGetOrCreateMeshGameObject())
                m_computeShaderInstance.SetMatrix(Properties.MeshTransform_Matrix4x4, m_meshFilterGameObject.transform.worldToLocalMatrix);
            else if (m_mainSettings.OutputMode == OutputMode.Procedural)
                m_computeShaderInstance.SetMatrix(Properties.MeshTransform_Matrix4x4, Matrix4x4.identity);
        }

        public void OnCellCountChanged()
        {
            m_bounds = new Bounds { extents = m_voxelSettings.Extents };

            if (!m_initialized || !m_isEnabled)
                return;

            CreateVariableBuffers();

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnCellSizeChanged()
        {
            m_bounds = new Bounds { extents = m_voxelSettings.Extents };

            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.CellSize_Float, m_voxelSettings.CellSize);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnConstrainToCellUnitsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.ConstrainToCellUnits_Float, m_algorithmSettings.ConstrainToCellUnits);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnVisualNormalSmoothingChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.VisualNormalSmoothing, m_algorithmSettings.VisualNormalSmoothing);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnMaxAngleToleranceChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.MaxAngleCosine_Float, Mathf.Cos(m_algorithmSettings.MaxAngleTolerance * Mathf.Deg2Rad));

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnGradientDescentIterationsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetInt(Properties.GradientDescentIterations_Int, m_algorithmSettings.GradientDescentIterations);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnBinarySearchIterationsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetInt(Properties.BinarySearchIterations_Int, m_algorithmSettings.BinarySearchIterations);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnIsosurfaceExtractionTypeChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_algorithmSettings.IsosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                m_computeShaderInstance.EnableKeyword(DualContouringKeyword);
            else
                m_computeShaderInstance.DisableKeyword(DualContouringKeyword);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnEdgeIntersectionTypeChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_algorithmSettings.EdgeIntersectionType == EdgeIntersectionType.BinarySearch)
                m_computeShaderInstance.DisableKeyword(InterpolationKeyword);
            else
                m_computeShaderInstance.EnableKeyword(InterpolationKeyword);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnNudgeSettingsChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.NudgeVerticesToAverageNormalScalar_Float, m_algorithmSettings.NudgeVerticesToAverageNormalScalar);
            m_computeShaderInstance.SetFloat(Properties.NudgeMaxMagnitude_Float, m_algorithmSettings.NudgeMaxMagnitude);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnApplyGradientDescentChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            if (m_algorithmSettings.ApplyGradientDescent)
                m_computeShaderInstance.EnableKeyword(ApplyGradientDescentKeyword);
            else
                m_computeShaderInstance.DisableKeyword(ApplyGradientDescentKeyword);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void OnQEFSettingsOverrideChanged()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(Properties.QEFPseudoInverseThreshold_Float, m_algorithmSettings.QefPseudoInverseThreshold);
            m_computeShaderInstance.SetInt(Properties.QEFSweeps_Int, m_algorithmSettings.QefSweeps);

            if (m_algorithmSettings.OverrideQEFSettings)
                m_computeShaderInstance.EnableKeyword(OverrideQEFSettingsKeyword);
            else
                m_computeShaderInstance.DisableKeyword(OverrideQEFSettingsKeyword);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }
        
        public void OnOutputModeChanged()
        {
            if (TryGetOrCreateMeshGameObject())
            {
                m_meshFilterGameObject.SetActive(true);

                if (MeshRenderer)
                    MeshRenderer.enabled = !Group.IsEmpty;
            }
            else if (m_mainSettings.OutputMode == OutputMode.Procedural)
            {
                if (m_meshFilterGameObject)
                    m_meshFilterGameObject.SetActive(false);
            }

            SendTransformToGPU();
            Group.RequestUpdate(onlySendBufferOnChange: false);
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

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void UpdateSettingsBuffer(ComputeBuffer computeBuffer)
        {
            if (!m_isEnabled)
                return;

            if (!m_initialized)
                InitializeComputeShaderSettings();

            UpdateMapKernels(Properties.Settings_StructuredBuffer, computeBuffer);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
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
                MeshRenderer.enabled = m_mainSettings.OutputMode == OutputMode.MeshFilter;
        }

        public void OnPrimitivesChanged()
        {
            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        #endregion

        #region Grid Helper Functions

        public Vector3 CellCoordinateToVertex(int x, int y, int z)
        {
            float gridSize = (float)(m_voxelSettings.SamplesPerSide - 1f);
            float bound = (gridSize / 2f) * m_voxelSettings.CellSize;

            float xPos = Mathf.LerpUnclamped(-bound, bound, x / gridSize);
            float yPos = Mathf.LerpUnclamped(-bound, bound, y / gridSize);
            float zPos = Mathf.LerpUnclamped(-bound, bound, z / gridSize);

            return new Vector3(xPos, yPos, zPos);
        }

        public Vector3 CellCoordinateToVertex(Vector3Int vec) =>
            CellCoordinateToVertex(vec.x, vec.y, vec.z);

        public Vector3Int IndexToCellCoordinate(int index)
        {
            int samplesPerSide = m_voxelSettings.SamplesPerSide;

            int z = index / (samplesPerSide * samplesPerSide);
            index -= (z * samplesPerSide * samplesPerSide);
            int y = index / samplesPerSide;
            int x = index % samplesPerSide;

            return new Vector3Int(x, y, z);
        }

        public Vector3 IndexToVertex(int index)
        {
            Vector3Int coords = IndexToCellCoordinate(index);
            return CellCoordinateToVertex(coords.x, coords.y, coords.z);
        }

        public int CellCoordinateToIndex(int x, int y, int z) =>
            (x + y * m_voxelSettings.SamplesPerSide + z * m_voxelSettings.SamplesPerSide * m_voxelSettings.SamplesPerSide);

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

        #endregion

        #region Data Classes

        [System.Serializable]
        public class MainSettings
        {
            [SerializeField]
            private bool m_autoUpdate = true;
            public bool AutoUpdate => m_autoUpdate;

            [SerializeField]
            private OutputMode m_outputMode = OutputMode.Procedural;
            public OutputMode OutputMode => m_outputMode;

            [SerializeField]
            private bool m_isAsynchronous = false;
            public bool IsAsynchronous => m_isAsynchronous;

            [SerializeField]
            private Material m_proceduralMaterial;
            public Material ProceduralMaterial => m_proceduralMaterial;

            public MainSettings Copy()
            {
                return new MainSettings()
                {
                    m_autoUpdate = this.m_autoUpdate,
                    m_outputMode = this.m_outputMode,
                    m_isAsynchronous = this.m_isAsynchronous,
                    m_proceduralMaterial = this.m_proceduralMaterial
                };
            }
        }

        [System.Serializable]
        public class VoxelSettings
        {
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

            public int SamplesPerSide => CellCount + 1;
            public int TotalSampleCount
            {
                get
                {
                    int samplesPerSide = CellCount + 1;
                    return samplesPerSide * samplesPerSide * samplesPerSide;
                }
            }

            public Vector3 Extents => Vector3.one * CellCount * CellSize;

            /// <summary>
            /// Returns the distance, along any axis, along which an additional volume must be positioned in order to perfectly overlap with this one.
            /// For example, if this volume is at the origin, with a cellcount of 50 and a cellsize of 0.1, a volume to the left must be placed at (-4.8, 0, 0).
            /// </summary>
            public float OffsetDistance => (CellCount - 2) * CellSize;

            public VoxelSettings Copy()
            {
                return new VoxelSettings()
                {
                    m_cellSizeMode = this.m_cellSizeMode,
                    m_cellSize = this.m_cellSize,
                    m_cellCount = this.m_cellCount,
                    m_volumeSize = this.m_volumeSize,
                    m_cellDensity = this.m_cellDensity
                };
            }
        }

        [System.Serializable]
        public class AlgorithmSettings
        {
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

            public AlgorithmSettings Copy()
            {
                return new AlgorithmSettings()
                {
                    m_maxAngleTolerance = this.m_maxAngleTolerance,
                    m_visualNormalSmoothing = this.m_visualNormalSmoothing,
                    m_isosurfaceExtractionType = this.m_isosurfaceExtractionType,
                    m_constrainToCellUnits = this.m_constrainToCellUnits,
                    m_overrideQEFSettings = this.m_overrideQEFSettings,
                    m_qefSweeps = this.m_qefSweeps,
                    m_qefPseudoInverseThreshold = this.m_qefPseudoInverseThreshold,
                    m_edgeIntersectionType = this.m_edgeIntersectionType,
                    m_binarySearchIterations = this.m_binarySearchIterations,
                    m_applyGradientDescent = this.m_applyGradientDescent,
                    m_gradientDescentIterations = this.m_gradientDescentIterations,
                    m_nudgeVerticesToAverageNormalScalar = this.m_nudgeVerticesToAverageNormalScalar,
                    m_nudgeMaxMagnitude = this.m_nudgeMaxMagnitude,
                };
            }
        }

        #endregion

        #region Static Methods

        public SDFGroupMeshGenerator Duplicate(Vector3Int offset = default) => Duplicate(this, offset);

        /// <summary>
        /// Duplicate this object in a way that's fast and works in editor and in builds, in play mode and in edit mode.
        /// </summary>
        public static SDFGroupMeshGenerator Duplicate(SDFGroupMeshGenerator original, Vector3Int offset = default)
        {
            GameObject cloneObject = new GameObject(original.name + " Clone");
            cloneObject.transform.SetParent(original.transform.parent);
            cloneObject.transform.position = original.transform.position + (Vector3)offset * original.m_voxelSettings.OffsetDistance;
            cloneObject.SetActive(false);

            GameObject cloneMeshObject = null;

            if (original.m_mainSettings.OutputMode == OutputMode.MeshFilter && original.m_meshFilterGameObject)
            {
                GameObject originalMeshObject = original.m_meshFilterGameObject;

                cloneMeshObject = new GameObject(original.name + " Generated Mesh", typeof(MeshFilter));
                cloneMeshObject.transform.SetParent(cloneObject.transform);
                cloneMeshObject.transform.Reset();
                
                if (originalMeshObject.TryGetComponent(out MeshRenderer originalMeshRenderer))
                {
                    MeshRenderer clonedMeshRenderer = cloneMeshObject.AddComponent<MeshRenderer>();
                    clonedMeshRenderer.sharedMaterial = originalMeshRenderer.sharedMaterial;
                }

                if (originalMeshObject.TryGetComponent(out MeshCollider originalMeshCollider))
                {
                    cloneMeshObject.AddComponent<MeshCollider>();
                }
            }

            SDFGroupMeshGenerator clonedComponent = cloneObject.AddComponent<SDFGroupMeshGenerator>();

            clonedComponent.m_meshFilterGameObject = cloneMeshObject;
            clonedComponent.m_group = original.m_group;
            clonedComponent.m_mainSettings = original.m_mainSettings.Copy();
            clonedComponent.m_algorithmSettings = original.m_algorithmSettings.Copy();
            clonedComponent.m_voxelSettings = original.m_voxelSettings.Copy();

            cloneObject.SetActive(original.gameObject.activeSelf);

            // Move directly underneath original
            cloneObject.transform.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);

#if UNITY_EDITOR
            // Select new object
            Selection.activeGameObject = cloneObject;

            // Register Undo
            Undo.RegisterCreatedObjectUndo(cloneObject, "Duplicated SDF Mesh Generator");
#endif

            return clonedComponent;
        }

        #endregion
    }

    public enum IsosurfaceExtractionType { SurfaceNets, DualContouring };
    public enum EdgeIntersectionType { Interpolation, BinarySearch };

    public enum CellSizeMode { Fixed, Density };
    public enum OutputMode { MeshFilter, Procedural };
}