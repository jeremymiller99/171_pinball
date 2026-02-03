using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying a single round card.
/// Design the card layout in the prefab - no dynamic resizing.
/// </summary>
public class RoundCardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text roundNumberText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text modifierNameText;
    [SerializeField] private TMP_Text modifierDescriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("Type Colors")]
    [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color angelColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color devilColor = new Color(0.8f, 0.2f, 0.3f, 1f);

    [Header("Background Colors")]
    [SerializeField] private Color normalBgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color angelBgColor = new Color(0.2f, 0.18f, 0.1f, 0.9f);
    [SerializeField] private Color devilBgColor = new Color(0.2f, 0.1f, 0.1f, 0.9f);

    /// <summary>
    /// Initializes the card with round data.
    /// </summary>
    public void Init(RoundData data)
    {
        if (data == null)
            return;

        // Round number
        if (roundNumberText != null)
            roundNumberText.text = (data.roundIndex + 1).ToString();

        // Type
        if (typeText != null)
        {
            typeText.text = data.type.ToString().ToUpper();
            typeText.color = GetTypeColor(data.type);
        }

        // Modifier name
        if (modifierNameText != null)
            modifierNameText.text = data.GetModifierDisplayName();

        // Modifier description
        if (modifierDescriptionText != null)
            modifierDescriptionText.text = data.GetModifierDescription();

        // Icon
        if (iconImage != null)
        {
            if (data.modifier != null && data.modifier.icon != null)
            {
                iconImage.sprite = data.modifier.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // Border color
        if (borderImage != null)
            borderImage.color = GetTypeColor(data.type);

        // Background color
        if (backgroundImage != null)
            backgroundImage.color = GetBackgroundColor(data.type);
    }

    private Color GetTypeColor(RoundType type)
    {
        switch (type)
        {
            case RoundType.Angel: return angelColor;
            case RoundType.Devil: return devilColor;
            default: return normalColor;
        }
    }

    private Color GetBackgroundColor(RoundType type)
    {
        switch (type)
        {
            case RoundType.Angel: return angelBgColor;
            case RoundType.Devil: return devilBgColor;
            default: return normalBgColor;
        }
    }
}
