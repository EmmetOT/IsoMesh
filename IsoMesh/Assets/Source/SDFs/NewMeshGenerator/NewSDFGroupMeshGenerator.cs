using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh.New
{
    /// <summary>
    /// This class passes SDF data to an isosurface extraction compute shader and returns a mesh.
    /// This mesh can be passed directly to a material as a triangle and index buffer in 'Procedural' mode,
    /// or transfered to the CPU and sent to a MeshFilter in 'Mesh' mode.
    /// </summary>
    [ExecuteInEditMode]
    public partial class NewSDFGroupMeshGenerator : MonoBehaviour, ISDFGroupComponent
    {
        #region Consts

        private const string COMPUTE_SHADER_RESOURCE = "Compute_New_IsoSurfaceExtraction";

        //private const int OCTTREE_HASHMAP_CAPACITY = 32768;
        //private const int MAX_NODES = 1024 * 1024;
        //private static int MAX_VERTICES = MAX_NODES * 3;

        // indirect args offsets (multiply by sizeof(int))
        private const int VERTEX_COUNTER = 0;
        private const int TRIANGLE_COUNTER = 3;
        private const int VERTEX_COUNTER_DIV_64 = 6;
        private const int VERTEX_COUNTER_DIV_3 = 9;

        #endregion

        public int HashmapCapacity = 32768;
        public int MaxNodes = 1024 * 1024;
        public int MaxVertices = 1024 * 1024 * 3;

        #region Fields/Properties

        [SerializeField]
        private MainSettings m_mainSettings = new MainSettings();
        public MainSettings MainSettings => m_mainSettings;

        [SerializeField]
        private VoxelSettings m_voxelSettings = new VoxelSettings();
        public VoxelSettings VoxelSettings => m_voxelSettings;

        [SerializeField]
        private AlgorithmSettings m_algorithmSettings = new AlgorithmSettings();
        public AlgorithmSettings AlgorithmSettings => m_algorithmSettings;

        [SerializeField]
        private DebugSettings m_debugSettings = new DebugSettings();
        public DebugSettings DebugSettings => m_debugSettings;

        private Kernels m_kernels;
        private Buffers m_buffers;

        [SerializeField]
        private ComputeShader m_computeShader;
        private ComputeShader ComputeShader
        {
            get
            {
                if (m_computeShader)
                    return m_computeShader;

                return m_computeShader = Resources.Load<ComputeShader>(COMPUTE_SHADER_RESOURCE);
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

        private bool m_isInitializing = false;
        private bool m_initialized = false;
        private bool m_isEnabled = false;

        private Bounds m_bounds;
        private MaterialPropertyBlock m_propertyBlock;

        #endregion

        #region Monobehaviour Callbacks

        private void Reset()
        {
            m_initialized = false;
        }

        private void OnEnable()
        {
            m_isEnabled = true;
            m_initialized = false;

            if (Group.IsReady)
            {
                Initialize();
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

        private void Update()
        {
            if (transform.hasChanged && Group.IsReady && !Group.IsEmpty && Group.IsRunning)
                UpdateMesh();

            transform.hasChanged = false;
        }

        private void LateUpdate()
        {
            if (!m_initialized || !Group.IsReady || Group.IsEmpty)
                return;

            if (m_mainSettings.OutputMode == OutputMode.Procedural && m_mainSettings.ProceduralMaterial && !m_buffers.ProceduralArgs.IsNullOrInvalid() && m_mainSettings.AutoUpdate)
                Graphics.DrawProceduralIndirect(m_mainSettings.ProceduralMaterial, m_bounds, MeshTopology.Triangles, m_buffers.ProceduralArgs, properties: m_propertyBlock);
        }

        private void OnUndo()
        {
            if (m_initialized)
            {
                m_initialized = false;
                Initialize();
                m_group.RequestUpdate();
            }
        }

        #endregion

        #region Interface Stuff

        public void OnEmpty() { }

        public void OnNotEmpty() { }

        public void Run()
        {
            // this only happens when unity is recompiling. this is annoying and hacky but it works.
            if (m_buffers == null || m_buffers.Counter.IsNullOrInvalid())
                return;

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
            {
                if (!m_initialized)
                    Initialize();

                UpdateMesh();
            }
        }

        public void UpdateDataBuffers(ComputeBuffer dataBuffer, ComputeBuffer materialBuffer, int count)
        {
            if (!m_isEnabled)
                return;

            if (!m_initialized)
                Initialize();

            UpdateMapKernels(Properties.SDFData_StructuredBuffer, dataBuffer);
            UpdateMapKernels(Properties.SDFMaterials_StructuredBuffer, materialBuffer);
            m_computeShaderInstance.SetInt(Properties.SDFDataCount_Int, count);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        public void UpdateSettingsBuffer(ComputeBuffer computeBuffer)
        {
            if (!m_isEnabled)
                return;

            if (!m_initialized)
                Initialize();

            UpdateMapKernels(Properties.Settings_StructuredBuffer, computeBuffer);

            if (m_mainSettings.AutoUpdate && !m_isInitializing)
                UpdateMesh();
        }

        #endregion

        #region Initialization and Main Loop

        private void Initialize()
        {
            if (m_initialized || !m_isEnabled)
                return;

            ReleaseUnmanagedMemory();

            m_isInitializing = true;
            m_initialized = true;

            m_computeShaderInstance = Instantiate(ComputeShader);

            SendTransformToGPU();

            m_kernels = new Kernels(m_computeShaderInstance);
            m_buffers = new Buffers();

            m_buffers.CreateBuffers(HashmapCapacity, MaxVertices, MaxNodes);

            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_AllocateEmptyNodeHashMap, Properties.OcttreeRootHashMap_StructuredBuffer, m_buffers.OcttreeRootHashMap);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindRoots, Properties.OcttreeRootHashMap_StructuredBuffer, m_buffers.OcttreeRootHashMap);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindRoots, Properties.OcttreeIndirectArgs, m_buffers.OcttreeIndirectArgs);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindRoots, Properties.OcttreeNodeBuffer_AppendBuffer, m_buffers.OcttreeNodeBuffer_Two);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindSurfaceNodes, Properties.OcttreeIndirectArgs, m_buffers.OcttreeIndirectArgs);
            //m_computeShaderInstance.SetBuffer(m_kernels.Octtree_SwapBuffers, Properties.OcttreeNodeBuffer_AppendBuffer, m_buffers.OcttreeNodeAppend);
            //m_computeShaderInstance.SetBuffer(m_kernels.Octtree_SwapBuffers, Properties.OcttreeNodeBuffer_ConsumeBuffer, m_buffers.OcttreeNodeConsume);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_SwapBufferCounts, Properties.OcttreeIndirectArgs, m_buffers.OcttreeIndirectArgs);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, Properties.OcttreeIndirectArgs, m_buffers.OcttreeIndirectArgs);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, Properties.Counter_RWBuffer, m_buffers.Counter);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, Properties.SurfacePoints_StructuredBuffer, m_buffers.SurfacePoints);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, Properties.ProceduralArgs_RWBuffer, m_buffers.ProceduralArgs);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, Properties.ProceduralArgs_RWBuffer, m_buffers.ProceduralArgs);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, Properties.Counter_RWBuffer, m_buffers.Counter);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, Properties.SurfacePoints_StructuredBuffer, m_buffers.SurfacePoints);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, Properties.SurfacePointMaterials_StructuredBuffer, m_buffers.SurfacePointMaterials);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Finalize, Properties.SurfacePoints_StructuredBuffer, m_buffers.SurfacePoints);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Finalize, Properties.Counter_RWBuffer, m_buffers.Counter);

            if (m_mainSettings.ProceduralMaterial)
            {
                if (m_propertyBlock == null)
                    m_propertyBlock = new MaterialPropertyBlock();

                m_propertyBlock.SetBuffer(Properties.SurfacePoints_StructuredBuffer, m_buffers.SurfacePoints);
                m_propertyBlock.SetBuffer(Properties.SurfacePointMaterials_StructuredBuffer, m_buffers.SurfacePointMaterials);
            }

            if (m_group)
            {
                if (!m_group.DataBuffer.IsNullOrInvalid() && !m_group.MaterialBuffer.IsNullOrInvalid())
                    UpdateDataBuffers(m_group.DataBuffer, m_group.MaterialBuffer, m_group.SDFElementsCount);

                if (!m_group.SettingsBuffer.IsNullOrInvalid())
                    UpdateSettingsBuffer(m_group.SettingsBuffer);
            }

            // none of these will update the mesh because m_isInitializing is set to false
            SetValue(Properties.HashMapCapacity_Int, HashmapCapacity);
            SetValue(Properties.VisualNormalSmoothing_Float, m_algorithmSettings.VisualNormalSmoothing);
            SetValue(Properties.MaxAngleCosine_Float, Mathf.Cos(m_algorithmSettings.MaxAngleTolerance * Mathf.Deg2Rad));
            SetValue(Properties.OcttreeDepth_Int, m_voxelSettings.OcttreeMaxNodeDepth);
            SetValue(Properties.OcttreeRootNodeSize_Float, m_voxelSettings.OcttreeRootNodeSize);
            SetValue(Properties.OcttreeNodePadding_Float, m_voxelSettings.OcttreeNodePadding);
            SetValue(Properties.CellSize_Float, m_voxelSettings.CellSize);
            SetValue(Properties.GradientDescentIterations_Int, m_algorithmSettings.GradientDescentIterations);
            SetValue(Properties.BinarySearchIterations_Int, m_algorithmSettings.BinarySearchIterations);
            SetValue(Properties.IsosurfaceExtractionType_Int, (int)m_algorithmSettings.IsosurfaceExtractionType);

            m_isInitializing = false;
        }

        /// <summary>
        /// Buffers and NativeArrays are unmanaged and Unity will cry if we don't do this.
        /// </summary>
        private void ReleaseUnmanagedMemory()
        {
            StopAllCoroutines();

            m_buffers?.ReleaseAll();

            m_initialized = false;

            if (m_computeShaderInstance)
                DestroyImmediate(m_computeShaderInstance);
        }

        public void UpdateMesh()
        {
            if (!m_initialized || !Group.IsReady || Group.IsEmpty)
                return;

            // data has changed somehow, so update the bounds to be
            // the minimum bounds which encapsulates all objects which add to the 'volume' of the mesh
            bool isFirst = true;
            m_bounds.SetMinMax(Vector3.zero, Vector3.zero);
            foreach (SDFObject obj in m_group.SDFObjects)
            {
                if (obj.Operation == SDFCombineType.SmoothUnion)
                {
                    if (isFirst)
                    {
                        m_bounds = obj.AABB;
                        isFirst = false;
                    }
                    else
                    {
                        m_bounds.Encapsulate(obj.AABB);
                    }
                }
            }

            DispatchAll();
        }

        #endregion

        #region Compute Shader

        /// <summary>
        /// Dispatch all the compute kernels in the correct order. Basically... do the thing.
        /// </summary>
        private void DispatchAll()
        {
            // this only happens when unity is recompiling. this is annoying and hacky but it works.
            if (m_buffers.Counter.IsNullOrInvalid())
                return;

            m_buffers.ResetCounters();

            UpdateMapKernels(Properties.Settings_StructuredBuffer, Group.SettingsBuffer); // todo: probably not necessary

            DispatchFindRootNodes();
            ComputeBuffer finalLeafNodesBuffer = DispatchFindSurfaceNodes();
            DispatchTriangulate(finalLeafNodesBuffer);
            DispatchApplyQEF(finalLeafNodesBuffer);
            DispatchFinalizeMesh();
        }

        /// <summary>
        /// In this part of the algorithm, we pass in the bounds of all the objects and find all the unique root nodes which fully surround them all.
        /// </summary>
        private void DispatchFindRootNodes()
        {
            // completely wipe the buffer
            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.Octtree_AllocateEmptyNodeHashMap, out uint x, out _, out _);
            m_computeShaderInstance.Dispatch(m_kernels.Octtree_AllocateEmptyNodeHashMap, Mathf.CeilToInt(HashmapCapacity / (float)x), 1, 1);

            // data is ready, attempt to find the overlapping grid cells
            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.Octtree_FindRoots, out x, out _, out _);
            m_computeShaderInstance.Dispatch(m_kernels.Octtree_FindRoots, Mathf.CeilToInt(m_group.SDFElementsCount / (float)x), 1, 1);

            Debug.Log($"m_debugSettings.ShowIteration(0) = {m_debugSettings.ShowIteration(0)}");

            if (m_debugSettings.ShowIteration(0))
            {
                int[] indirectArgs = m_buffers.GetIndirectArgs();
                OcttreeNode[] nodes = new OcttreeNode[indirectArgs[3]];
                m_buffers.OcttreeNodeBuffer_Two.GetData(nodes);

                m_debugNodes[0] = nodes;

                Debug.Log($"[CREATING] {nodes.Length} nodes at index {0}");
            }
        }



        private ComputeBuffer DispatchFindSurfaceNodes()
        {
            // after finding root nodes, root nodes are stored in buffer two

            ComputeBuffer bufferAppend = m_buffers.OcttreeNodeBuffer_One;
            ComputeBuffer bufferConsume = m_buffers.OcttreeNodeBuffer_Two;

            // ensure count of buffer two is now references as consume count
            m_computeShaderInstance.Dispatch(m_kernels.Octtree_SwapBufferCounts, 1, 1, 1);

            //int[] indirectArgs = m_buffers.GetIndirectArgs();

            //Debug.Log("Find surface nodes.");
            //Debug.Log($"Append count at very beginning = {indirectArgs[5]}".AddColour(Color.yellow));
            //Debug.Log($"Consume count at very beginning = {indirectArgs[6]}".AddColour(Color.yellow));


            // this kernel is dispatched a number of times, with the nodes getting smaller by a factor of 8 every time
            for (int i = 0; i < m_voxelSettings.OcttreeMaxNodeDepth; i++)
            {
                //indirectArgs = m_buffers.GetIndirectArgs();

                //Debug.Log($"BEFORE ITERATION {i} COUNT IS: {indirectArgs.ToFormattedString()}".AddColour(Color.cyan));


                //OcttreeNode[] consumeNodes = new OcttreeNode[indirectArgs[6]];
                //bufferConsume.GetData(consumeNodes);
                //Debug.Log($"Input in iteration {i}: {consumeNodes.ToFormattedString()}".AddColour(Color.red));


                m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindSurfaceNodes, Properties.OcttreeNodeBuffer_AppendBuffer, bufferAppend);
                m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindSurfaceNodes, Properties.OcttreeNodeBuffer_ConsumeBuffer, bufferConsume);
                m_computeShaderInstance.DispatchIndirect(m_kernels.Octtree_FindSurfaceNodes, m_buffers.OcttreeIndirectArgs);

                //indirectArgs = m_buffers.GetIndirectArgs();

                //Debug.Log($"Append count after iteration {i} = {indirectArgs[5]}".AddColour(Color.yellow));
                //Debug.Log($"Consume count after iteration {i} = {indirectArgs[6]}".AddColour(Color.yellow));

                //OcttreeNode[] nodes = new OcttreeNode[indirectArgs[5]];
                //bufferAppend.GetData(nodes);
                //Debug.Log($"Output in iteration {i}: {nodes.ToFormattedString()}".AddColour(Color.magenta));

                //nodes = new OcttreeNode[indirectArgs[3] * 2];
                //m_buffers.OcttreeNodeBuffer_Two.GetData(nodes);
                //Debug.Log($"The first {nodes.Length}: {nodes.ToFormattedString()}");

                if (m_debugSettings.ShowIteration(i + 1))
                {
                    int[] indirectArgs = m_buffers.GetIndirectArgs();

                    OcttreeNode[] nodes = new OcttreeNode[indirectArgs[3]];
                    bufferAppend.GetData(nodes);

                    m_debugNodes[i + 1] = nodes;
                }


                //Debug.Log($"BEFORE SWAP {i} COUNT IS: {indirectArgs.ToFormattedString()}".AddColour(Color.cyan));

                // switcheroo ;)
                // the newest data should always end up in bufferConsume
                (bufferAppend, bufferConsume) = (bufferConsume, bufferAppend);
                m_computeShaderInstance.Dispatch(m_kernels.Octtree_SwapBufferCounts, 1, 1, 1);


                //indirectArgs = m_buffers.GetIndirectArgs();

                //Debug.Log($"AFTER SWAP {i} COUNT IS: {indirectArgs.ToFormattedString()}".AddColour(Color.cyan));

                //Debug.Log("--------------------------");

            }

            //Debug.Log("=====================================================");

            return bufferConsume;
        }

        private void DispatchTriangulate(ComputeBuffer leafNodesBuffer)
        {
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, Properties.OcttreeLeafNodes_StructuredBuffer, leafNodesBuffer);

            m_computeShaderInstance.GetKernelThreadGroupSizes(m_kernels.Mesh_Triangulate, out uint x, out _, out _);
            m_computeShaderInstance.Dispatch(m_kernels.Mesh_Triangulate, Mathf.CeilToInt(MaxVertices / (float)x), 1, 1);
        }

        private void DispatchApplyQEF(ComputeBuffer leafNodesBuffer)
        {
            if (m_algorithmSettings.IsosurfaceExtractionType == IsosurfaceExtractionType.Voxels)
                return;

            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, Properties.OcttreeLeafNodes_StructuredBuffer, leafNodesBuffer);

            m_computeShaderInstance.DispatchIndirect(m_kernels.Mesh_ApplyQEF, m_buffers.Counter, VERTEX_COUNTER_DIV_64 * sizeof(int));
        }

        private void DispatchFinalizeMesh()
        {
            m_computeShaderInstance.DispatchIndirect(m_kernels.Mesh_Finalize, m_buffers.Counter, VERTEX_COUNTER_DIV_3 * sizeof(int));

            //Debug.Log(m_buffers.GetVertexCount());
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

            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindRoots, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.Octtree_FindSurfaceNodes, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Triangulate, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_ApplyQEF, id, buffer);
            m_computeShaderInstance.SetBuffer(m_kernels.Mesh_Finalize, id, buffer);
        }

        private void SetValue(int nameID, float val, bool updateMesh = true)
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetFloat(nameID, val);

            if (m_mainSettings.AutoUpdate && !m_isInitializing && updateMesh)
                UpdateMesh();
        }

        private void SetValue(int nameID, int val, bool apply = true)
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetInt(nameID, val);

            if (m_mainSettings.AutoUpdate && !m_isInitializing && apply)
                UpdateMesh();
        }

        private void SendTransformToGPU()
        {
            if (!m_initialized || !m_isEnabled)
                return;

            m_computeShaderInstance.SetMatrix(Properties.Transform_Matrix4x4, transform.localToWorldMatrix);
            m_computeShaderInstance.SetMatrix(Properties.MeshTransform_Matrix4x4, Matrix4x4.identity);
        }

        #endregion

        #region Editor Stuff

        public void OnHashMapCapacityChanged()
        {
            m_initialized = false;
            Initialize();
        }

        public void OnMaxNodesChanged()
        {
            m_initialized = false;
            Initialize();
        }

        public void OnMaxVerticesChanged()
        {
            m_initialized = false;
            Initialize();
        }

        public void OnOcttreeNodePaddingChanged()
        {
            SetValue(Properties.OcttreeNodePadding_Float, m_voxelSettings.OcttreeNodePadding);
        }

        public void OnOcttreeMaxNodeDepthChanged()
        {
            SetValue(Properties.OcttreeDepth_Int, m_voxelSettings.OcttreeMaxNodeDepth, updateMesh: false);
            SetValue(Properties.OcttreeRootNodeSize_Float, m_voxelSettings.OcttreeRootNodeSize);
        }

        public void OnCellSizeChanged()
        {
            SetValue(Properties.CellSize_Float, m_voxelSettings.CellSize, updateMesh: false);
            SetValue(Properties.OcttreeRootNodeSize_Float, m_voxelSettings.OcttreeRootNodeSize);
        }

        public void OnGradientDescentIterationsChanged() => SetValue(Properties.GradientDescentIterations_Int, m_algorithmSettings.GradientDescentIterations);

        public void OnBinarySearchIterationsChanged() => SetValue(Properties.BinarySearchIterations_Int, m_algorithmSettings.BinarySearchIterations);

        public void OnIsosurfaceExtractionTypeChanged() => SetValue(Properties.IsosurfaceExtractionType_Int, (int)m_algorithmSettings.IsosurfaceExtractionType);

        public void OnVisualNormalSmoothingChanged() => SetValue(Properties.VisualNormalSmoothing_Float, m_algorithmSettings.VisualNormalSmoothing);

        public void OnMaxAngleToleranceChanged() => SetValue(Properties.MaxAngleCosine_Float, Mathf.Cos(m_algorithmSettings.MaxAngleTolerance * Mathf.Deg2Rad));

        private OcttreeNode[][] m_debugNodes = new OcttreeNode[9][];

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            for (int i = 0; i < m_debugNodes.Length; i++)
            {
                if (m_debugSettings.ShowIteration(i))
                {
                    OcttreeNode[] nodes = m_debugNodes[i];

                    if (!nodes.IsNullOrEmpty())
                    {
                        HashSet<float> widths = new HashSet<float>();

                        Color col = Utils.RainbowLerp(i / (m_debugNodes.Length - 1f));

                        Handles.color = col;

                        for (int j = 0; j < nodes.Length; j++)
                        {
                            widths.Add(nodes[j].Width);
                            Handles.DrawWireCube(nodes[j].Position, Vector3.one * nodes[j].Width);

                            if (m_debugSettings.ShowDistances)
                            {
                                Gizmos.color = col;
                                Gizmos.DrawSphere(nodes[j].Position, 0.05f);

                                Utils.Label(nodes[j].Position, nodes[j].Distance, size: 15, line: -2, col: col);
                            }
                        }

                        //Debug.Log($"[DRAWING] At depth {(i + 1)} there are {widths.Count} widths: {widths.ToArray().ToFormattedString()}");
                    }
                }
            }
        }

#endif

        #endregion

        #region Subclasses

        private class Buffers
        {
            // counter buffer has 18 integers: [vertex count, 1, 1, triangle count, 1, 1, vertex count / 64, 1, 1, vertex count / 3, 1, 1]
            private readonly int[] m_counterArray = new int[] { 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1 };
            private readonly int[] m_counterOutputArray = new int[12];

            private readonly int[] m_octtreeIndirectArgsData = new int[] { 1, 1, 1, 0, 0, 0, 0 }; // indirect args, and then current node count, and then root node collisions, then append count, then consume count
            private readonly int[] m_proceduralArgsArray = new int[] { 0, 1, 0, 0, 0 };

            public ComputeBuffer OcttreeIndirectArgs { get; private set; } = null;
            public ComputeBuffer OcttreeNodeBuffer_One { get; private set; } = null;
            public ComputeBuffer OcttreeNodeBuffer_Two { get; private set; } = null;
            public ComputeBuffer OcttreeRootHashMap { get; private set; } = null;
            public ComputeBuffer ProceduralArgs { get; private set; } = null;
            public ComputeBuffer Counter { get; private set; } = null;
            public ComputeBuffer SurfacePoints { get; private set; } = null;
            public ComputeBuffer SurfacePointMaterials { get; private set; } = null;

            public void ReleaseAll()
            {
                OcttreeIndirectArgs?.Dispose();
                OcttreeNodeBuffer_One?.Dispose();
                OcttreeNodeBuffer_Two?.Dispose();
                OcttreeRootHashMap?.Dispose();
                ProceduralArgs?.Dispose();
                Counter?.Dispose();
                SurfacePoints?.Dispose();
                SurfacePointMaterials?.Dispose();
            }

            /// <summary>
            /// Reset count of append buffers and buffers containing count data.
            /// </summary>
            public void ResetCounters()
            {
                OcttreeIndirectArgs.SetData(m_octtreeIndirectArgsData);
                ProceduralArgs?.SetData(m_proceduralArgsArray);
                Counter?.SetData(m_counterArray);

                OcttreeNodeBuffer_One?.SetCounterValue(0);
                OcttreeNodeBuffer_Two?.SetCounterValue(0);
            }

            /// <summary>
            /// Constant buffers don't typically change size in the lifetime of a mesh generator.
            /// </summary>
            public void CreateBuffers(int hashmapCapacity, int maxVertices, int maxNodes)
            {
                OcttreeRootHashMap = new ComputeBuffer(hashmapCapacity, KeyValue.Stride, ComputeBufferType.Structured);
                OcttreeIndirectArgs = new ComputeBuffer(m_octtreeIndirectArgsData.Length, sizeof(int), ComputeBufferType.IndirectArguments);
                Counter = new ComputeBuffer(m_counterArray.Length, sizeof(int), ComputeBufferType.IndirectArguments);
                ProceduralArgs = new ComputeBuffer(m_proceduralArgsArray.Length, sizeof(int), ComputeBufferType.IndirectArguments);
                SurfacePoints = new ComputeBuffer(maxVertices, SurfacePoint.Stride, ComputeBufferType.Structured);
                SurfacePointMaterials = new ComputeBuffer(maxVertices, SDFMaterialGPU.Stride, ComputeBufferType.Structured);
                OcttreeNodeBuffer_One = new ComputeBuffer(maxNodes, OcttreeNode.Stride, ComputeBufferType.Structured);
                OcttreeNodeBuffer_Two = new ComputeBuffer(maxNodes, OcttreeNode.Stride, ComputeBufferType.Structured);

                ResetCounters();
            }

            public int[] GetIndirectArgs()
            {
                if (OcttreeIndirectArgs.IsNullOrInvalid())
                    return null;

                int[] indirectArgs = new int[m_octtreeIndirectArgsData.Length];
                OcttreeIndirectArgs.GetData(indirectArgs);
                return indirectArgs;
            }

            public int[] GetCounterData()
            {
                if (Counter.IsNullOrInvalid())
                    return null;

                int[] counterData = new int[m_counterArray.Length];
                Counter.GetData(counterData);
                return counterData;
            }

            public int GetVertexCount()
            {
                if (Counter.IsNullOrInvalid())
                    return 0;

                Counter.GetData(m_counterOutputArray);
                return m_counterOutputArray[0];
            }

            public void GetCounterData(int[] dataArray)
            {
                if (Counter.IsNullOrInvalid())
                    return;

                Counter.GetData(dataArray);
            }

            public SurfacePoint[] GetSurfacePointData()
            {
                if (SurfacePoints.IsNullOrInvalid() || Counter.IsNullOrInvalid())
                    return null;

                int[] counterData = new int[m_counterArray.Length];
                Counter.GetData(counterData);

                SurfacePoint[] points = new SurfacePoint[counterData[0]];
                SurfacePoints.GetData(points);
                return points;
            }
        }

        private static class Properties
        {
            public static readonly int OcttreeNodeBuffer_AppendBuffer = Shader.PropertyToID("_OcttreeNodeBuffer_Append");
            public static readonly int OcttreeNodeBuffer_ConsumeBuffer = Shader.PropertyToID("_OcttreeNodeBuffer_Consume");
            public static readonly int OcttreeIndirectArgs = Shader.PropertyToID("_OcttreeIndirectArgs");
            public static readonly int OcttreeNodePadding_Float = Shader.PropertyToID("_OcttreeNodePadding");
            public static readonly int OcttreeRootNodeSize_Float = Shader.PropertyToID("_RootSize");
            public static readonly int OcttreeDepth_Int = Shader.PropertyToID("_OcttreeDepth");
            public static readonly int OcttreeRootHashMap_StructuredBuffer = Shader.PropertyToID("_OcttreeRootHashMap");
            public static readonly int HashMapCapacity_Int = Shader.PropertyToID("_Capacity");
            public static readonly int OcttreeLeafNodes_StructuredBuffer = Shader.PropertyToID("_OcttreeLeafNodes");

            public static readonly int Counter_RWBuffer = Shader.PropertyToID("_Counter");

            public static readonly int SurfacePoints_StructuredBuffer = Shader.PropertyToID("_SurfacePoints");
            public static readonly int SurfacePointMaterials_StructuredBuffer = Shader.PropertyToID("_SurfacePointMaterials");

            public static readonly int CellSize_Float = Shader.PropertyToID("_CellSize");

            public static readonly int Settings_StructuredBuffer = Shader.PropertyToID("_Settings");
            public static readonly int Transform_Matrix4x4 = Shader.PropertyToID("_GroupTransform");
            public static readonly int MeshTransform_Matrix4x4 = Shader.PropertyToID("_MeshTransform");

            public static readonly int SDFData_StructuredBuffer = Shader.PropertyToID("_SDFData");
            public static readonly int SDFMaterials_StructuredBuffer = Shader.PropertyToID("_SDFMaterials");
            public static readonly int SDFDataCount_Int = Shader.PropertyToID("_SDFDataCount");

            public static readonly int ProceduralArgs_RWBuffer = Shader.PropertyToID("_ProceduralArgs");

            public static readonly int BinarySearchIterations_Int = Shader.PropertyToID("_BinarySearchIterations");
            public static readonly int IsosurfaceExtractionType_Int = Shader.PropertyToID("_IsosurfaceExtractionType");
            public static readonly int GradientDescentIterations_Int = Shader.PropertyToID("_GradientDescentIterations");
            public static readonly int MaxAngleCosine_Float = Shader.PropertyToID("_MaxAngleCosine");
            public static readonly int VisualNormalSmoothing_Float = Shader.PropertyToID("_VisualNormalSmoothing");
        }

        private struct Kernels
        {
            public const string Octtree_AllocateEmptyNodeHashMap_Name = "Octtree_AllocateEmptyNodeHashMap";
            public const string Octtree_FindRoots_Name = "Octtree_FindRoots";
            public const string Octtree_FindSurfaceNodes_Name = "Octtree_FindSurfaceNodes";
            public const string Octtree_SwapBufferCounts_Name = "Octtree_SwapBufferCounts";

            public const string Mesh_Triangulate_Name = "Mesh_Triangulate";
            public const string Mesh_ApplyQEF_Name = "Mesh_ApplyQEF";
            public const string Mesh_Finalize_Name = "Mesh_Finalize";

            public int Octtree_AllocateEmptyNodeHashMap { get; }
            public int Octtree_FindRoots { get; }
            public int Octtree_FindSurfaceNodes { get; }
            public int Octtree_SwapBufferCounts { get; }
            public int Mesh_Triangulate { get; }
            public int Mesh_ApplyQEF { get; }
            public int Mesh_Finalize { get; }

            public Kernels(ComputeShader shader)
            {
                Octtree_AllocateEmptyNodeHashMap = shader.FindKernel(Octtree_AllocateEmptyNodeHashMap_Name);
                Octtree_FindRoots = shader.FindKernel(Octtree_FindRoots_Name);
                Octtree_FindSurfaceNodes = shader.FindKernel(Octtree_FindSurfaceNodes_Name);
                Octtree_SwapBufferCounts = shader.FindKernel(Octtree_SwapBufferCounts_Name);
                Mesh_Triangulate = shader.FindKernel(Mesh_Triangulate_Name);
                Mesh_ApplyQEF = shader.FindKernel(Mesh_ApplyQEF_Name);
                Mesh_Finalize = shader.FindKernel(Mesh_Finalize_Name);
            }
        }

        #endregion

    }

    public static class BufferUtils
    {
        public static bool IsNullOrInvalid(this ComputeBuffer buffer) => buffer == null || !buffer.IsValid();
    }
}