using UnityEngine;
using TMPro;

public class ShopButton3D : MonoBehaviour
{
    [SerializeField] private MeshRenderer buttonRenderer;
    [SerializeField] private Material activatedMaterial;
    [SerializeField] private Material deactivatedMaterial;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Color activatedTextColor = Color.green;
    [SerializeField] private Color deactivatedTextColor = Color.gray;

    private void Start()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules != null)
        {
            rules.ShopAvailabilityChanged -= OnShopAvailabilityChanged;
            rules.ShopAvailabilityChanged += OnShopAvailabilityChanged;
            OnShopAvailabilityChanged(rules.ShopAvailable);
        }
        else
        {
            SetActivated(false);
        }
    }

    private void OnDestroy()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules != null) rules.ShopAvailabilityChanged -= OnShopAvailabilityChanged;
    }

    private void OnShopAvailabilityChanged(bool isAvailable)
    {
        SetActivated(isAvailable);
    }

    private void SetActivated(bool activated)
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.sharedMaterial = activated ? activatedMaterial : deactivatedMaterial;
        }

        if (buttonText != null)
        {
            buttonText.color = activated ? activatedTextColor : deactivatedTextColor;
        }
    }

    private void Update()
    {
        if (ControlsBindingsService.WasPressedThisFrame(ControlAction.UISelect))
        {
            OnClick();
        }
    }

    public void OnClick()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules == null || !rules.ShopAvailable) return;

        rules.TryEnterShopFromButton();
    }
}
