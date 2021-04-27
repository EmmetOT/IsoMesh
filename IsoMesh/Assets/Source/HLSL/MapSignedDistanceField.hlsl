#ifndef COMPUTE_MAP_INCLUDED
#define COMPUTE_MAP_INCLUDED

#define PRIMITIVE_TYPE_SPHERE 0
#define PRIMITIVE_TYPE_TORUS 1
#define PRIMITIVE_TYPE_CUBOID 2
#define PRIMITIVE_TYPE_BOX_FRAME 3

#define MAX_ITERATIONS 256
#define SURFACE_DISTANCE 0.001
#define MAX_DISTANCE 350.0
#define STEP_SIZE_SCALAR 1

#include "Common.hlsl"
#include "./SDFFunctions.hlsl"

// This struct represents a single SDF object, to be sent as an instruction to the GPU.
struct SDFGPUData
{
    int Type; // -1 if mesh, else it's an enum value
    float4 Data; // if primitive, this could be anything. if mesh, it's (size, sample start index, uv start index, 0)
    float4x4 Transform; // translation/rotation/scale
    int Operation; // how this sdf is combined with previous 
    int Flip; // whether to multiply by -1, turns inside out
    float3 MinBounds; // only used by sdfmesh, near bottom left
    float3 MaxBounds; // only used by sdfmesh, far top right
    
    int Size()
    {
        return (int) Data.x;
    }
    
    int SampleStartIndex()
    {
        return (int) Data.y;
    }
    
    int UVStartIndex()
    {
        return (int) Data.z;
    }
};

struct Settings
{
    float Smoothing; // the input to the smooth min function
    float NormalSmoothing; // the 'epsilon' value for computing the gradient, affects how smoothed out the normals are
};

StructuredBuffer<Settings> _Settings;

StructuredBuffer<SDFGPUData> _SDFData;
int _SDFDataCount;

StructuredBuffer<float> _SDFMeshSamples;
StructuredBuffer<float> _SDFMeshPackedUVs;

float3 MapNormal(float3 p, float smoothing = -1.0);

float3 CellCoordinateToVertex(int x, int y, int z, SDFGPUData data)
{
    float gridSize = data.Size() - 1.0;
    float3 minBounds = data.MinBounds;
    float3 maxBounds = data.MaxBounds;
    float xPos = lerp(minBounds.x, maxBounds.x, x / gridSize);
    float yPos = lerp(minBounds.y, maxBounds.y, y / gridSize);
    float zPos = lerp(minBounds.z, maxBounds.z, z / gridSize);

    return float3(xPos, yPos, zPos);
}

int CellCoordinateToIndex(int x, int y, int z, SDFGPUData data)
{
    int size = data.Size();
    return data.SampleStartIndex() + (x + y * size + z * size * size);
}

float GetSignedDistance(int x, int y, int z, SDFGPUData data)
{
    int index = CellCoordinateToIndex(x, y, z, data);
    return _SDFMeshSamples[index];
}

float2 GetUV(int x, int y, int z, SDFGPUData data)
{
    int index = CellCoordinateToIndex(x, y, z, data);
    
    return Unpack2In1(_SDFMeshPackedUVs[index]);
}

// clamp the input point to an axis aligned bounding cube of the given bounds
// optionally can provide an offset which pushes the bounds in or out.
// this is used to get the position on the bounding cube nearest the given point as 
// part of the sdf to mesh calculation. the additional push param is used to ensure we have enough
// samples around our point that we can get a gradient
float3 GetClosestPointToVolume(float3 p, SDFGPUData data, float boundsOffset = 0)
{
    float3 minBounds = data.MinBounds;
    float3 maxBounds = data.MaxBounds;
    return float3(
            clamp(p.x, minBounds.x + boundsOffset, maxBounds.x - boundsOffset),
            clamp(p.y, minBounds.y + boundsOffset, maxBounds.y - boundsOffset),
            clamp(p.z, minBounds.z + boundsOffset, maxBounds.z - boundsOffset)
            );
}

// ensure the given point is inside the volume, and then smush into the the [0, 1] range
float3 ClampAndNormalizeToVolume(float3 p, SDFGPUData data, float boundsOffset = 0)
{
    // clamp so we're inside the volume
    p = GetClosestPointToVolume(p, data, boundsOffset);
    
    float3 minBounds = data.MinBounds;
    float3 maxBounds = data.MaxBounds;
    
    return float3(
    invLerp(minBounds.x + boundsOffset, maxBounds.x - boundsOffset, p.x),
    invLerp(minBounds.y + boundsOffset, maxBounds.y - boundsOffset, p.y),
    invLerp(minBounds.z + boundsOffset, maxBounds.z - boundsOffset, p.z)
    );
}

// given a point, return the coords of the cell it's in, and the fractional component for interpolation
void GetNearestCoordinates(float3 p, SDFGPUData data, out float3 coords, out float3 fracs, float boundsOffset = 0)
{
    p = ClampAndNormalizeToVolume(p, data, boundsOffset);
    int cellsPerSide = data.Size() - 1;
    
    // sometimes i'm not good at coming up with names :U
    float3 floored = floor(p * cellsPerSide);
    coords = min(floored, cellsPerSide - 1);
    
    fracs = frac(p * cellsPerSide);
}

float SampleAssetInterpolated(float3 p, SDFGPUData data, float boundsOffset = 0)
{
    float3 coords;
    float3 fracs;
    GetNearestCoordinates(p, data, coords, fracs, boundsOffset);
    
    int x = coords.x;
    int y = coords.y;
    int z = coords.z;

    float sampleA = GetSignedDistance(x, y, z, data);
    float sampleB = GetSignedDistance(x + 1, y, z, data);
    float sampleC = GetSignedDistance(x, y + 1, z, data);
    float sampleD = GetSignedDistance(x + 1, y + 1, z, data);
    float sampleE = GetSignedDistance(x, y, z + 1, data);
    float sampleF = GetSignedDistance(x + 1, y, z + 1, data);
    float sampleG = GetSignedDistance(x, y + 1, z + 1, data);
    float sampleH = GetSignedDistance(x + 1, y + 1, z + 1, data);

    return TrilinearInterpolate(fracs, sampleA, sampleB, sampleC, sampleD, sampleE, sampleF, sampleG, sampleH);
}

// this is trilinear interpolation with a twist: if the difference between two adjacent values exceed some threshold,
// just use (the average? the min?)
float2 UVTrilinearInterpolate(float3 fracs, float2 a, float2 b, float2 c, float2 d, float2 e, float2 f, float2 g, float2 h)
{
    /*     g-------h
    *     /|      /|
    *    / |     / |
    *   c--|----d  |
    *   |  e----|--f
    *   | /     | /
    *   a-------b
    */

    const float threshold = 0.03;
    
    // x axis
    float2 aToB = (abs(b - a) > threshold) ? a : lerp(a, b, fracs.x);
    float2 cToD = (abs(d - c) > threshold) ? c : lerp(c, d, fracs.x);
    float2 eToF = (abs(f - e) > threshold) ? e : lerp(e, f, fracs.x);
    float2 gToH = (abs(h - g) > threshold) ? g : lerp(g, h, fracs.x);

    // y axis
    float2 y1 = (abs(cToD - aToB) > threshold) ? aToB : lerp(aToB, cToD, fracs.y);
    float2 y2 = (abs(gToH - eToF) > threshold) ? eToF : lerp(eToF, gToH, fracs.y);

    // finally, z axis
    return (abs(y2 - y1) > threshold) ? y1 : lerp(y1, y2, fracs.z);
}

float2 SampleUVInterpolated(float3 p, SDFGPUData data, float boundsOffset = 0)
{
    float3 coords;
    float3 fracs;
    GetNearestCoordinates(p, data, coords, fracs, boundsOffset);
    
    int x = coords.x;
    int y = coords.y;
    int z = coords.z;

    float2 uvA = GetUV(x, y, z, data);
    float2 uvB = GetUV(x + 1, y, z, data);
    float2 uvC = GetUV(x, y + 1, z, data);
    float2 uvD = GetUV(x + 1, y + 1, z, data);
    float2 uvE = GetUV(x, y, z + 1, data);
    float2 uvF = GetUV(x + 1, y, z + 1, data);
    float2 uvG = GetUV(x, y + 1, z + 1, data);
    float2 uvH = GetUV(x + 1, y + 1, z + 1, data);
    
    return UVTrilinearInterpolate(fracs, uvA, uvB, uvC, uvD, uvE, uvF, uvG, uvH);
}

float3 ComputeGradient(float3 p, SDFGPUData data, float epsilon, float boundsOffset = 0)
{
    // sample the map 4 times to calculate the gradient at that point, then normalize it
    const float2 e = float2(epsilon, -epsilon);
    
    return normalize(
        e.xyy * SampleAssetInterpolated(p + e.xyy, data, boundsOffset) +
        e.yyx * SampleAssetInterpolated(p + e.yyx, data, boundsOffset) +
        e.yxy * SampleAssetInterpolated(p + e.yxy, data, boundsOffset) +
        e.xxx * SampleAssetInterpolated(p + e.xxx, data, boundsOffset));
}

// returns the vector pointing to the surface of the mesh representation, as well as the sign
// (negative for inside, positive for outside)
// this can be used to recreate a signed distance field
float3 unsignedDirection_mesh(float3 p, SDFGPUData data, out float distSign, out float3 transformedP)
{
    transformedP = mul(data.Transform, float4(p, 1.0)).xyz;
    
    const float epsilon = 0.75;
    
    // note: this should be larger than epsilon
    const float pushIntoBounds = 0.04;
    
    // get the distance either at p, or at the point on the bounds nearest p
    float sample = SampleAssetInterpolated(transformedP, data);
    
    float3 closestPoint = GetClosestPointToVolume(transformedP, data, pushIntoBounds);
    
    float3 vecInBounds = (-normalize(ComputeGradient(closestPoint, data, epsilon, pushIntoBounds)) * sample);
    float3 vecToBounds = (closestPoint - transformedP);
    float3 finalVec = vecToBounds + vecInBounds;
    
    distSign = sign(sample);
    
    return finalVec;
}

float sdf(float3 p, SDFGPUData data)
{
    if (data.Type < 0)
    {
        float distSign;
        float3 transformedP;
        float3 vec = unsignedDirection_mesh(p, data, distSign, transformedP);

        return length(vec) * distSign * data.Flip;;
    }
    else
    {
        p = mul(data.Transform, float4(p, 1.0)).xyz;
    
        switch (data.Type)
        {
            case PRIMITIVE_TYPE_SPHERE:
                return sdf_sphere(p, data.Data.x) * data.Flip;
            case PRIMITIVE_TYPE_TORUS:
                return sdf_torus(p, data.Data.xy) * data.Flip;
            case PRIMITIVE_TYPE_CUBOID:
                return sdf_roundedBox(p, data.Data.xyz, data.Data.w) * data.Flip;
            default:
                return sdf_boxFrame(p, data.Data.xyz, data.Data.w) * data.Flip;
        }
    }
}

float2 sdf_uv(float3 p, SDFGPUData data, out float dist)
{
    if (data.Type < 0)
    {
        float distSign;
        float3 transformedP;
        float3 vec = unsignedDirection_mesh(p, data, distSign, transformedP);
    
        // push p to the nearest surface of the 'mesh'
        p = transformedP + vec;
    
        dist = length(vec) * distSign * data.Flip;;
    
        return SampleUVInterpolated(p, data);
    }
    else
    {
        p = mul(data.Transform, float4(p, 1.0)).xyz;
    
        switch (data.Type)
        {
            case PRIMITIVE_TYPE_SPHERE:
                dist = sdf_sphere(p, data.Data.x) * data.Flip;
                return sdf_uv_sphere(p, data.Data.x);
            case PRIMITIVE_TYPE_TORUS:
                dist = sdf_torus(p, data.Data.xy) * data.Flip;
                return sdf_uv_sphere(p, data.Data.x);
            case PRIMITIVE_TYPE_CUBOID:
                dist = sdf_roundedBox(p, data.Data.xyz, data.Data.w) * data.Flip;
                return sdf_uv_triplanar(p, data.Data.xyz, sdf_box_normal(p, data.Data.xyz));
            default:
                dist = sdf_boxFrame(p, data.Data.xyz, data.Data.w) * data.Flip;
                return sdf_uv_triplanar(p, data.Data.xyz, sdf_box_normal(p, data.Data.xyz));
        }
    }
}

float Map(float3 p)
{
    float minDist = 10000.0;
    
    float smoothing = _Settings[0].Smoothing;
    
    for (int i = 0; i < _SDFDataCount; i++)
    {
        if (_SDFData[i].Operation == 0)
            minDist = sdf_op_smin(minDist, sdf(p, _SDFData[i]), smoothing);
        else
            minDist = sdf_op_smoothSubtraction(sdf(p, _SDFData[i]), minDist, smoothing);
    }
    
    return minDist;
}

float2 MapUV(float3 p)
{
    const float smallNumber = 0.000000001;
    const float bigNumber = 100000000;
    
    float inverseDistanceSum = 0;
    
    for (int i = 0; i < _SDFDataCount; i++)
    {
        inverseDistanceSum += (1.0 / clamp(sdf(p, _SDFData[i]), smallNumber, bigNumber));
    }
    
    float2 finalUV = float2(0, 0);
    
    for (int j = 0; j < _SDFDataCount; j++)
    {
        float dist;
        float2 uv = sdf_uv(p, _SDFData[j], dist);
        
        float inverseDist = 1.0 / clamp(dist, smallNumber, bigNumber);
        float weight = inverseDist / inverseDistanceSum;

        finalUV += uv * weight;
    }
    
    return finalUV;
}

// this function returns the vector pointing away from the nearest point
float3 MapNormal(float3 p, float smoothing)
{
    float normalSmoothing = smoothing < 0.0 ? _Settings[0].NormalSmoothing : smoothing;
    
    //return sdf_mesh_normal(p, 0);
    //const float epsilon = _NormalSmoothing;
    // sample the map 4 times to calculate the gradient at that point, then normalize it
    float2 e = float2(normalSmoothing, -normalSmoothing);
    
    return normalize(
        e.xyy * Map(p + e.xyy) +
        e.yyx * Map(p + e.yyx) +
        e.yxy * Map(p + e.yxy) +
        e.xxx * Map(p + e.xxx));
}

// https://www.shadertoy.com/view/MsdGz2
float MapThickness(float3 p, float3 n, float maxDist, float falloff)
{
    const int nbIte = 8;
    const float nbIteInv = 1. / float(nbIte);
    float ao = 0.0;
    
    for (int i = 0; i < nbIte; i++)
    {
        float l = hash(float(i)) * maxDist;
        float3 rd = normalize(-n) * l;
        ao += (l + Map(p + rd)) / pow(2.0, falloff);
    }
    
    return clamp(1. - ao * nbIteInv, 0., 1.);
}

#endif // COMPUTE_MAP_INCLUDED