#ifndef NODE_CUSTOM_VERTEX_ID
#define NODE_CUSTOM_VERTEX_ID

StructuredBuffer<float3> _MeshVertices;
StructuredBuffer<float3> _MeshNormals;
StructuredBuffer<int> _MeshTriangles;

void CustomVertexID_float(in uint VertexID, out float3 Position, out float3 Normal)
{
    int id = _MeshTriangles[VertexID];
    
    Position = _MeshVertices[id];
    Normal = _MeshNormals[id];
}
#endif