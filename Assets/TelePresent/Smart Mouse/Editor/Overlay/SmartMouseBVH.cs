/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;
using System.Collections.Generic;
namespace TelePresent.SmartMouse
{
    internal class SmartMouseBVH
    {
        SmartMouseBVHNode _root;

        // Synchronous build, for the immediate (EnsureBVH / RebuildBVH) path.
        public SmartMouseBVH(SmartMouseBVHNode[] nodes)
        {
            SmartMouseBVHBuilder builder = new SmartMouseBVHBuilder(nodes);
            builder.Step(int.MaxValue);
            _root = builder.RootNode;
        }

        // Wrap a root the background builder produced across frames.
        internal SmartMouseBVH(SmartMouseBVHNode root)
        {
            _root = root;
        }

        // Fills the caller's list with the leaf nodes whose bounds the ray intersects; allocates nothing.
        public void Traverse(Ray ray, List<SmartMouseBVHNode> results)
        {
            TraverseRecursive(_root, ray, results);
        }

        public void Clear()
        {
            _root?.Clear();
            _root = null;
        }

        void TraverseRecursive(SmartMouseBVHNode node, Ray ray, List<SmartMouseBVHNode> results)
        {
            if (node == null) return;

            if (node.Bounds.IntersectRay(ray, out float _))
            {
                if (node.IsLeaf)
                {
                    results.Add(node);
                }
                else
                {
                    TraverseRecursive(node.LeftChild, ray, results);
                    TraverseRecursive(node.RightChild, ray, results);
                }
            }
        }
    }
}
