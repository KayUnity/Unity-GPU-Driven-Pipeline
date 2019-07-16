using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System.Threading;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe struct TerrainQuadTreeSettings
    {
        public int maximumLodLevel;
        public double largestChunkSize;
        public double2 screenOffset;
    }
    public unsafe struct TerrainQuadTree
    {
        public enum LocalPos
        {
            LeftDown, LeftUp, RightDown, RightUp
        };
        public TerrainQuadTree* leftDown { get; private set; }
        public TerrainQuadTree* leftUp { get; private set; }
        public TerrainQuadTree* rightDown { get; private set; }
        public TerrainQuadTree* rightUp { get; private set; }
        public TerrainQuadTree* parent { get; private set; }
        private TerrainQuadTreeSettings* setting;
        public int lodLevel;
        public int2 localPosition;
        public int2 rootPosition;
        public TerrainQuadTree(TerrainQuadTree* parent, int parentLodLevel, ref TerrainQuadTreeSettings setting, LocalPos sonPos, int2 parentPos, int2 rootPosition)
        {
            this.setting = setting.Ptr();
            this.rootPosition = rootPosition;
            lodLevel = parentLodLevel + 1;
            this.parent = parent;
            leftDown = null;
            leftUp = null;
            rightDown = null;
            rightUp = null;
            localPosition = parentPos * 2;
            switch(sonPos)
            {
                case LocalPos.LeftUp:
                    localPosition += int2(0, 1);
                    break;
                case LocalPos.RightDown:
                    localPosition += int2(1, 0);
                    break;
                case LocalPos.RightUp:
                    localPosition += 1;
                    break;
            }
        }
        public float2 WorldPosition
        {
            get
            {
                double2 chunkPos = rootPosition + setting->screenOffset;
                chunkPos *= setting->largestChunkSize;
                return (float2)chunkPos;
            }
        }
        public void Dispose()
        {
            parent = null;
            lodLevel = -1;
            if (leftDown != null)
            {
                leftDown->Dispose();
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
            }

            if (leftUp != null)
            {
                leftUp->Dispose();
                UnsafeUtility.Free(leftUp, Allocator.Persistent);
                leftUp = null;
            }

            if (rightDown != null)
            {
                rightDown->Dispose();
                UnsafeUtility.Free(rightDown, Allocator.Persistent);
                rightDown = null;
            }

            if (rightUp != null)
            {
                rightUp->Dispose();
                UnsafeUtility.Free(rightUp, Allocator.Persistent);
                rightUp = null;
            }
        }
    }
}
