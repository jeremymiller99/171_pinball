/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    internal class SmartMouseMeshRaycaster
    {
        struct Node
        {
            public Vector3 Min;
            public Vector3 Max;
            public int Left;
            public int Right;
            public int Start;
            public int Count;
        }

        const int LeafSize = 4;

        readonly Vector3[] _verts;
        readonly int[] _tris;
        readonly int[] _order;
        readonly Node[] _nodes;

        static readonly int[] _stack = new int[256];

        static readonly Dictionary<Mesh, SmartMouseMeshRaycaster> _cache = new Dictionary<Mesh, SmartMouseMeshRaycaster>();

        const int MaxCache = 256;
        static readonly LinkedList<Mesh> _lru = new LinkedList<Mesh>();
        static readonly Dictionary<Mesh, LinkedListNode<Mesh>> _lruNodes = new Dictionary<Mesh, LinkedListNode<Mesh>>();


        internal Vector3[] Verts => _verts;
        internal int[] Tris => _tris;

        static void Touch(Mesh mesh)
        {
            if (_lruNodes.TryGetValue(mesh, out LinkedListNode<Mesh> node))
            {
                _lru.Remove(node);
                _lru.AddLast(node);
            }
            else
            {
                _lruNodes[mesh] = _lru.AddLast(mesh);
            }
        }

        public static SmartMouseMeshRaycaster Get(Mesh mesh, Vector3[] vertices, int[] triangles)
        {
            // Capacity eviction re-reads a persistent mesh into new arrays with identical content
            if (_cache.TryGetValue(mesh, out SmartMouseMeshRaycaster existing) &&
                (ReferenceEquals(existing._verts, vertices) && ReferenceEquals(existing._tris, triangles)
                 || existing._verts.Length == vertices.Length && existing._tris.Length == triangles.Length))
            {
                Touch(mesh);
                return existing;
            }

            if (_cache.Count >= MaxCache && _lru.First != null)
            {
                Mesh oldest = _lru.First.Value;
                _lru.RemoveFirst();
                _lruNodes.Remove(oldest);
                _cache.Remove(oldest);
            }

            SmartMouseMeshRaycaster built = new SmartMouseMeshRaycaster(vertices, triangles);
            _cache[mesh] = built;
            Touch(mesh);
            return built;
        }

        public static void Clear()
        {
            _cache.Clear();
            _lru.Clear();
            _lruNodes.Clear();
        }

        public static void Evict(Mesh mesh)
        {
            _cache.Remove(mesh);
            if (_lruNodes.TryGetValue(mesh, out LinkedListNode<Mesh> node))
            {
                _lru.Remove(node);
                _lruNodes.Remove(mesh);
            }
        }

        Vector3[] _sortCentroids;
        int _sortAxis;
        IComparer<int> _comparer;
        readonly List<Node> _build = new List<Node>();

        SmartMouseMeshRaycaster(Vector3[] vertices, int[] triangles)
        {
            _verts = vertices;
            _tris = triangles;
            int triCount = triangles.Length / 3;
            _order = new int[triCount];

            Vector3[] centroids = new Vector3[triCount];
            for (int i = 0; i < triCount; i++)
            {
                _order[i] = i;
                int b = i * 3;
                centroids[i] = (vertices[triangles[b]] + vertices[triangles[b + 1]] + vertices[triangles[b + 2]]) * (1f / 3f);
            }

            _sortCentroids = centroids;
            _comparer = Comparer<int>.Create((a, b) => _sortCentroids[a][_sortAxis].CompareTo(_sortCentroids[b][_sortAxis]));

            if (triCount > 0) BuildNode(0, triCount);
            _nodes = _build.ToArray();

            _build.Clear();
            _sortCentroids = null;
            _comparer = null;
        }

        int BuildNode(int start, int end)
        {
            int index = _build.Count;
            _build.Add(default);

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = start; i < end; i++)
            {
                int b = _order[i] * 3;
                Encapsulate(ref min, ref max, _verts[_tris[b]]);
                Encapsulate(ref min, ref max, _verts[_tris[b + 1]]);
                Encapsulate(ref min, ref max, _verts[_tris[b + 2]]);
            }

            Node node = new Node { Min = min, Max = max, Left = -1, Right = -1, Start = start, Count = end - start };

            if (end - start > LeafSize)
            {
                Vector3 size = max - min;
                int axis = SmartMouseGeometry.LargestAxis(size);
                _sortAxis = axis;
                System.Array.Sort(_order, start, end - start, _comparer);

                int mid = (start + end) / 2;
                node.Left = BuildNode(start, mid);
                node.Right = BuildNode(mid, end);
            }

            _build[index] = node;
            return index;
        }

        static void Encapsulate(ref Vector3 min, ref Vector3 max, Vector3 v)
        {
            if (v.x < min.x) min.x = v.x;
            if (v.y < min.y) min.y = v.y;
            if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x;
            if (v.y > max.y) max.y = v.y;
            if (v.z > max.z) max.z = v.z;
        }
        
        public bool Raycast(Ray localRay, float maxT, out Vector3 hitPoint, out int triIndex)
        {
            hitPoint = Vector3.zero;
            triIndex = -1;
            if (_nodes.Length == 0) return false;

            float bestT = maxT;
            bool found = false;

            int sp = 0;
            _stack[sp++] = 0;
            while (sp > 0)
            {
                Node node = _nodes[_stack[--sp]];

                UnityEngine.Bounds bounds = new UnityEngine.Bounds((node.Min + node.Max) * 0.5f, node.Max - node.Min);
                if (!bounds.IntersectRay(localRay, out float entry) || entry > bestT) continue;

                if (node.Left < 0)
                {
                    for (int k = 0; k < node.Count; k++)
                    {
                        int tri = _order[node.Start + k];
                        int tb = tri * 3;
                        if (SmartMouseGeometry.RayIntersectsTriangle(localRay,
                                _verts[_tris[tb]], _verts[_tris[tb + 1]], _verts[_tris[tb + 2]],
                                out Vector3 p, out float t) && t < bestT)
                        {
                            bestT = t;
                            hitPoint = p;
                            triIndex = tri;
                            found = true;
                        }
                    }
                }
                else if (sp + 2 <= _stack.Length)
                {
                    _stack[sp++] = node.Left;
                    _stack[sp++] = node.Right;
                }
            }

            return found;
        }
    }
}
