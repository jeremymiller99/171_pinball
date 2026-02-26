// Generated with Cursor (GPT-5.3-codex) by OpenAI assistant for jjmil on 2026-02-26.
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime installer that duplicates the existing Instant Win debug button and rewires
/// the duplicate as a Reset Level debug button.
/// </summary>
[DisallowMultipleComponent]
public sealed class DebugResetLevelButtonInstaller : MonoBehaviour
{
    private const string installerObjectName = "DebugResetLevelButtonInstaller";
    private const string resetButtonName = "Reset Level Button";
    private const string resetButtonLabel = "Reset Level";
    private const float defaultVerticalSpacing = 10f;

    private static bool installerCreated;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstallerExists()
    {
        if (installerCreated)
        {
            return;
        }

        GameObject installerObject = new GameObject(installerObjectName);
        DontDestroyOnLoad(installerObject);
        installerObject.AddComponent<DebugResetLevelButtonInstaller>();
        installerCreated = true;
    }


    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstallButton();
    }


    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        TryInstallButton();
    }


    private static void TryInstallButton()
    {
        if (FindFirstObjectByType<ResetLevelButton>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        InstantWinButton instantWin = FindFirstObjectByType<InstantWinButton>(FindObjectsInactive.Include);
        if (instantWin == null)
        {
            return;
        }

        Button sourceButton = instantWin.GetComponent<Button>();
        if (sourceButton == null)
        {
            Debug.LogWarning(
                $"{nameof(DebugResetLevelButtonInstaller)}: Instant Win button has no Button component.");
            return;
        }

        Transform parent = sourceButton.transform.parent;
        if (parent == null)
        {
            Debug.LogWarning(
                $"{nameof(DebugResetLevelButtonInstaller)}: Instant Win button has no parent transform.");
            return;
        }

        GameObject clone = Instantiate(sourceButton.gameObject, parent);
        clone.name = resetButtonName;

        InstantWinButton cloneInstantWin = clone.GetComponent<InstantWinButton>();
        if (cloneInstantWin != null)
        {
            Destroy(cloneInstantWin);
        }

        if (clone.GetComponent<ResetLevelButton>() == null)
        {
            clone.AddComponent<ResetLevelButton>();
        }

        SetButtonLabel(clone.transform);
        PositionButtonBelowSource(sourceButton.transform as RectTransform, clone.transform as RectTransform);
    }


    private static void SetButtonLabel(Transform buttonRoot)
    {
        if (buttonRoot == null)
        {
            return;
        }

        TMP_Text tmpText = buttonRoot.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmpText != null)
        {
            tmpText.text = resetButtonLabel;
            return;
        }

        Text legacyText = buttonRoot.GetComponentInChildren<Text>(includeInactive: true);
        if (legacyText != null)
        {
            legacyText.text = resetButtonLabel;
        }
    }


    private static void PositionButtonBelowSource(RectTransform source, RectTransform clone)
    {
        if (source == null || clone == null)
        {
            return;
        }

        Vector2 position = source.anchoredPosition;
        float sourceHeight = source.rect.height;
        float verticalOffset = sourceHeight + defaultVerticalSpacing;
        clone.anchoredPosition = new Vector2(position.x, position.y - verticalOffset);
        clone.localScale = source.localScale;
    }
}
