#ifndef SURFACE_NET_STRUCTS
#define SURFACE_NET_STRUCTS

struct CellData
{
    int VertexID;
    float3 SurfacePoint;
    
    bool HasSurfacePoint()
    {
        return VertexID >= 0;
    }
};

struct VertexData
{
    int Index;
    int CellID;
    float3 Vertex;
    float3 Normal;
};

struct TriangleData
{
    int P_1;
    int P_2;
    int P_3;
    
    //uint P_1_IsNewVertex;
    //uint P_2_IsNewVertex;
    //uint P_3_IsNewVertex;
};

struct NewVertexData
{
    int Index;
    float3 Vertex;
    float3 Normal;
};

#endif // SURFACE_NET_STRUCTS