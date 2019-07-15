#ifndef __VOXELLIGHT_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 128
#define VOXELZ 64
#define MAXLIGHTPERCLUSTER 128
#define FROXELRATE 1.35
#define CLUSTERRATE 1.5
static const uint3 _ScreenSize = uint3(160, 90, 128);
#include "CGINC/Plane.cginc"

#define VOXELSIZE uint3(XRES, YRES, ZRES)


            struct PointLight{
                float3 lightColor;
                float4 sphere;
                int shadowIndex;
            };
            struct SpotLight
            {
                float3 lightColor;
                Cone lightCone;
                float angle;
                float4x4 vpMatrix;
                float smallAngle;
                float nearClip;
                int shadowIndex;
                int iesIndex;
            };

            struct FogVolume
            {
                float3x3 localToWorld;
                float3x4 worldToLocal;
                float3 position;
                float3 extent;
                float targetVolume;
                float3 color;
                float3 emissionColor;
            };
            struct AreaLight
            {
                float4x4 mat;
                float3 color;
            };
            struct Decal
            {
                float3x3 localToWorldMat;
                float3x4 worldToLocal;
                float3 position;
                float4 albedoScaleOffset;
                float4 normalScaleOffset;
                int2 texIndex;
            };
float3 _CameraForward;
float3 _CameraNearPos;
float3 _CameraFarPos;
float4 _VolumetricLightVar; //x: Camera nearclip plane      y: Volume distance - nearclip       z: volume distance      w: indirect intensity

inline uint GetIndex(uint3 id, const uint3 size, const int multiply){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
    return dot(id, multiValue);
}

#endif