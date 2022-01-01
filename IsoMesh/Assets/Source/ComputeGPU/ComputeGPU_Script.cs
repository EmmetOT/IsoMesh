using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEditor;

[StructLayout(LayoutKind.Sequential)]
public struct TestBounds
{
    public static readonly int Stride = sizeof(float) * 6 + sizeof(uint);

    public Vector3 Centre;
    public Vector3 Size;
    public uint Key;

    public override string ToString() => $"Centre = {Centre}, Size = {Size}, Key = {Key}";
}

[ExecuteInEditMode]
public class ComputeGPU_Script : MonoBehaviour
{
    private const int CAPACITY = 32768 * 4;
    private const int INSERTION_COUNT = 100;
    private const int TEST_BOUNDS_COUNT = 2;

    [SerializeField]
    private ComputeShader m_computeShader;

    [SerializeField]
    private Transform m_testBoundsPositionOne = null;

    [SerializeField]
    private Vector3 m_testBoundsSizeOne = Vector3.one;

    [SerializeField]
    private Transform m_testBoundsPositionTwo = null;

    [SerializeField]
    private Vector3 m_testBoundsSizeTwo = Vector3.one;

    [SerializeField]
    [Min(0.25f)]
    private float m_rootSize = 1f;

    public TestBounds[] TestBoundsData
    {
        get
        {
            return new TestBounds[]
            {
                new TestBounds()
                {
                    Centre = m_testBoundsPositionOne.position,
                    Size = m_testBoundsSizeOne
                },
                new TestBounds()
                {
                    Centre = m_testBoundsPositionTwo.position,
                    Size = m_testBoundsSizeTwo
                },
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KeyValue
    {
        public static readonly int Stride = sizeof(uint) * 2;

        public uint Key;
        public uint Value;
    }

    uint Hash(uint k)
    {
        k ^= k >> 16;
        k *= 0x85ebca6b;
        k ^= k >> 13;
        k *= 0xc2b2ae35;
        k ^= k >> 16;
        return k & (CAPACITY - 1);
    }

    uint HashVector3(Vector3 v)
    {
        return Hash((uint)((int)(v.x * 1000) ^ ((int)(v.y * 1000) << 2) ^ ((int)(v.z * 1000) >> 2)));
    }

    [ContextMenu("Test")]
    public void Test()
    {
        // 100 random positions to be inserted into the hashmap
        Vector3[] insertionData = new Vector3[INSERTION_COUNT];
        for (int i = 0; i < INSERTION_COUNT; i++)
            insertionData[i] = new Vector3(Random.Range(-1000f, 1000f), Random.Range(-1000f, 1000f), Random.Range(-1000f, 1000f));

        // 100 positions, half of which are in the hashmap, half of which couldn't possibly be
        Vector3[] lookupsData = new Vector3[INSERTION_COUNT];
        for (int i = 0; i < INSERTION_COUNT; i++)
            lookupsData[i] = i % 2 == 0 ? insertionData[i] : (Vector3.one * 2000f);

        // the results of looking up which of the above array's positions are in the hashmap,
        // where 0 means it wasn't found and 1 means it was
        int[] resultsData = new int[INSERTION_COUNT];
        for (int i = 0; i < INSERTION_COUNT; i++)
            resultsData[i] = 0;

        ComputeShader instance = Instantiate(m_computeShader);

        int kernel_Wipe = instance.FindKernel("Wipe");
        int kernel_Insert = instance.FindKernel("Insert");
        int kernel_TestLookupOne = instance.FindKernel("TestLookupOne");
        int kernel_Delete = instance.FindKernel("Delete");
        int kernel_TestLookupTwo = instance.FindKernel("TestLookupTwo");

        ComputeBuffer hashMapBuffer = new ComputeBuffer(CAPACITY, KeyValue.Stride, ComputeBufferType.Structured);
        ComputeBuffer insertionTestBuffer = new ComputeBuffer(INSERTION_COUNT, sizeof(float) * 3, ComputeBufferType.Structured);
        ComputeBuffer lookupAttemptsBuffer = new ComputeBuffer(INSERTION_COUNT, sizeof(float) * 3, ComputeBufferType.Structured);
        ComputeBuffer lookupResultBuffer = new ComputeBuffer(INSERTION_COUNT, sizeof(int), ComputeBufferType.Structured);

        instance.SetInt("_Capacity", CAPACITY);

        instance.SetBuffer(kernel_Wipe, "_HashMap", hashMapBuffer);
        instance.SetBuffer(kernel_Insert, "_HashMap", hashMapBuffer);
        instance.SetBuffer(kernel_TestLookupOne, "_HashMap", hashMapBuffer);
        //instance.SetBuffer(kernel_Delete, "_HashMap", hashMapBuffer);
        //instance.SetBuffer(kernel_TestLookupTwo, "_HashMap", hashMapBuffer);

        insertionTestBuffer.SetData(insertionData);
        instance.SetBuffer(kernel_Insert, "_InsertionTest", insertionTestBuffer);

        lookupAttemptsBuffer.SetData(lookupsData);
        lookupResultBuffer.SetData(resultsData);
        instance.SetBuffer(kernel_TestLookupOne, "_LookupResult", lookupResultBuffer);
        instance.SetBuffer(kernel_TestLookupOne, "_LookupAttempts", lookupAttemptsBuffer);

        Debug.Log("Wiping buffer...");

        // completely wipe the buffer
        instance.GetKernelThreadGroupSizes(kernel_Wipe, out uint x, out _, out _);
        instance.Dispatch(kernel_Wipe, Mathf.CeilToInt(CAPACITY / (float)x), 1, 1);

        Debug.Log("Inserting keys...\n" + insertionData.ToFormattedString());

        // insert 100 random keys
        instance.Dispatch(kernel_Insert, INSERTION_COUNT, 1, 1);

        Debug.Log("Looking up keys...\n" + lookupsData.ToFormattedString());

        // look up the keys and write to the results buffer
        instance.Dispatch(kernel_TestLookupOne, INSERTION_COUNT, 1, 1);

        lookupResultBuffer.GetData(resultsData);

        // should expect to see [1, 0, 1, 0...]
        Debug.Log("The results are in!\n" + resultsData.ToFormattedString());

        hashMapBuffer.Dispose();
        insertionTestBuffer.Dispose();
        lookupAttemptsBuffer.Dispose();
        lookupResultBuffer.Dispose();
    }

    public bool updating = false;

    private void Update()
    {
        if (!updating)
            return;

        if (!m_testBoundsPositionOne || !m_testBoundsPositionTwo)
            return;

        TestFindRoots();
    }

    private TestBounds[] m_lastOutput = null;

    [ContextMenu("Test Find Roots")]
    public void TestFindRoots()
    {
        if (!m_testBoundsPositionOne || !m_testBoundsPositionTwo)
        {
            Debug.LogError("Missing transforms", this);
            return;
        }

        ComputeShader instance = Instantiate(m_computeShader);

        int kernel_Wipe = instance.FindKernel("Wipe");
        int kernel_Root = instance.FindKernel("TestRootNodes");

        ComputeBuffer hashMapBuffer = new ComputeBuffer(CAPACITY, KeyValue.Stride, ComputeBufferType.Structured);
        ComputeBuffer boundsInputBuffer = new ComputeBuffer(TEST_BOUNDS_COUNT, TestBounds.Stride, ComputeBufferType.Structured);
        ComputeBuffer boundsOutputBuffer = new ComputeBuffer(1000, TestBounds.Stride, ComputeBufferType.Append);
        ComputeBuffer countBuffer = new ComputeBuffer(2, sizeof(int), ComputeBufferType.Structured);

        instance.SetFloat("_RootSize", m_rootSize);
        instance.SetInt("_Capacity", CAPACITY);
        boundsOutputBuffer.SetCounterValue(0);

        instance.SetBuffer(kernel_Wipe, "_HashMap", hashMapBuffer);
        instance.SetBuffer(kernel_Root, "_HashMap", hashMapBuffer);
        instance.SetBuffer(kernel_Root, "_TestBoundsInput", boundsInputBuffer);
        instance.SetBuffer(kernel_Root, "_TestBoundsOutput", boundsOutputBuffer);
        instance.SetBuffer(kernel_Root, "_TestBoundsCount", countBuffer);

        Debug.Log("Wiping buffer...");

        // completely wipe the buffer
        instance.GetKernelThreadGroupSizes(kernel_Wipe, out uint x, out _, out _);
        instance.Dispatch(kernel_Wipe, Mathf.CeilToInt(CAPACITY / (float)x), 1, 1);

        TestBounds[] inputData = TestBoundsData;

        Debug.Log("Input data is " + inputData.ToFormattedString());

        int[] countData = new int[] { 0, 0 };

        boundsInputBuffer.SetData(inputData);
        countBuffer.SetData(countData);

        Debug.Log("Finding overlapping grid cells...");

        // data is ready, attempt to find the overlapping grid cells
        instance.GetKernelThreadGroupSizes(kernel_Root, out x, out _, out _);
        instance.Dispatch(kernel_Root, Mathf.CeilToInt(TEST_BOUNDS_COUNT / (float)x), 1, 1);

        countBuffer.GetData(countData);

        Debug.Log("Count is " + countData.ToFormattedString() + " should be " + countData[0] + " output nodes.");

        m_lastOutput = new TestBounds[countData[0]];
        boundsOutputBuffer.GetData(m_lastOutput);

        Debug.Log("Output data is " + m_lastOutput.ToFormattedString());

        Debug.Log("-------------------------");

        hashMapBuffer.Dispose();
        boundsInputBuffer.Dispose();
        boundsOutputBuffer.Dispose();
        countBuffer.Dispose();
    }

    //private Dictionary<uint, (Vector3, int)> m_seenVecs = new Dictionary<uint, (Vector3, int)>();

    private void OnDrawGizmos()
    {
        if (m_testBoundsPositionOne && m_testBoundsPositionTwo)
        {
            Handles.color = Color.red;
            Handles.DrawWireCube(m_testBoundsPositionOne.position, m_testBoundsSizeOne);

            Handles.color = Color.blue;
            Handles.DrawWireCube(m_testBoundsPositionTwo.position, m_testBoundsSizeTwo);
        }

        if (!m_lastOutput.IsNullOrEmpty())
        {
            Handles.color = Color.grey;
            for (int i = 0; i < m_lastOutput.Length; i++)
                Handles.DrawWireCube(m_lastOutput[i].Centre, Vector3.one * m_rootSize);

            //m_seenVecs.Clear();

            //for (int i = 0; i < m_lastOutput.Length; i++)
            //{
            //    uint hash = HashVector3(m_lastOutput[i].Centre);

            //    if (m_seenVecs.TryGetValue(hash, out (Vector3 centre, int count) val))
            //    {
            //        m_seenVecs[hash] = (m_lastOutput[i].Centre, val.count + 1);
            //    }
            //    else
            //    {
            //        m_seenVecs[hash] = (m_lastOutput[i].Centre, 1);
            //    }
            //}

            //foreach (KeyValuePair<uint, (Vector3 centre, int count)> kvp in m_seenVecs)
            //{
            //    Handles.color = kvp.Value.count > 1 ? Color.red : Color.grey;
            //    Handles.DrawWireCube(kvp.Value.centre, Vector3.one * m_rootSize);
            //}
        }
    }
}
