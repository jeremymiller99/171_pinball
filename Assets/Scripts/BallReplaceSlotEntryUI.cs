using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI entry for a single "replace this ball" slot in the shop.
/// Intended to be used as a prefab that ShopUIController instantiates.
/// </summary>
public sealed class BallReplaceSlotEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image iconImage;

    private int _slotIndex = -1;
    private ShopUIController _shop;

    public void Init(ShopUIController shop, int slotIndex, string label, Sprite icon)
    {
        _shop = shop;
        _slotIndex = slotIndex;

        if (labelText != null)
        {
            labelText.text = label ?? string.Empty;
        }

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
        if (_shop == null || _slotIndex < 0)
        {
            return;
        }
        _shop.ChooseReplaceSlot(_slotIndex);
    }
}

