#ifndef NODE_CUSTOM_VERTEX_ID
#define NODE_CUSTOM_VERTEX_ID

#include "../Compute_IsoSurfaceExtraction_Structs.hlsl"

StructuredBuffer<float3> _MeshVertices;
StructuredBuffer<float3> _MeshNormals;
StructuredBuffer<int> _MeshTriangles;
StructuredBuffer<SDFMaterialGPU> _MeshVertexMaterials;

StructuredBuffer<SurfacePoint> _SurfacePoints;
StructuredBuffer<SDFMaterialGPU> _SurfacePointMaterials;

void CustomVertexID_float(in uint VertexID, out float3 Position, out float3 Normal, out float3 Colour, out float3 Emission, out float Metallic, out float Smoothness, out float Thickness, out float3 SubsurfaceColour, out float SubsurfaceScatteringPower)
{
    int id = _MeshTriangles[VertexID];
    
    Position = _MeshVertices[id];
    Normal = _MeshNormals[id];
    
    SDFMaterialGPU material = _MeshVertexMaterials[id];
    
    Colour = material.Colour;
    SubsurfaceColour = material.SubsurfaceColour;
    Emission = material.Emission;
    Metallic = material.Metallic;
    Smoothness = material.Smoothness;
    Thickness = material.Thickness;
    SubsurfaceScatteringPower = material.SubsurfaceScatteringPower;
}


void New_CustomVertexID_float(in uint VertexID, out float3 Position, out float3 Normal, out float3 Colour, out float3 Emission, out float Metallic, out float Smoothness, out float Thickness, out float3 SubsurfaceColour, out float SubsurfaceScatteringPower)
{
    SurfacePoint sp = _SurfacePoints[VertexID];
    SDFMaterialGPU material = _SurfacePointMaterials[VertexID];
    
    Position = sp.Position;
    Normal = sp.Normal;
    
    Colour = material.Colour;
    SubsurfaceColour = material.SubsurfaceColour;
    Emission = material.Emission;
    Metallic = material.Metallic;
    Smoothness = material.Smoothness;
    Thickness = material.Thickness;
    SubsurfaceScatteringPower = material.SubsurfaceScatteringPower;
}

#endif