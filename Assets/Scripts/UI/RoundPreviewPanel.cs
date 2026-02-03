using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI panel that displays all rounds as cards.
/// Uses separate prefabs for large/medium/small cards - design them in the editor.
/// Auto-scales cards to fit all on screen.
/// </summary>
public class RoundPreviewPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text continueButtonText;

    [Header("Card Prefabs")]
    [Tooltip("Prefab for the focused/current round (largest).")]
    [SerializeField] private RoundCardUI largeCardPrefab;
    [Tooltip("Prefab for adjacent rounds (medium).")]
    [SerializeField] private RoundCardUI mediumCardPrefab;
    [Tooltip("Prefab for other rounds (smallest).")]
    [SerializeField] private RoundCardUI smallCardPrefab;

    [Header("Layout")]
    [SerializeField] private float cardSpacing = 10f;
    [SerializeField] private float horizontalPadding = 40f;
    [SerializeField] private float minScale = 0.5f;
    [Tooltip("Offset applied to all cards after centering.")]
    [SerializeField] private Vector2 cardsOffset = Vector2.zero;
    [Tooltip("If true, auto-centers cards. If false, uses Manual Start X.")]
    [SerializeField] private bool autoCenter = true;
    [Tooltip("Manual starting X position (only used if Auto Center is false).")]
    [SerializeField] private float manualStartX = 0f;
    [Tooltip("Y position for all cards.")]
    [SerializeField] private float cardsY = 0f;

    private readonly List<RoundCardUI> _spawnedCards = new List<RoundCardUI>();
    private Action _onContinueCallback;
    private bool _isShowing;
    private bool _buttonHooked;

    private void Awake()
    {
        HookButton();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void HookButton()
    {
        if (_buttonHooked || continueButton == null)
            return;

        continueButton.onClick.AddListener(OnContinueClicked);
        _buttonHooked = true;
    }

    /// <summary>
    /// Shows the panel focusing on round 0.
    /// </summary>
    public void Show(IReadOnlyList<RoundData> rounds, Action onContinue)
    {
        Show(rounds, 0, onContinue);
    }

    /// <summary>
    /// Shows the panel focusing on a specific round.
    /// </summary>
    public void Show(IReadOnlyList<RoundData> rounds, int focusRoundIndex, Action onContinue)
    {
        // Ensure button is hooked (in case Awake didn't run yet)
        HookButton();

        if (_isShowing)
            return;

        _isShowing = true;
        _onContinueCallback = onContinue;

        ClearCards();

        if (panelRoot != null)
            panelRoot.SetActive(true);

        UpdateTitle(focusRoundIndex);
        SpawnCards(rounds, focusRoundIndex);
    }

    /// <summary>
    /// Hides the panel.
    /// </summary>
    public void Hide()
    {
        _isShowing = false;
        _onContinueCallback = null;
        ClearCards();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void UpdateTitle(int focusRoundIndex)
    {
        if (titleText != null)
            titleText.text = focusRoundIndex == 0 ? "YOUR ROUNDS" : $"ROUND {focusRoundIndex + 1}";

        if (continueButtonText != null)
            continueButtonText.text = focusRoundIndex == 0 ? "START RUN" : "CONTINUE";
    }

    private void OnContinueClicked()
    {
        var callback = _onContinueCallback;
        Hide();
        callback?.Invoke();
    }

    private void SpawnCards(IReadOnlyList<RoundData> rounds, int focusIndex)
    {
        if (rounds == null || cardsContainer == null)
            return;

        // First pass: spawn all cards and calculate total width
        float totalWidth = 0f;
        for (int i = 0; i < rounds.Count; i++)
        {
            var prefab = GetPrefabForIndex(i, focusIndex);
            if (prefab == null)
                continue;

            var card = Instantiate(prefab, cardsContainer);
            card.Init(rounds[i]);
            _spawnedCards.Add(card);

            var rect = card.GetComponent<RectTransform>();
            if (rect != null)
                totalWidth += rect.rect.width;
        }

        // Add spacing between cards
        if (_spawnedCards.Count > 1)
            totalWidth += cardSpacing * (_spawnedCards.Count - 1);

        // Calculate available width
        float availableWidth = cardsContainer.rect.width - (horizontalPadding * 2f);

        // Calculate scale factor
        float scale = 1f;
        if (totalWidth > availableWidth && totalWidth > 0f)
            scale = Mathf.Max(minScale, availableWidth / totalWidth);

        // Calculate total scaled width (cards + spacing) for centering
        float totalScaledWidth = totalWidth * scale;
        if (_spawnedCards.Count > 1)
            totalScaledWidth += cardSpacing * (_spawnedCards.Count - 1);

        // Determine starting X position
        float xPos;
        if (autoCenter)
            xPos = -totalScaledWidth / 2f + cardsOffset.x;
        else
            xPos = manualStartX;

        float yPos = cardsY + cardsOffset.y;

        foreach (var card in _spawnedCards)
        {
            var rect = card.GetComponent<RectTransform>();
            if (rect == null)
                continue;

            card.transform.localScale = Vector3.one * scale;

            float cardWidth = rect.rect.width * scale;
            rect.anchoredPosition = new Vector2(xPos + (cardWidth / 2f), yPos);
            xPos += cardWidth + cardSpacing;
        }
    }

    private RoundCardUI GetPrefabForIndex(int index, int focusIndex)
    {
        int distance = Mathf.Abs(index - focusIndex);

        if (distance == 0)
            return largeCardPrefab;
        if (distance == 1)
            return mediumCardPrefab;
        return smallCardPrefab;
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        _spawnedCards.Clear();
    }
}
