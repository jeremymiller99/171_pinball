using UnityEngine;

public class FloatingTextSpawner : MonoBehaviour
{
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 spawnOffset = new Vector2(20f, 0f);

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    /// <summary>
    /// Spawns floating text at the given world position, mapped to the canvas.
    /// </summary>
    /// <param name="worldPosition">World position to spawn the text (e.g., ball position)</param>
    /// <param name="text">The text to display</param>
    public void SpawnText(Vector3 worldPosition, string text)
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
    }
}
