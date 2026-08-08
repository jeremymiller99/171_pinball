/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{

    internal static class SmartMouseSelectionResolver
    {
        public static GameObject ResolveSelectionTarget(GameObject obj)
        {
            if (obj == null) return null;

            if (obj.transform is RectTransform)
                return obj;

            LODGroup lodGroup = obj.GetComponentInParent<LODGroup>();
            GameObject baseTarget = lodGroup != null ? lodGroup.gameObject : obj;

            if (!SmartMouseSettings.SelectPrefabRoot)
                return baseTarget;

            GameObject selectionBase = FindSelectionBaseAncestor(baseTarget);
            if (selectionBase != null)
                return selectionBase;

            GameObject prefabRoot = SmartMouseSettings.PrefabRootOutermost
                ? PrefabUtility.GetOutermostPrefabInstanceRoot(baseTarget)
                : PrefabUtility.GetNearestPrefabInstanceRoot(baseTarget);

            return prefabRoot != null ? prefabRoot : baseTarget;
        }

        // Reused buffer and a per-type memo, since this runs on every hover.
        static readonly List<Component> _components = new List<Component>();
        static readonly Dictionary<Type, bool> _isSelectionBase = new Dictionary<Type, bool>();

        public static GameObject FindSelectionBaseAncestor(GameObject obj)
        {
            for (Transform t = obj.transform; t != null; t = t.parent)
            {
                t.GetComponents(_components);
                foreach (Component component in _components)
                {
                    if (component == null) continue;
                    Type type = component.GetType();
                    if (!_isSelectionBase.TryGetValue(type, out bool isBase))
                    {
                        isBase = Attribute.IsDefined(type, typeof(SelectionBaseAttribute));
                        _isSelectionBase[type] = isBase;
                    }
                    if (isBase) return t.gameObject;
                }
            }
            return null;
        }
    }
}
