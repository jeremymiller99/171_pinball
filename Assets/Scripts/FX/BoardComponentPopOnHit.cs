// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-16.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardComponentPopOnHit : MonoBehaviour
{
    private const float DefaultPopScaleMultiplier = 1.12f;
    private const float DefaultPopUpDurationSeconds = 0.06f;
    private const float DefaultPopDownDurationSeconds = 0.08f;
    private const float DefaultMinTriggerIntervalSeconds = 0.04f;

    [Header("Hit Detection")]
    [SerializeField] private string ballTag = "Ball";

    [Header("Visuals")]
    [Tooltip("Root transform to scale. Should contain only mesh renderers (no colliders).")]
    [SerializeField] private Transform visualRoot;

    [Header("Pop")]
    [SerializeField] private float popScaleMultiplier = DefaultPopScaleMultiplier;
    [SerializeField] private float popUpDurationSeconds = DefaultPopUpDurationSeconds;
    [SerializeField] private float popDownDurationSeconds = DefaultPopDownDurationSeconds;
    [SerializeField] private float minTriggerIntervalSeconds = DefaultMinTriggerIntervalSeconds;

    private Transform[] popTargets;
    private Vector3[] baseScales;
    private Coroutine popCoroutine;
    private float lastTriggerTimeSeconds = float.NegativeInfinity;

    private void Awake()
    {
        CachePopTargets();
    }

    private void OnEnable()
    {
        if (popTargets == null || baseScales == null || popTargets.Length != baseScales.Length)
        {
            CachePopTargets();
        }
    }

    private void OnDisable()
    {
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        ApplyScaleMultiplier(1f);
    }

    private void CachePopTargets()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
        List<Transform> targets = new List<Transform>(renderers.Length);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            targets.Add(renderers[i].transform);
        }

        popTargets = targets.ToArray();
        baseScales = new Vector3[popTargets.Length];

        for (int i = 0; i < popTargets.Length; i++)
        {
            baseScales[i] = popTargets[i] != null ? popTargets[i].localScale : Vector3.one;
        }
    }

    private bool IsBall(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ballTag))
        {
            return false;
        }

        return collider.CompareTag(ballTag);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        if (!IsBall(collision.collider))
        {
            return;
        }

        TriggerPop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsBall(other))
        {
            return;
        }

        TriggerPop();
    }

    private void TriggerPop()
    {
        // Another script (ex: FrenzyExplodable) may disable this object during the same hit callback.
        // Unity won't start coroutines on inactive/disabled behaviours.
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Time.time - lastTriggerTimeSeconds < Mathf.Max(0f, minTriggerIntervalSeconds))
        {
            return;
        }

        lastTriggerTimeSeconds = Time.time;

        if (popTargets == null || baseScales == null || popTargets.Length != baseScales.Length)
        {
            CachePopTargets();
        }

        if (popTargets.Length == 0)
        {
            return;
        }

        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
        }

        popCoroutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        float safePopScaleMultiplier = Mathf.Max(1f, popScaleMultiplier);

        float safePopUpDurationSeconds = Mathf.Max(0f, popUpDurationSeconds);
        float safePopDownDurationSeconds = Mathf.Max(0f, popDownDurationSeconds);

        if (safePopUpDurationSeconds <= 0f && safePopDownDurationSeconds <= 0f)
        {
            ApplyScaleMultiplier(1f);
            popCoroutine = null;
            yield break;
        }

        if (safePopUpDurationSeconds > 0f)
        {
            float elapsedSeconds = 0f;
            while (elapsedSeconds < safePopUpDurationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / safePopUpDurationSeconds);
                float eased = t * t;
                ApplyScaleMultiplier(Mathf.Lerp(1f, safePopScaleMultiplier, eased));
                yield return null;
            }
        }

        if (safePopDownDurationSeconds > 0f)
        {
            float elapsedSeconds = 0f;
            while (elapsedSeconds < safePopDownDurationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / safePopDownDurationSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                ApplyScaleMultiplier(Mathf.Lerp(safePopScaleMultiplier, 1f, eased));
                yield return null;
            }
        }

        ApplyScaleMultiplier(1f);
        popCoroutine = null;
    }

    private void ApplyScaleMultiplier(float multiplier)
    {
        if (popTargets == null || baseScales == null)
        {
            return;
        }

        int count = Mathf.Min(popTargets.Length, baseScales.Length);
        for (int i = 0; i < count; i++)
        {
            Transform target = popTargets[i];
            if (target == null)
            {
                continue;
            }

            target.localScale = baseScales[i] * multiplier;
        }
    }
}

