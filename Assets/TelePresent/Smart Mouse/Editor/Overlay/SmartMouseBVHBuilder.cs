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

    internal sealed class SmartMouseBVHBuilder
    {
        struct Task
        {
            public int Start;
            public int End;
            public SmartMouseBVHNode Parent;
            public bool Left;
        }

        readonly SmartMouseBVHNode[] _nodes;
        readonly Stack<Task> _stack = new Stack<Task>();
        SmartMouseBVHNode _root;
        bool _done;

        public SmartMouseBVHBuilder(SmartMouseBVHNode[] nodes)
        {
            _nodes = nodes;
            if (nodes.Length == 0) { _done = true; return; }
            _stack.Push(new Task { Start = 0, End = nodes.Length, Parent = null, Left = false });
        }


        public SmartMouseBVHNode RootNode => _root;

        public bool Step(int budget)
        {
            int work = 0;
            while (_stack.Count > 0)
            {
                Task task = _stack.Pop();
                int count = task.End - task.Start;
                SmartMouseBVHNode node;
                if (count == 1)
                {
                    node = _nodes[task.Start];
                }
                else
                {
                    UnityEngine.Bounds bounds = TotalBounds(_nodes, task.Start, task.End);
                    int axis = SmartMouseGeometry.LargestAxis(bounds.size);
                    int mid = task.Start + count / 2;
                    NthElement(_nodes, task.Start, task.End, mid, axis);
                    node = new SmartMouseBVHNode(null, null, bounds);
                    _stack.Push(new Task { Start = mid, End = task.End, Parent = node, Left = false });
                    _stack.Push(new Task { Start = task.Start, End = mid, Parent = node, Left = true });
                }

                if (task.Parent == null) _root = node;
                else if (task.Left) task.Parent.LeftChild = node;
                else task.Parent.RightChild = node;

                work += count;
                if (work >= budget) break;
            }

            if (_stack.Count == 0) _done = true;
            return _done;
        }

        static UnityEngine.Bounds TotalBounds(SmartMouseBVHNode[] nodes, int start, int end)
        {
            UnityEngine.Bounds bounds = new UnityEngine.Bounds(nodes[start].Bounds.center, nodes[start].Bounds.size);
            for (int i = start + 1; i < end; i++)
                bounds.Encapsulate(nodes[i].Bounds);
            return bounds;
        }
        
        static void NthElement(SmartMouseBVHNode[] nodes, int start, int end, int nth, int axis)
        {
            int lo = start, hi = end - 1;
            while (lo < hi)
            {
                float pivot = Median3(nodes[lo].Center[axis], nodes[(lo + hi) / 2].Center[axis], nodes[hi].Center[axis]);
                int i = lo, j = hi;
                while (i <= j)
                {
                    while (nodes[i].Center[axis] < pivot) i++;
                    while (nodes[j].Center[axis] > pivot) j--;
                    if (i <= j)
                    {
                        SmartMouseBVHNode t = nodes[i]; nodes[i] = nodes[j]; nodes[j] = t;
                        i++; j--;
                    }
                }
                if (nth <= j) hi = j;
                else if (nth >= i) lo = i;
                else break;
            }
        }

        static float Median3(float x, float y, float z) =>
            Mathf.Max(Mathf.Min(x, y), Mathf.Min(Mathf.Max(x, y), z));
    }
}
