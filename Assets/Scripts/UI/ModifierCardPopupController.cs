// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.
// Updated with Cursor (Composer) by assistant on 2026-03-31: CurrentRoundData from RoundModifierController.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ModifierCardPopupController : MonoBehaviour
{
    private const string GameplayCoreSceneName = "GameplayCore";
    private const string PanelObjectName = "Modifier Card Panel";

    [Header("Visibility")]
    [SerializeField] private bool showOnNormalRounds = false;
    [SerializeField] private bool showOnAngelRounds = true;
    [SerializeField] private bool showOnDevilRounds = true;

    [Header("Runtime (debug)")]
    [SerializeField] private bool isHooked;
    [SerializeField] private int lastShownLevelIndex = -1;

    private GameRulesManager _rules;
    private ModifierCardPanelController _panel;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryResolveAndHook();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unhook();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (!string.Equals(scene.name, GameplayCoreSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lastShownLevelIndex = -1;
        TryResolveAndHook();
    }

    private void TryResolveAndHook()
    {
        ResolvePanel();
        ResolveRulesManager();
        Hook();

        _panel?.Hide();
    }

    private void ResolvePanel()
    {
        Scene scene = SceneManager.GetSceneByName(GameplayCoreSceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject panelGo = FindGameObjectInSceneByName(scene, PanelObjectName);
        if (panelGo == null)
        {
            return;
        }

        _panel = panelGo.GetComponent<ModifierCardPanelController>();
        if (_panel == null)
        {
            _panel = panelGo.AddComponent<ModifierCardPanelController>();
        }
    }

    private void ResolveRulesManager()
    {
        if (_rules != null && _rules.gameObject != null &&
            _rules.gameObject.scene.IsValid() &&
            string.Equals(
                _rules.gameObject.scene.name,
                GameplayCoreSceneName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _rules = null;

        GameRulesManager[] all = FindObjectsByType<GameRulesManager>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameRulesManager r = all[i];
            if (r == null) continue;
            if (!r.gameObject.scene.IsValid()) continue;
            if (!string.Equals(r.gameObject.scene.name, GameplayCoreSceneName, StringComparison.OrdinalIgnoreCase))
                continue;

            _rules = r;
            break;
        }
    }

    private void Hook()
    {
        Unhook();

        if (_rules == null)
        {
            isHooked = false;
            return;
        }

        _rules.LevelChanged += OnLevelChanged;
        _rules.RoundStarted += OnRoundStarted;
        _rules.ShopOpened += OnShopOpened;
        _rules.ShopClosed += OnShopClosed;

        isHooked = true;
    }

    private void Unhook()
    {
        if (_rules != null)
        {
            _rules.LevelChanged -= OnLevelChanged;
            _rules.RoundStarted -= OnRoundStarted;
            _rules.ShopOpened -= OnShopOpened;
            _rules.ShopClosed -= OnShopClosed;
        }

        isHooked = false;
    }

    private void OnLevelChanged()
    {
        TryShowForCurrentRound();
    }

    private void OnRoundStarted()
    {
        TryShowForCurrentRound();
    }

    private void OnShopOpened()
    {
        _panel?.Hide();
    }

    private void OnShopClosed()
    {
        _panel?.Hide();
    }

    private void TryShowForCurrentRound()
    {
        if (_rules == null)
        {
            TryResolveAndHook();
            if (_rules == null)
            {
                return;
            }
        }

        if (_panel == null)
        {
            ResolvePanel();
            if (_panel == null)
            {
                return;
            }
        }

        RoundData data = ServiceLocator.Get<RoundModifierController>()?.CurrentRoundData;
        if (data == null)
        {
            return;
        }

        if (data.type == RoundType.Normal && !showOnNormalRounds)
        {
            return;
        }

        if (data.type == RoundType.Angel && !showOnAngelRounds)
        {
            return;
        }

        if (data.type == RoundType.Devil && !showOnDevilRounds)
        {
            return;
        }

        int levelIndex = Mathf.Max(0, _rules.LevelIndex);
        if (levelIndex == lastShownLevelIndex)
        {
            return;
        }

        // Don't show modifier card on first round when player is getting the tutorial (first-time prompt).
        if (levelIndex == 0 && !ProfileService.HasAnsweredFirstTimePlayingPrompt())
        {
            return;
        }

        lastShownLevelIndex = levelIndex;
        _panel.Show(data, levelIndex);
    }

    private static GameObject FindGameObjectInSceneByName(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null) continue;
            if (go.scene != scene) continue;

            if (string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return go;
            }
        }

        return null;
    }
}

internal sealed class ModifierCardPopupBootstrapper : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall();
    }

    private void TryInstall()
    {
        if (GetComponent<ModifierCardPopupController>() != null)
        {
            return;
        }

        gameObject.AddComponent<ModifierCardPopupController>();
    }
}

internal static class ModifierCardPopupBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (ServiceLocator.Get<ModifierCardPopupBootstrapper>() != null)
        {
            return;
        }

        var go = new GameObject(nameof(ModifierCardPopupBootstrapper));
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<ModifierCardPopupBootstrapper>();
    }
}

