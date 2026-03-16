// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.
// RestoreAndHide added by Cursor for jjmil on 2026-03-15.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WinScreenController : MonoBehaviour
{
    private const string winScreenRootName = "Win Screen";
    private const string promptChildName = "Prompt";
    private const string promptFallbackContains = "You reached";
    private const string quitButtonName = "Quit Button";
    private const string overlayCanvasName = "WinScreenOverlayCanvas";

    [Header("Runtime (debug)")]
    [SerializeField] private GameObject winScreenRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button quitButton;

    private float _timeScaleBefore = 1f;
    private float _fixedDeltaBefore = 0.02f;
    private bool _pausedByThis;
    private CursorLockMode _cursorLockBefore;
    private bool _cursorVisibleBefore;

    private readonly Dictionary<Behaviour, bool> _disabledInputBehaviours = new Dictionary<Behaviour, bool>();

    public static bool Show(int levelReached, long totalPoints)
    {
        GameObject root = FindWinScreenRoot();
        if (root == null)
        {
            Debug.LogWarning($"{nameof(WinScreenController)}: Could not find '{winScreenRootName}' in loaded scenes.");
            return false;
        }

        var controller = root.GetComponent<WinScreenController>();
        if (controller == null)
        {
            controller = root.AddComponent<WinScreenController>();
        }

        controller.ShowInternal(levelReached, totalPoints);
        return true;
    }

    private void OnDestroy()
    {
        RestorePauseAndInput();
    }

    private void ShowInternal(int levelReached, long totalPoints)
    {
        winScreenRoot = gameObject;

        EnsureOverlayCanvasParent();
        EnsureUiBindings();

        if (promptText != null)
        {
            promptText.text = $"You reached Level {levelReached} and scored {totalPoints} points!";
        }

        gameObject.SetActive(true);

        PauseAndDisableInput();
        WireQuitButtonIfPossible();
    }

    private void EnsureOverlayCanvasParent()
    {
        GameObject overlayCanvas = GameObject.Find(overlayCanvasName);
        if (overlayCanvas == null)
        {
            overlayCanvas = new GameObject(overlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var c = overlayCanvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10000;

            var scaler = overlayCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform canvasRt = overlayCanvas.GetComponent<RectTransform>();
        if (canvasRt != null)
        {
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.anchoredPosition = Vector2.zero;
            canvasRt.sizeDelta = Vector2.zero;
            canvasRt.localScale = Vector3.one;
        }

        Transform t = transform;
        if (t.parent != overlayCanvas.transform)
        {
            t.SetParent(overlayCanvas.transform, worldPositionStays: false);
        }
    }

    private void EnsureUiBindings()
    {
        if (promptText == null)
        {
            Transform promptChild = FindChildByName(transform, promptChildName);
            if (promptChild != null)
            {
                promptText = promptChild.GetComponent<TMP_Text>();
            }
        }

        if (promptText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(includeInactive: true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null) continue;
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.text.IndexOf(promptFallbackContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    promptText = t;
                    break;
                }
            }
        }

        if (quitButton == null)
        {
            Transform quitChild = FindChildByName(transform, quitButtonName);
            if (quitChild != null)
            {
                quitButton = quitChild.GetComponent<Button>();
            }
        }
    }

    private void PauseAndDisableInput()
    {
        if (_pausedByThis)
        {
            return;
        }

        _timeScaleBefore = Time.timeScale;
        _fixedDeltaBefore = Time.fixedDeltaTime;

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;

        Time.timeScale = 0f;
        _pausedByThis = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisableGameplayInputBehaviours();
    }

    private void RestorePauseAndInput()
    {
        if (!_pausedByThis)
        {
            return;
        }

        float restored = Mathf.Max(0f, _timeScaleBefore);
        if (restored <= 0f)
        {
            restored = 1f;
        }

        Time.timeScale = restored;
        Time.fixedDeltaTime = Mathf.Max(0.0001f, _fixedDeltaBefore);

        Cursor.lockState = _cursorLockBefore;
        Cursor.visible = _cursorVisibleBefore;

        RestoreDisabledGameplayInputBehaviours();

        _pausedByThis = false;
    }

    private void WireQuitButtonIfPossible()
    {
        if (quitButton == null)
        {
            return;
        }

        quitButton.onClick.RemoveListener(OnQuitClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnQuitClicked()
    {
        var session = GameSession.Instance;
        if (session != null)
        {
            session.ResetSession();
        }

        RestorePauseAndInput();
    }

    /// <summary>
    /// Restores pause state and hides the win screen. Call when entering endless mode.
    /// </summary>
    public void RestoreAndHide()
    {
        RestorePauseAndInput();
        if (gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    private void DisableGameplayInputBehaviours()
    {
        _disabledInputBehaviours.Clear();

        DisableIfEnabledByTypeName("PinballLauncher");
        DisableIfEnabledByTypeName("PinballFlipper");
    }

    private void DisableIfEnabledByTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return;
        }

        Behaviour[] all = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Behaviour b = all[i];
            if (b == null) continue;
            if (!b.enabled) continue;
            if (!string.Equals(b.GetType().Name, typeName, StringComparison.Ordinal)) continue;

            if (!_disabledInputBehaviours.ContainsKey(b))
            {
                _disabledInputBehaviours.Add(b, true);
            }

            b.enabled = false;
        }
    }

    private void RestoreDisabledGameplayInputBehaviours()
    {
        foreach (KeyValuePair<Behaviour, bool> kvp in _disabledInputBehaviours)
        {
            Behaviour b = kvp.Key;
            if (b != null)
            {
                b.enabled = kvp.Value;
            }
        }

        _disabledInputBehaviours.Clear();
    }

    private static GameObject FindWinScreenRoot()
    {
        GameObject go = GameObject.Find(winScreenRootName);
        if (go != null)
        {
            return go;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
            if (t.name != winScreenRootName) continue;

            return t.gameObject;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}

