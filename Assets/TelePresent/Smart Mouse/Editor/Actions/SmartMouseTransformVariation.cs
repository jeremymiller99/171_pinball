/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace TelePresent.SmartMouse
{
    // Snapshots each object's pre-variation transform
    internal sealed class SmartMouseTransformVariation
    {
        readonly Dictionary<GameObject, Quaternion> _rotations = new Dictionary<GameObject, Quaternion>();
        readonly Dictionary<GameObject, Vector3> _scales = new Dictionary<GameObject, Vector3>();
        readonly Dictionary<GameObject, Quaternion> _rotationOffsets = new Dictionary<GameObject, Quaternion>();
        readonly Dictionary<GameObject, float> _scaleFactors = new Dictionary<GameObject, float>();

        public void Capture(GameObject obj)
        {
            if (obj == null) return;
            if (!_rotations.ContainsKey(obj)) _rotations[obj] = obj.transform.localRotation;
            if (!_scales.ContainsKey(obj)) _scales[obj] = obj.transform.localScale;
        }

        public void Clear()
        {
            _rotations.Clear();
            _scales.Clear();
            _rotationOffsets.Clear();
            _scaleFactors.Clear();
        }

        public void ApplyRotation(GameObject obj, Vector3 range)
        {
            if (obj == null || !_rotations.TryGetValue(obj, out Quaternion baseRotation)) return;

            Vector3 offset = new Vector3(
                Random.Range(-range.x, range.x),
                Random.Range(-range.y, range.y),
                Random.Range(-range.z, range.z));
            Quaternion rotationOffset = Quaternion.Euler(offset);
            _rotationOffsets[obj] = rotationOffset;
            obj.transform.localRotation = baseRotation * rotationOffset;
            EditorUtility.SetDirty(obj);
        }

        public void ApplyScale(GameObject obj, float range)
        {
            if (obj == null || !_scales.TryGetValue(obj, out Vector3 baseScale)) return;

            range = Mathf.Max(0f, range);
            float factor = Random.Range(Mathf.Max(0.01f, 1f - range), 1f + range);
            _scaleFactors[obj] = factor;
            obj.transform.localScale = baseScale * factor;
            EditorUtility.SetDirty(obj);
        }

        // Placement may tilt/settle an already-varied transform. Fold that placement into the stored base
        // while retaining the current random offset, so later slider edits do not erase surface alignment.
        public void RebaseFromCurrent(GameObject obj)
        {
            if (obj == null) return;
            if (_rotationOffsets.TryGetValue(obj, out Quaternion rotationOffset))
                _rotations[obj] = obj.transform.localRotation * Quaternion.Inverse(rotationOffset);
            if (_scaleFactors.TryGetValue(obj, out float factor) && Mathf.Abs(factor) > 1e-6f)
                _scales[obj] = obj.transform.localScale / factor;
        }
    }
}
