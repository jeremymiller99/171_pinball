using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI entry for a single shop offer (one of the 3 balls offered this shop visit).
/// Intended to be used as a prefab that ShopUIController instantiates.
/// </summary>
public sealed class ShopBallOfferEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image iconImage;

    private int _offerIndex = -1;
    private ShopUIController _shop;

    public void Init(ShopUIController shop, int offerIndex, string title, int cost, Sprite icon)
    {
        _shop = shop;
        _offerIndex = offerIndex;

        if (titleText != null) titleText.text = title ?? string.Empty;
        if (costText != null) costText.text = cost.ToString();

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = icon;
            }
            else
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (_shop == null || _offerIndex < 0) return;
        _shop.BuyOfferByIndex(_offerIndex);
    }
}

