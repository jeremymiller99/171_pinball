// Generated with Claude Code (Opus 4.8) for jjmil on 2026-06-05.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this on the ROOT of a hand-made tutorial panel prefab, then wire up the
/// slots below in the inspector. <see cref="BasicTutorialController"/> instantiates
/// the prefab and calls <see cref="Bind"/> to fill in the localized text and wire
/// the close button. You control all layout, spacing, fonts, and art in the prefab.
///
/// Prefabs are loaded from a Resources folder by name — see the resource path
/// constants in BasicTutorialController (default: Assets/Resources/Tutorial/).
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPanelView : MonoBehaviour
{
    [Header("Text slots — filled at runtime with localized strings")]
    [Tooltip("The TextMeshPro element that shows the panel title.")]
    [SerializeField] private TextMeshProUGUI titleLabel;

    [Tooltip("The TextMeshPro element that shows the panel body text.")]
    [SerializeField] private TextMeshProUGUI bodyLabel;

    [Header("Close button — optional")]
    [Tooltip("The dismiss button. Leave EMPTY for the level-up panel, which "
        + "auto-closes when the player opens the shop.")]
    [SerializeField] private Button closeButton;

    [Tooltip("The TextMeshPro label inside the close button (e.g. START / GOT IT). "
        + "Optional — leave empty to keep whatever text you authored on the button.")]
    [SerializeField] private TextMeshProUGUI closeButtonLabel;

    /// <summary>
    /// Populates the panel's text and wires the close button. Called by the
    /// controller right after the prefab is instantiated.
    /// </summary>
    /// <param name="title">Localized title text.</param>
    /// <param name="body">Localized body text.</param>
    /// <param name="buttonLabel">Localized button label, or null/empty for no button.</param>
    /// <param name="onClose">Invoked when the close button is pressed. If null, the
    /// button is hidden (used by the level-up panel).</param>
    public void Bind(string title, string body, string buttonLabel, Action onClose)
    {
        if (titleLabel != null) titleLabel.text = title ?? string.Empty;
        if (bodyLabel != null) bodyLabel.text = body ?? string.Empty;

        bool wantButton = closeButton != null
            && !string.IsNullOrEmpty(buttonLabel)
            && onClose != null;

        if (closeButton == null) return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.gameObject.SetActive(wantButton);

        if (!wantButton) return;

        if (closeButtonLabel != null) closeButtonLabel.text = buttonLabel;

        closeButton.onClick.AddListener(() => onClose?.Invoke());
        ServiceLocator.Get<AudioManager>()?.WireButtonAudio(closeButton);
    }
}
