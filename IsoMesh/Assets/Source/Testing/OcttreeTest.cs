using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IsoMesh;
using System.Linq;
using System.Runtime.InteropServices;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class OcttreeTest : MonoBehaviour
{
    private const float SQRT_3_OVER_2 = 0.866025403f;
    private const long MAX_BUFFER_SIZE = 2147483648;


    [SerializeField]
    private bool m_autoUpdate = false;
    public bool AutoUpdate => m_autoUpdate;

    [SerializeField]
    private SDFGroupMeshGenerator m_meshGen;

    private void OnEnable()
    {
        if (!m_meshGen)
            return;

        //m_meshGen.NodesSetEvent -= OnNodesSet;
        //m_meshGen.NodesSetEvent += OnNodesSet;
    }

    private void OnDisable()
    {
        if (!m_meshGen)
            return;

        //m_meshGen.NodesSetEvent -= OnNodesSet;
    }

    private void OnValidate()
    {
        if (!m_meshGen)
            return;

        //m_meshGen.NodesSetEvent -= OnNodesSet;

        //if (m_autoUpdate)
        //    m_meshGen.NodesSetEvent += OnNodesSet;
    }

    private void OnNodesSet(SDFGroupMeshGenerator.OcttreeNode[] nodes)
    {
        if (!m_autoUpdate)
            return;

        m_leafNodes = nodes.ToList();
    }

    //#region Fields and Properties

    //private static class Properties
    //{
    //    public static readonly int IndirectArgs = Shader.PropertyToID("_IndirectArgs");
    //    public static readonly int NodeBuffer_Append = Shader.PropertyToID("_NodeBuffer_Append");
    //    public static readonly int NodeBuffer_Consume = Shader.PropertyToID("_NodeBuffer_Consume");
    //    public static readonly int NodeCount_Structured = Shader.PropertyToID("_NodeCount_Structured");
    //}

    //private struct Kernels
    //{
    //    public const string MainKernelName = "MainKernel";

    //    public int Main { get; }

    //    public Kernels(ComputeShader shader)
    //    {
    //        Main = shader.FindKernel(MainKernelName);
    //    }
    //}

    //private static Kernels m_kernels;

    //#endregion

    //[SerializeField]
    //private ComputeShader m_computeShader;
    //private ComputeShader m_computeShaderInstance;

    [SerializeField]
    private SDFGroup m_group;

    //[SerializeField]
    //private int m_maxNodes = 1000000;

    private List<SDFGroupMeshGenerator.OcttreeNode> m_leafNodes = new List<SDFGroupMeshGenerator.OcttreeNode>();

    private readonly Stack<SDFGroupMeshGenerator.OcttreeNode> m_bufferOne = new Stack<SDFGroupMeshGenerator.OcttreeNode>();
    private readonly Stack<SDFGroupMeshGenerator.OcttreeNode> m_bufferTwo = new Stack<SDFGroupMeshGenerator.OcttreeNode>();

    private Stack<SDFGroupMeshGenerator.OcttreeNode> m_onBuffer;
    private Stack<SDFGroupMeshGenerator.OcttreeNode> m_offBuffer;

    //[ContextMenu("Test GPU")]
    //private void TestGPU() => FindOccupiedNodesCompute(transform.position, m_startingWidth, m_maxLevel);

    //[ContextMenu("Test CPU")]
    //private void TestCPU()
    //{
    //    if (!m_meshGen)
    //        return;

    //    FindOccupiedNodes(transform.position, m_meshGen.VoxelSettings.OcttreeRootWidth, m_meshGen.VoxelSettings.OcttreeMaxNodeDepth);
    //    Debug.Log("Found " + m_leafNodes.Count + " nodes.");
    //}

    //public IEnumerable<(int, int)> IterateFindOccupiedNodes(Vector3 centre, float width, int levels)
    //{
    //    m_bufferOne.Clear();
    //    m_bufferTwo.Clear();

    //    m_onBuffer = m_bufferOne;
    //    m_offBuffer = m_bufferTwo;

    //    SDFGroupMeshGenerator.OcttreeNode root = new SDFGroupMeshGenerator.OcttreeNode()
    //    {
    //        Position = centre,
    //        Width = width,
    //        ID = 1
    //    };

    //    m_onBuffer.Push(root);
    //    int nodeCount = 1;
    //    int args = 1;

    //    void Evaluate(SDFGroupMeshGenerator.OcttreeNode node)
    //    {
    //        float signedDistance = m_group.Mapper.Map(node.Position);

    //        if (Mathf.Abs(signedDistance) <= ((node.Width * SQRT_3_OVER_2) + m_meshGen.VoxelSettings.OcttreeNodePadding))
    //        {
    //            uint i = 0;
    //            foreach (Vector3 childCentre in node.ChildCentres)
    //            {
    //                SDFGroupMeshGenerator.OcttreeNode child = new()
    //                {
    //                    Position = childCentre,
    //                    Width = node.Width * 0.5f,
    //                    ID = (root.ID << 3) | i++ // shift id 3 bits left and add the id for the new node
    //                };

    //                m_offBuffer.Push(child);
    //            }

    //            nodeCount += 8;
    //        }
    //    }

    //    for (int i = 0; i < levels; i++)
    //    {
    //        while (m_onBuffer.Count > 0)
    //        {
    //            --nodeCount;
    //            Evaluate(m_onBuffer.Pop());
    //        }

    //        args = Mathf.Max(args, 1, Mathf.CeilToInt(nodeCount / 64f));

    //        (m_onBuffer, m_offBuffer) = (m_offBuffer, m_onBuffer);

    //        yield return (args, nodeCount);
    //    }

    //    m_leafNodes = m_onBuffer.ToList();
    //}

    //private void FindOccupiedNodes(Vector3 centre, float width, int levels)
    //{
    //    int iteration = 0;
    //    foreach ((int, int) indirectArgs in IterateFindOccupiedNodes(centre, width, levels))
    //    {
    //        Debug.Log($"Iteration {iteration} is {indirectArgs}.");
    //    }


    //    //m_bufferOne.Clear();
    //    //m_bufferTwo.Clear();

    //    //m_onBuffer = m_bufferOne;
    //    //m_offBuffer = m_bufferTwo;

    //    //SDFGroupMeshGenerator.OcttreeNode root = new SDFGroupMeshGenerator.OcttreeNode()
    //    //{
    //    //    Position = centre,
    //    //    Width = width
    //    //};

    //    //m_onBuffer.Push(root);

    //    //void Evaluate(SDFGroupMeshGenerator.OcttreeNode node)
    //    //{
    //    //    float signedDistance = m_group.Mapper.Map(node.Position);

    //    //    if (Mathf.Abs(signedDistance) <= node.Width * SQRT_3_OVER_2)
    //    //    {
    //    //        foreach (Vector3 childCentre in node.ChildCentres)
    //    //        {
    //    //            SDFGroupMeshGenerator.OcttreeNode child = new SDFGroupMeshGenerator.OcttreeNode()
    //    //            {
    //    //                Position = childCentre,
    //    //                Width = node.Width * 0.5f
    //    //            };

    //    //            m_offBuffer.Push(child);
    //    //        }
    //    //    }
    //    //}

    //    //for (int i = 0; i < levels; i++)
    //    //{
    //    //    while (m_onBuffer.Count > 0)
    //    //    {
    //    //        Evaluate(m_onBuffer.Pop());
    //    //    }

    //    //    (m_onBuffer, m_offBuffer) = (m_offBuffer, m_onBuffer);
    //    //}

    //    //m_leafNodes = m_onBuffer.ToList();
    //}

    //private int GetMaxPossibleNodes(int levels)
    //{
    //    long power = 1;

    //    for (int i = 0; i < levels; i++)
    //        power *= 8;

    //    if (power > m_maxNodes)
    //        return m_maxNodes;

    //    if (power > int.MaxValue)
    //        return int.MaxValue;

    //    return (int)power;
    //}

    //private void FindOccupiedNodesCompute(Vector3 centre, float width, int levels)
    //{
    //    m_computeShaderInstance = Instantiate(m_computeShader);
    //    m_kernels = new Kernels(m_computeShaderInstance);

    //    uint[] indirectArgsData = new uint[] { 1, 1, 1 };
    //    ComputeBuffer indirectArgs = new ComputeBuffer(indirectArgsData.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
    //    indirectArgs.SetData(indirectArgsData);
    //    m_computeShaderInstance.SetBuffer(m_kernels.Main, Properties.IndirectArgs, indirectArgs);

    //    uint[] nodeCountData = new uint[] { 1 };

    //    ComputeBuffer nodeCount = new ComputeBuffer(1, sizeof(uint));
    //    nodeCount.SetData(nodeCountData);
    //    m_computeShaderInstance.SetBuffer(m_kernels.Main, Properties.NodeCount_Structured, nodeCount);

    //    int maxNodes = GetMaxPossibleNodes(levels);

    //    ComputeBuffer bufferOne = new ComputeBuffer(maxNodes, SDFGroupMeshGenerator.OcttreeNode.Stride, ComputeBufferType.Append);
    //    ComputeBuffer bufferTwo = new ComputeBuffer(maxNodes, SDFGroupMeshGenerator.OcttreeNode.Stride, ComputeBufferType.Append);

    //    ComputeBuffer bufferAppend = bufferOne;
    //    ComputeBuffer bufferConsume = bufferTwo;

    //    SDFGroupMeshGenerator.OcttreeNode root = new SDFGroupMeshGenerator.OcttreeNode()
    //    {
    //        Position = centre,
    //    };

    //    bufferConsume.SetData(new SDFGroupMeshGenerator.OcttreeNode[] { root });
    //    bufferConsume.SetCounterValue(1);

    //    for (int i = 0; i < levels; i++)
    //    {
    //        bufferAppend.SetCounterValue(0);

    //        m_computeShaderInstance.SetBuffer(m_kernels.Main, Properties.NodeBuffer_Append, bufferAppend);
    //        m_computeShaderInstance.SetBuffer(m_kernels.Main, Properties.NodeBuffer_Consume, bufferConsume);
    //        m_computeShaderInstance.DispatchIndirect(m_kernels.Main, indirectArgs);

    //        (bufferAppend, bufferConsume) = (bufferConsume, bufferAppend);
    //    }

    //    nodeCount.GetData(nodeCountData);
    //    SDFGroupMeshGenerator.OcttreeNode[] result = new SDFGroupMeshGenerator.OcttreeNode[nodeCountData[0]];
    //    bufferConsume.GetData(result);

    //    nodeCount.Release();
    //    indirectArgs.Release();
    //    bufferOne.Release();
    //    bufferTwo.Release();
    //    DestroyImmediate(m_computeShaderInstance);

    //    m_leafNodes = new List<SDFGroupMeshGenerator.OcttreeNode>(result);
    //}

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (m_leafNodes.IsNullOrEmpty() || !m_meshGen)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        for (int i = 0; i < m_leafNodes.Count; i++)
        {
            SDFGroupMeshGenerator.OcttreeNode node = m_leafNodes[i];

            Handles.color = Color.white.SetAlpha(0.5f);
            Handles.DrawWireCube(node.Position, Vector3.one * node.Width);
        }

        Handles.color = Color.black;
        Handles.DrawWireCube(transform.position, Vector3.one * m_meshGen.VoxelSettings.OcttreeRootWidth * 1.01f);
    }

#endif
}
