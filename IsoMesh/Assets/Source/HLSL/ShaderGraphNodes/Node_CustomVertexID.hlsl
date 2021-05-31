#ifndef NODE_CUSTOM_VERTEX_ID
#define NODE_CUSTOM_VERTEX_ID

#include "../Compute_IsoSurfaceExtraction_Structs.hlsl"

StructuredBuffer<float3> _MeshVertices;
StructuredBuffer<float3> _MeshNormals;
StructuredBuffer<int> _MeshTriangles;
StructuredBuffer<SDFMaterialGPU> _MeshVertexMaterials;

void CustomVertexID_float(in uint VertexID, out float3 Position, out float3 Normal, out float3 Colour, out float3 Emission, out float Metallic, out float Smoothness)
{
    int id = _MeshTriangles[VertexID];
    
    Position = _MeshVertices[id];
    Normal = _MeshNormals[id];
    
    SDFMaterialGPU material = _MeshVertexMaterials[id];
    
    Colour = material.Colour;
    Emission = material.Emission;
    Metallic = material.Metallic;
    Smoothness = material.Smoothness;
}
#endif