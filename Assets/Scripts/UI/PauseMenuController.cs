// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.
// Modified with Claude Code (Opus 4.7) by JJ on 2026-04-20: added Settings Panel access.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private const string PauseMenuPanelName = "Pause Menu";
    private const string ResumeButtonName = "Resume Button";
    private const string QuitButtonName = "Quit Button";
    private const string SettingsButtonName = "Settings Button";
    private const float MinFixedDeltaTime = 0.0001f;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("UI (optional - auto-wired if left blank)")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanelPrefab;

    [Header("Behavior")]
    [SerializeField] private bool pauseAudioListener = true;
    [SerializeField] private bool showCursorWhenPaused = true;

    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("Runtime (debug)")]
    [SerializeField] private bool isOpen;

    private bool _pausedByThisMenu;
    private float _timeScaleBeforePause = 1f;
    private float _fixedDeltaBeforePause = 0.02f;
    private bool _warnedMissingUi;

    private CursorLockMode _cursorLockBefore;
    private bool _cursorVisibleBefore;

    private readonly List<Behaviour> _disabledWhileOpen = new List<Behaviour>();

    private bool _buttonsWired;

    private GameObject _settingsInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallIfPauseMenuExists()
    {
        if (FindAnyPauseMenuController() != null)
        {
            return;
        }

        GameObject panel = FindPanelLikeObject(PauseMenuPanelName);
        if (panel == null)
        {
            return;
        }

        Canvas canvas = panel.GetComponentInParent<Canvas>(includeInactive: true);
        if (canvas == null)
        {
            return;
        }

        canvas.gameObject.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        AutoWireUiIfNeeded();
        EnsurePanelHiddenAtStart();
        WireButtonsIfPossible();
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            Close();
        }
    }

    private void Update()
    {
        if (isOpen)
        {
            EnforcePausedIfNeeded();
        }

        if (pauseAction == null || pauseAction.action == null
            || !pauseAction.action.WasPressedThisFrame())
        {
            return;
        }

        if (_settingsInstance != null)
        {
            CloseSettings();
            return;
        }

        if (!AutoWireUiIfNeeded())
        {
            if (!_warnedMissingUi)
            {
                _warnedMissingUi = true;
                Debug.LogWarning($"{nameof(PauseMenuController)}: Could not find Pause Menu UI.", this);
            }
            return;
        }

        WireButtonsIfPossible();

        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void LateUpdate()
    {
        if (!isOpen)
        {
            return;
        }

        EnforcePausedIfNeeded();
    }

    private void Open()
    {
        if (pauseMenuPanel == null)
        {
            return;
        }

        isOpen = true;
        pauseMenuPanel.SetActive(true);
        ServiceLocator.Get<AudioManager>()?.SetMusicMuffled(true);

        DisableWhileOpen();
        PauseTimeIfPossible();
        ApplyPausedCursor();

        if (resumeButton != null)
        {
            SelectButton(resumeButton);
        }
        else if (quitButton != null)
        {
            SelectButton(quitButton);
        }
    }

    private void Close()
    {
        isOpen = false;

        if (_settingsInstance != null)
        {
            Destroy(_settingsInstance);
            _settingsInstance = null;
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        ServiceLocator.Get<AudioManager>()?.SetMusicMuffled(false);

        RestoreDisabledWhileOpen();
        RestoreCursor();
        UnpauseTimeIfNeeded();
    }

    private void DisableWhileOpen()
    {
        _disabledWhileOpen.Clear();

        DisableIfEnabled<CameraAliveMotion>();
    }

    private void RestoreDisabledWhileOpen()
    {
        for (int i = 0; i < _disabledWhileOpen.Count; i++)
        {
            Behaviour b = _disabledWhileOpen[i];
            if (b != null)
            {
                b.enabled = true;
            }
        }

        _disabledWhileOpen.Clear();
    }

    private void DisableIfEnabled<T>() where T : Behaviour
    {
        T[] all;
        all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            T b = all[i];
            if (b == null || !b.enabled) continue;
            b.enabled = false;
            _disabledWhileOpen.Add(b);
        }
    }

    private void PauseTimeIfPossible()
    {
        if (Time.timeScale <= 0f)
        {
            _pausedByThisMenu = false;
            return;
        }

        _timeScaleBeforePause = Time.timeScale;
        _fixedDeltaBeforePause = Time.fixedDeltaTime;

        Time.timeScale = 0f;
        _pausedByThisMenu = true;

        if (pauseAudioListener)
        {
            AudioListener.pause = true;
        }
    }

    private void EnforcePausedIfNeeded()
    {
        if (!_pausedByThisMenu)
        {
            return;
        }

        if (!Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 0f;
        }

        if (pauseAudioListener && !AudioListener.pause)
        {
            AudioListener.pause = true;
        }
    }

    private void UnpauseTimeIfNeeded()
    {
        if (!_pausedByThisMenu)
        {
            return;
        }

        float restored = Mathf.Max(0f, _timeScaleBeforePause);
        if (restored <= 0f)
        {
            restored = 1f;
        }

        Time.timeScale = restored;
        Time.fixedDeltaTime = Mathf.Max(MinFixedDeltaTime, _fixedDeltaBeforePause);
        _pausedByThisMenu = false;

        if (pauseAudioListener)
        {
            AudioListener.pause = false;
        }
    }

    private void QuitToMainMenu()
    {
        Close();

        if (ProgressionService.Instance != null)
        {
            ProgressionService.Instance.CheckAndGrantUnlocks();
        }

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void Resume()
    {
        Close();
    }

    private void WireButtonsIfPossible()
    {
        if (_buttonsWired)
        {
            return;
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(Resume);
            resumeButton.onClick.AddListener(Resume);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitToMainMenu);
            quitButton.onClick.AddListener(QuitToMainMenu);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
            settingsButton.onClick.AddListener(OpenSettings);
        }

        _buttonsWired = resumeButton != null
            || quitButton != null
            || settingsButton != null;
    }

    private void OpenSettings()
    {
        if (_settingsInstance != null)
        {
            return;
        }

        if (settingsPanelPrefab == null)
        {
            Debug.LogWarning(
                $"{nameof(PauseMenuController)}: settingsPanelPrefab is not assigned.",
                this);
            return;
        }

        Transform parent = pauseMenuPanel != null
            ? pauseMenuPanel.transform.parent
            : transform;

        _settingsInstance = Instantiate(settingsPanelPrefab, parent, false);
        _settingsInstance.SetActive(true);

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        SelectFirstSelectable(_settingsInstance);
    }

    private void CloseSettings()
    {
        if (_settingsInstance != null)
        {
            Destroy(_settingsInstance);
            _settingsInstance = null;
        }

        if (!isOpen)
        {
            return;
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        if (resumeButton != null)
        {
            SelectButton(resumeButton);
        }
    }

    private static void SelectFirstSelectable(GameObject root)
    {
        if (root == null || EventSystem.current == null) return;

        Selectable first = root.GetComponentInChildren<Selectable>(true);
        if (first != null)
        {
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    private bool AutoWireUiIfNeeded()
    {
        if (pauseMenuPanel == null)
        {
            pauseMenuPanel = FindPanelLikeObject(PauseMenuPanelName);
        }

        if (pauseMenuPanel == null)
        {
            return false;
        }

        if (resumeButton == null)
        {
            resumeButton = FindButtonUnder(pauseMenuPanel, ResumeButtonName);
        }

        if (quitButton == null)
        {
            quitButton = FindButtonUnder(pauseMenuPanel, QuitButtonName);
        }

        if (settingsButton == null)
        {
            settingsButton = FindButtonUnder(pauseMenuPanel, SettingsButtonName);
        }

        return true;
    }

    private void EnsurePanelHiddenAtStart()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    private void ApplyPausedCursor()
    {
        if (!showCursorWhenPaused)
        {
            return;
        }

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!showCursorWhenPaused)
        {
            return;
        }

        Cursor.lockState = _cursorLockBefore;
        Cursor.visible = _cursorVisibleBefore;
    }

    private static void SelectButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static PauseMenuController FindAnyPauseMenuController()
    {
        return ServiceLocator.Get<PauseMenuController>();

    }

    private static GameObject FindPanelLikeObject(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        List<Transform> transforms = FindAllTransformsInLoadedScenes(includeInactive: true);
        for (int i = 0; i < transforms.Count; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.name != name) continue;

            if (t.GetComponent<RectTransform>() == null) continue;

            return t.gameObject;
        }

        return null;
    }

    private static Button FindButtonUnder(GameObject root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.name != name) continue;

            Button btn = t.GetComponent<Button>();
            if (btn != null)
            {
                return btn;
            }
        }

        return null;
    }

    private static List<Transform> FindAllTransformsInLoadedScenes(bool includeInactive)
    {
        Transform[] all;

        all = Object.FindObjectsByType<Transform>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        var result = new List<Transform>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;

            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;

            if ((t.hideFlags & HideFlags.NotEditable) != 0) continue;
            if ((t.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

            if (!includeInactive && !t.gameObject.activeInHierarchy) continue;

            result.Add(t);
        }

        return result;
    }
}

