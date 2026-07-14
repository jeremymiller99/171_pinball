using UnityEngine;

/// <summary>
/// Scale pulse for the shop item whose tooltip is open. Added on demand
/// by RenderTextureRaycaster when an item is clicked and disabled when
/// the highlight clears; restores the exact base scale on stop.
/// </summary>
public sealed class ShopItemPulse : MonoBehaviour
{
    // Unscaled time so the pulse keeps moving if the shop pauses the game.
    private const float pulsesPerSecond = 1.6f;
    private const float amplitude = 0.04f;

    private Vector3 _baseScale;
    private float _elapsed;

    private void OnEnable()
    {
        _baseScale = transform.localScale;
        _elapsed = 0f;
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        float pulse = 1f + amplitude * Mathf.Sin(
            _elapsed * pulsesPerSecond * 2f * Mathf.PI);
        transform.localScale = _baseScale * pulse;
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale;
    }
}
