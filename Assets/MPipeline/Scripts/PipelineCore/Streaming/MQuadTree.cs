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
        public bool isRendering { get; private set; }
        public TerrainQuadTree(TerrainQuadTree* parent, int parentLodLevel, TerrainQuadTreeSettings* setting, LocalPos sonPos, int2 parentPos, int2 rootPosition)
        {
            this.setting = setting;
            this.rootPosition = rootPosition;
            isRendering = false;
            lodLevel = parentLodLevel + 1;
            this.parent = parent;
            leftDown = null;
            leftUp = null;
            rightDown = null;
            rightUp = null;
            localPosition = parentPos * 2;
            switch (sonPos)
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
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
                leftUp = null;
                rightDown = null;
                rightUp = null;
            }
        }
        private void DisableSelfRendering()
        {
            isRendering = false;
            //TODO
            //Disable Self's rendering
        }
        private void EnableSelfRendering()
        {
            isRendering = true;
        }
        private void Separate()
        {
            if (!isRendering) return;
            isRendering = false;
            DisableSelfRendering();
            if (leftDown == null)
            {
                leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                leftUp = leftDown + 1;
                rightDown = leftDown + 2;
                rightUp = leftDown + 3;
            }
            *leftDown = new TerrainQuadTree(this.Ptr(), lodLevel, setting, LocalPos.LeftDown, localPosition, rootPosition)
            {
                isRendering = true
            };
            *leftUp = new TerrainQuadTree(this.Ptr(), lodLevel, setting, LocalPos.LeftUp, localPosition, rootPosition)
            {
                isRendering = true
            };
            *rightDown = new TerrainQuadTree(this.Ptr(), lodLevel, setting, LocalPos.RightDown, localPosition, rootPosition)
            {
                isRendering = true
            };
            *rightUp = new TerrainQuadTree(this.Ptr(), lodLevel, setting, LocalPos.RightUp, localPosition, rootPosition)
            {
                isRendering = true
            };
            //TODO
            //Enable Children's rendering
        }
        private void Combine()
        {
            if (!(leftDown != null && leftDown->isRendering &&
                  rightDown->isRendering && rightUp->isRendering && leftUp->isRendering))
            {
                //TODO
                //Remove children's rendering
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
                leftUp = null;
                rightDown = null;
                rightUp = null;
                isRendering = true;
                //TODO
                //Enabled self's rendering
            }
        }
    }
}
