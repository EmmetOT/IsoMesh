using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// This struct represents a single SDF object, to be sent as an instruction to the GPU.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[System.Serializable]
public struct SDFGPUData
{
    public static int Stride => sizeof(int) * 3 + sizeof(float) * 10 + sizeof(float) * 16;

    public int Type; // negative if operation, 0 if mesh, else it's an enum value
    public Vector4 Data; // if primitive, this could be anything. if mesh, it's (size, sample start index, uv start index, 0)
    public Matrix4x4 Transform; // translation/rotation/scale
    public int CombineType; // how this sdf is combined with previous 
    public int Flip; // whether to multiply by -1, turns inside out
    public Vector3 MinBounds; // only used by sdfmesh, near bottom left
    public Vector3 MaxBounds;// only used by sdfmesh, far top right
    
    public override string ToString()
    {
        if (Type == 0)
        {
            return $"[Mesh] Size = {(int)Data.x}, MinBounds = {MinBounds}, MaxBounds = {MaxBounds}, StartIndex = {(int)Data.y}, UVStartIndex = {(int)Data.z}";
        }
        else if (Type < 0)
        {
            return $"[{(SDFOperationType)Type}] Data = {Data}";
        }
        else
        {
            return $"[{(SDFPrimitiveType)Type}] Data = {Data}";
        }
    }
}
