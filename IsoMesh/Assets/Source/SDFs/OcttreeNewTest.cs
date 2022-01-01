using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IsoMesh;
using UnityEditor;
using System.Linq;

[ExecuteInEditMode]
public class OcttreeNewTest : MonoBehaviour
{
    private const float SQRT_3_OVER_2 = 0.866025403f;






    [ContextMenu("Test Hashtable")]
    public void TestHashtable()
    {
        GPUHashMapTest hashMap = new GPUHashMapTest();

        void Insert(uint key, uint val)
        {
            Debug.Log($"Inserting [{key}, {val}]");
            hashMap.Insert(key, val);
        }

        void Lookup(uint key)
        {
            uint val = hashMap.Lookup(key);
            Debug.Log($"Looked up {key}, got {(val == GPUHashMapTest.Empty ? "Nothing" : val.ToString())}");
        }

        void Delete(uint key)
        {
            Debug.Log($"Deleting [{key}]");
            hashMap.Delete(key);
        }

        Insert(0, 0);
        Insert(1, 1);
        Lookup(0);
        Lookup(1);
        Insert(0, 3);
        Lookup(0);
        Delete(0);
        Lookup(0);
        Delete(1);
        Lookup(1);

        Insert(103221, 3);
        Insert(342323, 76);

        Lookup(103221);
        Lookup(342323);
    }


    [SerializeField]
    private SDFGroup m_group;

    [SerializeField]
    [Min(0.001f)]
    private float m_cellSize = 0.1f;

    [SerializeField]
    [Min(1)]
    private int m_depth = 4;

    [SerializeField]
    [Min(0f)]
    private float m_octtreeNodeExtraSensitivity = 0.1f;

    private readonly Dictionary<int, Val> m_centres = new Dictionary<int, Val>();

    private readonly List<SDFGroupMeshGenerator.OcttreeNode> m_leafNodes = new List<SDFGroupMeshGenerator.OcttreeNode>();

    private readonly Stack<SDFGroupMeshGenerator.OcttreeNode> m_bufferOne = new Stack<SDFGroupMeshGenerator.OcttreeNode>();
    private readonly Stack<SDFGroupMeshGenerator.OcttreeNode> m_bufferTwo = new Stack<SDFGroupMeshGenerator.OcttreeNode>();

    private Stack<SDFGroupMeshGenerator.OcttreeNode> m_onBuffer;
    private Stack<SDFGroupMeshGenerator.OcttreeNode> m_offBuffer;

    private struct Val
    {
        public int Count;
        public Vector3 Centre;
    }

    private int HashVector3(Vector3 v) => (int) (v.x * 1000) ^ ((int)(v.y * 1000) << 2) ^ ((int)(v.z * 1000) >> 2);

    //private void OnEnable()
    //{
    //    m_group.OnGroupUpdate += RunOcttree;
    //}

    //private void OnDisable()
    //{
    //    m_group.OnGroupUpdate -= RunOcttree;
    //}

    //private void OnValidate()
    //{
    //    if (!m_group.IsReady)
    //        return;

    //    RunOcttree();
    //}

    private void UpdateRootNodes()
    {
        static Vector3Int FloorToInt(Vector3 vec) => new Vector3Int(Mathf.FloorToInt(vec.x), Mathf.FloorToInt(vec.y), Mathf.FloorToInt(vec.z));

        m_centres.Clear();

        float rootNodeSize = m_cellSize * Mathf.Pow(2f, m_depth);

        foreach (SDFObject obj in m_group.SDFObjects)
        {
            Bounds bounds = obj.AABB;

            Vector3Int boundsMin = FloorToInt(bounds.min / rootNodeSize);
            Vector3Int boundsMax = FloorToInt(bounds.max / rootNodeSize);

            for (int x = boundsMin.x; x <= boundsMax.x; x++)
            {
                for (int y = boundsMin.y; y <= boundsMax.y; y++)
                {
                    for (int z = boundsMin.z; z <= boundsMax.z; z++)
                    {
                        Vector3 centre = (new Vector3(x, y, z) + Vector3.one * 0.5f) * rootNodeSize;

                        int key = HashVector3(centre);

                        if (m_centres.TryGetValue(key, out Val val))
                        {
                            val.Count++;
                        }
                        else
                        {
                            val = new Val()
                            {
                                Centre = centre,
                                Count = 1
                            };
                        };

                        m_centres[key] = val;
                    }
                }
            }
        }
    }

    //private void Update() => BoxDrawer.Draw();

    //private void OnDrawGizmos()
    //{
    //    Vector3 rootNode = Vector3.one * m_cellSize * Mathf.Pow(2f, m_depth);

    //    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

    //    foreach (KeyValuePair<int, Val> centres in m_centres)
    //    {
    //        Color col = Utils.RainbowLerp(Mathf.Clamp01(centres.Value.Count / 5f));

    //        Handles.color = col;
    //        Gizmos.color = col;

    //        Handles.DrawWireCube(centres.Value.Centre, rootNode);
    //        Gizmos.DrawSphere(centres.Value.Centre, 0.1f);
    //    }

    //    if (!m_leafNodes.IsNullOrEmpty())
    //    {
    //        Vector3 leafNode = Vector3.one * m_cellSize;

    //        Handles.color = Color.black;
    //        for (int i = 0; i < m_leafNodes.Count; i++)
    //            Handles.DrawWireCube(m_leafNodes[i].Position, leafNode);
    //    }
    //}

    //[ContextMenu("Test")]
    //private void RunOcttree()
    //{
    //    UpdateRootNodes();

    //    m_leafNodes.Clear();
    //    float rootNodeSize = m_cellSize * Mathf.Pow(2f, m_depth);

    //    foreach (KeyValuePair<int, Val> centres in m_centres)
    //        FindOccupiedNodes(centres.Value.Centre, rootNodeSize);

    //    //BoxDrawer.Clear();

    //    //if (!m_leafNodes.IsNullOrEmpty())
    //    //{
    //    //    Vector3 leafNode = Vector3.one * m_cellSize;

    //    //    for (int i = 0; i < m_leafNodes.Count; i++)
    //    //        BoxDrawer.Add(m_leafNodes[i].Position, leafNode);
    //    //}
    //}


    //private void FindOccupiedNodes(Vector3 rootPosition, float width)
    //{
    //    m_bufferOne.Clear();
    //    m_bufferTwo.Clear();

    //    m_onBuffer = m_bufferOne;
    //    m_offBuffer = m_bufferTwo;

    //    SDFGroupMeshGenerator.OcttreeNode root = new SDFGroupMeshGenerator.OcttreeNode()
    //    {
    //        Position = rootPosition,
    //        Width = width,
    //        ID = 1
    //    };

    //    m_onBuffer.Push(root);
    //    int nodeCount = 1;
    //    int args = 1;

    //    void Evaluate(SDFGroupMeshGenerator.OcttreeNode node)
    //    {
    //        float signedDistance = m_group.Mapper.Map(node.Position);

    //        if (Mathf.Abs(signedDistance) <= ((node.Width * SQRT_3_OVER_2) + m_octtreeNodeExtraSensitivity))
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

    //    for (int i = 0; i < m_depth; i++)
    //    {
    //        while (m_onBuffer.Count > 0)
    //        {
    //            --nodeCount;
    //            Evaluate(m_onBuffer.Pop());
    //        }

    //        args = Mathf.Max(args, 1, Mathf.CeilToInt(nodeCount / 64f));

    //        (m_onBuffer, m_offBuffer) = (m_offBuffer, m_onBuffer);
    //    }

    //    m_leafNodes.AddRange(m_onBuffer);
    //}
}
