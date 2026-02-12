using UnityEngine;
using TMPro;

public class FloatingTextSpawner : MonoBehaviour
{
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 spawnOffset = new Vector2(20f, 0f);

    [Header("Points")]
    [Tooltip("TMP Font Asset for points text (blue style).")]
    [SerializeField] private TMP_FontAsset pointsFontAsset;
    [SerializeField] private float pointsScaleMin = 0.6f;
    [SerializeField] private float pointsScaleMax = 1.2f;
    [Tooltip("Points value at which scale reaches max.")]
    [SerializeField] private float pointsMaxValue = 100f;

    [Header("Multiplier")]
    [Tooltip("TMP Font Asset for multiplier text (red style).")]
    [SerializeField] private TMP_FontAsset multFontAsset;
    [SerializeField] private float multScaleMin = 0.6f;
    [SerializeField] private float multScaleMax = 1.3f;
    [Tooltip("Mult value at which scale reaches max.")]
    [SerializeField] private float multMaxValue = 3f;

    [Header("Gold/Coins")]
    [Tooltip("TMP Font Asset for gold/coins text (yellow style).")]
    [SerializeField] private TMP_FontAsset goldFontAsset;
    [SerializeField] private float goldScaleMin = 0.6f;
    [SerializeField] private float goldScaleMax = 1.1f;
    [Tooltip("Gold value at which scale reaches max.")]
    [SerializeField] private float goldMaxValue = 5f;

    [Header("Points + Mult side by side")]
    [Tooltip("When both points and mult text are shown for the same hit, mult text is offset by this (world space) so they don't overlap. Set X positive for mult to the right of points.")]
    [SerializeField] private Vector3 sideBySideOffsetForMultText = new Vector3(0.4f, 0f, 0f);

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    /// <summary>
    /// When showing both points and mult for the same hit, offset the mult text by this so they appear side by side at contact.
    /// </summary>
    public Vector3 GetSideBySideOffsetForMultText() => sideBySideOffsetForMultText;

    /// <summary>
    /// Spawns floating text at the given world position, mapped to the canvas.
    /// </summary>
    /// <param name="worldPosition">World position to spawn the text (e.g., ball position)</param>
    /// <param name="text">The text to display</param>
    public void SpawnText(Vector3 worldPosition, string text)
    {
        SpawnTextInternal(worldPosition, text, null, 0.6f);
    }

    /// <summary>
    /// Spawns floating text for points using the points font asset, size based on value.
    /// </summary>
    public void SpawnPointsText(Vector3 worldPosition, string text, float pointsValue)
    {
        float t = Mathf.Clamp01(pointsValue / pointsMaxValue);
        float scale = Mathf.Lerp(pointsScaleMin, pointsScaleMax, t);
        SpawnTextInternal(worldPosition, text, pointsFontAsset, scale);
    }

    /// <summary>
    /// Spawns floating text for multiplier using the mult font asset, size based on value.
    /// </summary>
    public void SpawnMultText(Vector3 worldPosition, string text, float multValue)
    {
        float t = Mathf.Clamp01(multValue / multMaxValue);
        float scale = Mathf.Lerp(multScaleMin, multScaleMax, t);
        SpawnTextInternal(worldPosition, text, multFontAsset, scale);
    }

    /// <summary>
    /// Spawns floating text for gold/coins using the gold font asset, size based on value.
    /// </summary>
    public void SpawnGoldText(Vector3 worldPosition, string text, float goldValue)
    {
        float t = Mathf.Clamp01(goldValue / goldMaxValue);
        float scale = Mathf.Lerp(goldScaleMin, goldScaleMax, t);
        SpawnTextInternal(worldPosition, text, goldFontAsset, scale);
    }

    private void SpawnTextInternal(Vector3 worldPosition, string text, TMP_FontAsset fontAsset, float scale)
    {
        if (floatingTextPrefab == null || canvas == null) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        if (targetCamera == null) return;

        FloatingText ft = Instantiate(floatingTextPrefab, canvas.transform);
        RectTransform rt = ft.GetComponent<RectTransform>();

        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPosition);

        // Convert screen pixel position to anchored position inside canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCamera,
            out Vector2 anchoredPos);

        rt.anchoredPosition = anchoredPos + spawnOffset;
        ft.SetText(text);
        
        if (fontAsset != null)
        {
            ft.SetFontAsset(fontAsset);
        }
        
        ft.SetScale(scale);
    }
}
