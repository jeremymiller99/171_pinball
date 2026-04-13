using UnityEngine;

[RequireComponent(typeof(BoardLightWaveSequence))]
public sealed class ShopButtonLights : MonoBehaviour
{
    private BoardLightWaveSequence _wave;

    private void Awake()
    {
        _wave = GetComponent<BoardLightWaveSequence>();
    }

    private void OnEnable()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules != null)
        {
            rules.ShopAvailabilityChanged -= OnShopAvailabilityChanged;
            rules.ShopAvailabilityChanged += OnShopAvailabilityChanged;
            Apply(rules.ShopAvailable);
        }
        else
        {
            Apply(false);
        }
    }

    private void OnDisable()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules != null) rules.ShopAvailabilityChanged -= OnShopAvailabilityChanged;
    }

    private void OnShopAvailabilityChanged(bool isAvailable) => Apply(isAvailable);

    private void Apply(bool on)
    {
        if (_wave == null) return;
        if (on) _wave.Play();
        else _wave.Stop();
    }
}
