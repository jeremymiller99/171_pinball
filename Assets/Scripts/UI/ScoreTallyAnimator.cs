// Updated with Antigravity by jjmil on 2026-04-07
// (simplified tally: Points fly to RoundTotal, removed Points×Mult→X flow).
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays a simplified "tally" animation on ball drain:
/// The current ball's earned points fly from the drain position to the
/// Round Total label, then banking commits the score.
/// </summary>
public class ScoreTallyAnimator : MonoBehaviour
{
    [Header("UI References (assign in Inspector)")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text roundTotalText;

    [Header("World Space Start (optional)")]
    [Tooltip(
        "Camera used to convert a world position to a screen " +
        "point. If null, uses Camera.main.")]
    [SerializeField] private Camera worldCamera;
    [Tooltip(
        "Canvas used to project a world position onto the " +
        "score UI plane. If null, inferred from roundTotalText.")]
    [SerializeField] private Canvas scoreCanvas;

    [Header("Timing")]
    [SerializeField] private float moveToRoundTotalDuration = 0.45f;
    [SerializeField] private float endHoldDuration = 0.05f;

    [Header("Behavior")]
    [SerializeField] private bool lockScoreManagerDuringTally = true;

    private Vector3 _roundTotalStartPos;
    private bool _initialized;
    public bool IsTallying { get; private set; }

    private const string ScoreCanvasRootName = "Score Canvas";
    private const string RoundTotalObjectName = "RoundTotal";
    private const string RoundScoreObjectName = "Round Score";

    private static string FormatPointsCompact(double value) => FormatPointsCompact((float)value);

    private static string FormatPointsCompact(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < 10000f)
        {
            float rounded1 = Mathf.Round(value * 10f) / 10f;
            return rounded1.ToString(
                "#,##0.0",
                CultureInfo.InvariantCulture);
        }

        float scale = 1000f;
        string suffix = "K";
        if (abs >= 1000000000f)
        {
            scale = 1000000000f;
            suffix = "B";
        }
        else if (abs >= 1000000f)
        {
            scale = 1000000f;
            suffix = "M";
        }

        float scaled = abs / scale;
        float scaledRounded1 =
            Mathf.Round(scaled * 10f) / 10f;

        if (scaledRounded1 >= 1000f)
        {
            if (suffix == "K")
            {
                scale = 1000000f;
                suffix = "M";
            }
            else if (suffix == "M")
            {
                scale = 1000000000f;
                suffix = "B";
            }

            scaled = abs / scale;
            scaledRounded1 =
                Mathf.Round(scaled * 10f) / 10f;
        }

        if (scaledRounded1 >= 100f)
        {
            string s = Mathf.RoundToInt(scaledRounded1)
                .ToString(CultureInfo.InvariantCulture) + suffix;
            return value < 0f ? "-" + s : s;
        }

        string core = scaledRounded1.ToString(
            "#,##0.#", CultureInfo.InvariantCulture) + suffix;
        return value < 0f ? "-" + core : core;
    }

    private void OnEnable()
    {
        ServiceLocator.Register<ScoreTallyAnimator>(this);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureTextBindings();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<ScoreTallyAnimator>();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(
        Scene scene, LoadSceneMode mode)
    {
        EnsureTextBindings();
    }

    public void ResetCachedPositions()
    {
        _initialized = false;
    }

    private void CacheInitialStateIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        if (roundTotalText != null)
            _roundTotalStartPos =
                roundTotalText.transform.position;
    }

    public IEnumerator PlayTally(ScoreManager scoreManager)
    {
        return PlayTally(scoreManager, null);
    }

    /// <summary>
    /// Plays the tally animation, optionally starting a
    /// floating score label at a world-space position that
    /// flies to the round total text.
    /// </summary>
    public IEnumerator PlayTally(
        ScoreManager scoreManager,
        Vector3? worldStartPosition)
    {
        if (IsTallying) yield break;
        IsTallying = true;

        EnsureTextBindings();
        CacheInitialStateIfNeeded();

        if (scoreManager != null && lockScoreManagerDuringTally)
            scoreManager.SetScoringLocked(true);

        // Fallback: if references missing, bank instantly.
        if (scoreManager == null || roundTotalText == null)
        {
            if (scoreManager != null)
                scoreManager.BankCurrentBallScore();

            if (scoreManager != null
                && lockScoreManagerDuringTally)
                scoreManager.SetScoringLocked(false);

            IsTallying = false;
            yield break;
        }

        double ballPoints = scoreManager.Points;
        string flyingLabel =
            FormatPointsCompact(ballPoints);

        // Create a temporary flying text element.
        TMP_Text flyText = null;
        Vector3 flyStartPos = _roundTotalStartPos;

        if (pointsText != null)
        {
            pointsText.text = flyingLabel;
            SetVisible(pointsText, true);
            flyText = pointsText;

            if (worldStartPosition.HasValue
                && TryProjectWorldPointToCanvas(
                    worldStartPosition.Value,
                    out Vector3 projected))
            {
                flyStartPos = projected;
                flyText.transform.position = flyStartPos;
            }
            else
            {
                flyStartPos =
                    flyText.transform.position;
            }
        }

        // Animate: fly the points label to the
        // round total position.
        if (flyText != null)
        {
            Vector3 roundPos =
                roundTotalText.transform.position;
            yield return MoveTo(
                flyText.transform,
                flyStartPos,
                roundPos,
                moveToRoundTotalDuration);
        }

        if (endHoldDuration > 0f)
            yield return new WaitForSeconds(
                endHoldDuration);

        // Commit bank into round total.
        scoreManager.BankCurrentBallScore();

        // Hide flying text and reset.
        if (flyText != null)
        {
            SetVisible(flyText, false);
            flyText.transform.position =
                _roundTotalStartPos;
        }

        // Update round total display.
        roundTotalText.text =
            FormatPointsCompact(scoreManager.RoundTotal);

        if (scoreManager != null
            && lockScoreManagerDuringTally)
            scoreManager.SetScoringLocked(false);

        IsTallying = false;
    }

    private void EnsureTextBindings()
    {
        if (IsLiveSceneText(roundTotalText)
            && IsLiveSceneText(pointsText))
            return;

        // Try Score Canvas root first.
        GameObject scoreCanvasRoot =
            GameObject.Find(ScoreCanvasRootName);
        if (scoreCanvasRoot != null)
        {
            if (!IsLiveSceneText(roundTotalText))
            {
                roundTotalText =
                    FindTmpTextInChildrenByName(
                        scoreCanvasRoot.transform,
                        RoundTotalObjectName);
                if (!IsLiveSceneText(roundTotalText))
                    roundTotalText =
                        FindTmpTextInChildrenByName(
                            scoreCanvasRoot.transform,
                            RoundScoreObjectName);
            }
        }

        // Global fallback.
        if (!IsLiveSceneText(roundTotalText))
        {
            roundTotalText =
                FindTmpTextInLoadedScenesByName(
                    RoundTotalObjectName);
            if (!IsLiveSceneText(roundTotalText))
                roundTotalText =
                    FindTmpTextInLoadedScenesByName(
                        RoundScoreObjectName);
        }

        // pointsText: a temporary flying label.
        // If not assigned, create one at runtime.
        if (!IsLiveSceneText(pointsText)
            && roundTotalText != null)
        {
            var go = new GameObject("TallyFlyText");
            go.transform.SetParent(
                roundTotalText.transform.parent,
                worldPositionStays: false);

            var tmp =
                go.AddComponent<TextMeshProUGUI>();
            tmp.font = roundTotalText.font;
            tmp.fontSize = roundTotalText.fontSize;
            tmp.color = roundTotalText.color;
            tmp.alignment = roundTotalText.alignment;
            tmp.raycastTarget = false;
            SetVisible(tmp, false);
            pointsText = tmp;
        }

        if (scoreCanvas == null && roundTotalText != null)
            scoreCanvas =
                roundTotalText
                    .GetComponentInParent<Canvas>();
    }

    private bool TryProjectWorldPointToCanvas(
        Vector3 worldPoint,
        out Vector3 projectedWorldOnCanvasPlane)
    {
        projectedWorldOnCanvasPlane = default;

        if (scoreCanvas == null && roundTotalText != null)
            scoreCanvas =
                roundTotalText
                    .GetComponentInParent<Canvas>();

        Camera cam = worldCamera != null
            ? worldCamera
            : (scoreCanvas != null
               && scoreCanvas.worldCamera != null
                    ? scoreCanvas.worldCamera
                    : FindLikelyMainCamera());
        if (cam == null) return false;
        if (scoreCanvas == null) return false;

        RectTransform canvasRect =
            scoreCanvas.transform as RectTransform;
        if (canvasRect == null) return false;

        Vector3 screenPos3 =
            cam.WorldToScreenPoint(worldPoint);
        Vector2 screenPos =
            new Vector2(screenPos3.x, screenPos3.y);

        Camera uiCam =
            scoreCanvas.renderMode
                == RenderMode.ScreenSpaceOverlay
            ? null
            : (scoreCanvas.worldCamera != null
                ? scoreCanvas.worldCamera
                : cam);

        return RectTransformUtility
            .ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPos,
                uiCam,
                out projectedWorldOnCanvasPlane);
    }

    private static Camera FindLikelyMainCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cams = FindObjectsByType<Camera>(
            FindObjectsSortMode.None);

        if (cams == null || cams.Length == 0)
            return null;

        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null || !c.enabled) continue;
            if (c.gameObject != null
                && c.gameObject.activeInHierarchy
                && c.CompareTag("MainCamera"))
                return c;
        }

        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null || !c.enabled) continue;
            if (c.gameObject != null
                && c.gameObject.activeInHierarchy
                && c.name.IndexOf(
                    "Main",
                    System.StringComparison
                        .OrdinalIgnoreCase) >= 0)
                return c;
        }

        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null || !c.enabled) continue;
            if (c.gameObject != null
                && c.gameObject.activeInHierarchy)
                return c;
        }

        return cams[0];
    }

    private static TMP_Text FindTmpTextInChildrenByName(
        Transform root, string childName)
    {
        if (root == null) return null;
        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(
                includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(
                    t.gameObject.name,
                    childName,
                    System.StringComparison
                        .OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private static TMP_Text
        FindTmpTextInLoadedScenesByName(string objectName)
    {
        TMP_Text[] allTexts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text t = allTexts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(
                    t.gameObject.name,
                    objectName,
                    System.StringComparison
                        .OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private static bool IsLiveSceneText(TMP_Text t)
    {
        if (t == null) return false;
        if (!t.gameObject.scene.IsValid()) return false;
        if (!t.gameObject.activeInHierarchy) return false;
        return true;
    }

    private static void SetVisible(
        TMP_Text t, bool visible)
    {
        if (t == null) return;
        t.alpha = visible ? 1f : 0f;
    }

    private static IEnumerator MoveTo(
        Transform t,
        Vector3 from,
        Vector3 to,
        float duration)
    {
        if (t == null) yield break;
        if (duration <= 0f)
        {
            t.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float s = u * u * (3f - 2f * u);
            t.position =
                Vector3.LerpUnclamped(from, to, s);
            yield return null;
        }

        t.position = to;
    }
}
