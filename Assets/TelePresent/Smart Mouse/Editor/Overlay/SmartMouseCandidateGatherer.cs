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
using UnityEditor;
using UnityEditor.SceneManagement;

// 2021 help.
#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

namespace TelePresent.SmartMouse
{

    internal sealed class SmartMouseCandidateGatherer
    {
        enum Phase { Lod, MeshFilters, Skinned, Complete }

        const int LodGroupWork = 8;

        Phase _phase = Phase.Lod;
        readonly HashSet<GameObject> _candidates = new HashSet<GameObject>();

        HashSet<GameObject> _excludedLod;
        LODGroup[] _groups;
        int _groupCursor;

        Component[] _current;
        int _cursor;

        readonly bool _scopeToStage;
        readonly UnityEngine.SceneManagement.Scene _stageScene;

        public SmartMouseCandidateGatherer()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) { _scopeToStage = true; _stageScene = stage.scene; }
        }

        bool InActiveStage(GameObject go) => !_scopeToStage || go.scene == _stageScene;

        public HashSet<GameObject> Result => _candidates;

        public bool Step(int budget)
        {
            int work = 0;
            while (_phase != Phase.Complete && work < budget)
            {
                switch (_phase)
                {
                    case Phase.Lod:
                        work += StepLod(budget - work);
                        break;
                    case Phase.MeshFilters:
                        if (_current == null) { _current = SmartMouseCompat.FindAll<MeshFilter>(); _cursor = 0; }
                        work += StepFilter(budget - work, next: Phase.Skinned);
                        break;
                    case Phase.Skinned:
                        if (_current == null) { _current = SmartMouseCompat.FindAll<SkinnedMeshRenderer>(); _cursor = 0; }
                        work += StepFilter(budget - work, next: Phase.Complete);
                        break;
                }
            }
            return _phase == Phase.Complete;
        }

        int StepLod(int budget)
        {
            if (_groups == null)
            {
                _groups = SmartMouseCompat.FindAll<LODGroup>();
                _excludedLod = _groups.Length > 0 ? new HashSet<GameObject>() : null;
                _groupCursor = 0;
            }

            int work = 0;
            while (_groupCursor < _groups.Length && work < budget)
            {
                LODGroup group = _groups[_groupCursor];
                if (group != null && group.enabled && group.gameObject.activeInHierarchy)
                    ExcludeNonKeptLevels(group, _excludedLod);
                _groupCursor++;
                work += LodGroupWork;
            }

            if (_groupCursor >= _groups.Length)
                _phase = Phase.MeshFilters;
            return work;
        }

        int StepFilter(int budget, Phase next)
        {
            int work = 0;
            for (; _cursor < _current.Length && work < budget; _cursor++, work++)
            {
                Component c = _current[_cursor];
                if (c == null) continue;
                GameObject go = c.gameObject;
                bool include = IsMeshCandidate(c);
                if (include && InActiveStage(go)) _candidates.Add(go);
            }

            if (_cursor >= _current.Length)
            {
                _current = null;
                _phase = next;
            }
            return work;
        }

        bool IsMeshCandidate(Component component)
        {
            GameObject go = component.gameObject;
            if (_excludedLod != null && _excludedLod.Contains(go)) return false;
            if (!go.activeInHierarchy || !SmartMouseSettings.IncludesLayer(go.layer)) return false;

            if (component is MeshFilter meshFilter)
                return meshFilter.sharedMesh != null &&
                       (!EditorApplication.isPlaying || meshFilter.sharedMesh.isReadable) &&
                       go.TryGetComponent(out MeshRenderer meshRenderer) &&
                       meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy &&
                       !meshRenderer.forceRenderingOff;

            if (component is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh != null && skinned.enabled && skinned.gameObject.activeInHierarchy &&
                       !skinned.forceRenderingOff;

            return false;
        }

        internal static void ExcludeNonKeptLevels(LODGroup group, HashSet<GameObject> excluded)
        {
            if (group == null || excluded == null || !group.enabled || !group.gameObject.activeInHierarchy)
                return;
            LOD[] lods = group.GetLODs();
            int[] counts = new int[lods.Length];
            for (int i = 0; i < lods.Length; i++)
                counts[i] = RendererCount(lods[i].renderers);

            int keep = KeptLodLevel(counts);
            if (keep < 0) return; 

            for (int i = 0; i < lods.Length; i++)
            {
                if (i == keep) continue;
                foreach (Renderer r in lods[i].renderers)
                    if (r != null) excluded.Add(r.gameObject);
            }
            foreach (Renderer r in lods[keep].renderers)
                if (r != null) excluded.Remove(r.gameObject);
        }

        static int RendererCount(Renderer[] renderers)
        {
            if (renderers == null) return 0;
            int n = 0;
            foreach (Renderer r in renderers)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff) n++;
            return n;
        }

        internal static int KeptLodLevel(int[] rendererCountPerLevel)
        {
            int nonEmpty = 0;
            foreach (int c in rendererCountPerLevel)
                if (c > 0) nonEmpty++;
            if (nonEmpty <= 1) return -1;

            int target = (nonEmpty - 1) / 2;
            int seen = 0;
            for (int i = 0; i < rendererCountPerLevel.Length; i++)
            {
                if (rendererCountPerLevel[i] == 0) continue;
                if (seen == target) return i;
                seen++;
            }
            return -1;
        }
    }
}
