using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using MStudio;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
    public unsafe class MeshCombiner : MonoBehaviour
    {
#if UNITY_EDITOR
        public ClusterMatResources res;
        public void GetPoints(NativeList<Point> points, Mesh targetMesh, Transform transform)
        {
            Vector3[] vertices = targetMesh.vertices;
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = transform.localToWorldMatrix.MultiplyPoint(vertices[i]);
                ///TODO
                ///Add others
            }

            int[] triangleArray = targetMesh.triangles;
            foreach (var i in triangleArray)
            {
                points.Add(new Point
                {
                    position = vertices[i]
                });
            }


        }
        public CombinedModel ProcessCluster(MeshRenderer[] allRenderers, Dictionary<MeshRenderer, bool> lowLODLevels)
        {
            List<MeshFilter> allFilters = new List<MeshFilter>(allRenderers.Length);
            int sumVertexLength = 0;
            int sumTriangleLength = 0;

            for (int i = 0; i < allRenderers.Length; ++i)
            {
                if (!lowLODLevels.ContainsKey(allRenderers[i]))
                {
                    MeshFilter filter = allRenderers[i].GetComponent<MeshFilter>();
                    allFilters.Add(filter);
                    sumVertexLength += (int)(filter.sharedMesh.vertexCount * 1.2f);
                }
            }
            sumTriangleLength = (int)(sumVertexLength * 1.5);
            NativeList<Point> points = new NativeList<Point>(sumVertexLength, Allocator.Temp);

            for (int i = 0; i < allFilters.Count; ++i)
            {
                Mesh mesh = allFilters[i].sharedMesh;
                GetPoints(points, mesh, allFilters[i].transform);
            }
            float3 less = points[0].position;
            float3 more = points[0].position;

            for (int i = 1; i < points.Length; ++i)
            {
                float3 current = points[i].position;
                if (less.x > current.x) less.x = current.x;
                if (more.x < current.x) more.x = current.x;
                if (less.y > current.y) less.y = current.y;
                if (more.y < current.y) more.y = current.y;
                if (less.z > current.z) less.z = current.z;
                if (more.z < current.z) more.z = current.z;
            }

            float3 center = (less + more) / 2;
            float3 extent = more - center;
            Bounds b = new Bounds(center, extent * 2);
            CombinedModel md;
            md.bound = b;
            md.allPoints = points;

            return md;
        }

        public struct CombinedModel
        {
            public NativeList<Point> allPoints;
            public Bounds bound;
        }
        public string modelName = "TestFile";
        [Range(100, 500)]
        public int voxelCount = 100;
        [EasyButtons.Button]
        public void TryThis()
        {
            bool save = false;

            if (res == null)
            {
                save = true;
                res = ScriptableObject.CreateInstance<ClusterMatResources>();
                res.name = "SceneManager";
                res.clusterProperties = new List<ClusterProperty>();
            }
            Func<ClusterProperty, ClusterProperty, bool> equalCompare = (a, b) =>
            {
                return a.name == b.name;
            };
            ClusterProperty property = new ClusterProperty();
            property.name = modelName;
            foreach (var i in res.clusterProperties)
            {
                if (equalCompare(property, i))
                {
                    Debug.LogError("Already Contained Scene " + modelName);
                    return;
                }
            }
            LODGroup[] groups = GetComponentsInChildren<LODGroup>();
            Dictionary<MeshRenderer, bool> lowLevelDict = new Dictionary<MeshRenderer, bool>();
            foreach (var i in groups)
            {
                LOD[] lods = i.GetLODs();
                for (int j = 1; j < lods.Length; ++j)
                {
                    foreach (var k in lods[j].renderers)
                    {
                        if (k.GetType() == typeof(MeshRenderer))
                            lowLevelDict.Add(k as MeshRenderer, true);
                    }
                }
            }
            CombinedModel model = ProcessCluster(GetComponentsInChildren<MeshRenderer>(false), lowLevelDict);
            property.clusterCount = ClusterGenerator.GenerateCluster(model.allPoints, model.bound, modelName, voxelCount, res.clusterProperties.Count);
            res.clusterProperties.Add(property);
            if (save)
                AssetDatabase.CreateAsset(res, "Assets/SceneManager.asset");
            else
                EditorUtility.SetDirty(res);
        }
#endif
    }
}
