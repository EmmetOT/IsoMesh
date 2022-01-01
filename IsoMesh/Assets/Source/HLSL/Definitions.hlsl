#ifndef DEFINITIONS
#define DEFINITIONS

#ifndef UNITY_COMMON_INCLUDED
#define PI 3.1415926
#endif

#define DEGREES_TO_RADIANS 0.0174533

#define ONE (float3(1, 1, 1))
#define LEFT (float3(-1, 0, 0))
#define RIGHT (float3(1, 0, 0))
#define UP (float3(0, 1, 0))
#define DOWN (float3(0, -1, 0))
#define FORWARD (float3(0, 0, 1))
#define BACK (float3(0, 0, -1))

#define IDENTITY_MATRIX float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)



#endif // DEFINITIONS