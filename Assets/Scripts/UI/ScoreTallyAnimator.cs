using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays a simple "tally" animation for current-ball scoring:
/// Points + Mult move toward the X label, disappear, X becomes the computed total,
/// then that total moves to the Round Total label, disappears, and scoring resets for next ball.
/// </summary>
public class ScoreTallyAnimator : MonoBehaviour
{
    [Header("UI References (assign in Inspector)")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text multText;
    [SerializeField] private TMP_Text xText;
    [SerializeField] private TMP_Text roundTotalText;

    [Header("Timing")]
    [SerializeField] private float moveToXDuration = 0.35f;
    [SerializeField] private float holdAtXDuration = 0.15f;
    [SerializeField] private float moveToRoundTotalDuration = 0.45f;
    [SerializeField] private float endHoldDuration = 0.05f;

    [Header("Behavior")]
    [SerializeField] private bool lockScoreManagerDuringTally = true;

    private Vector3 _pointsStartPos;
    private Vector3 _multStartPos;
    private Vector3 _xStartPos;
    private string _xStartString;

    private bool _initialized;
    public bool IsTallying { get; private set; }

    private const string ScorePanelRootName = "Score Panel";
    private const string PointsObjectName = "Points";
    private const string MultObjectName = "Mult";
    private const string XObjectName = "X";
    private const string RoundTotalObjectName = "RoundTotal";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureTextBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureTextBindings();
    }

    private void CacheInitialStateIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        if (pointsText != null) _pointsStartPos = pointsText.transform.position;
        if (multText != null) _multStartPos = multText.transform.position;
        if (xText != null)
        {
            _xStartPos = xText.transform.position;
            _xStartString = xText.text;
        }
    }

    public IEnumerator PlayTally(ScoreManager scoreManager)
    {
        return PlayTally(scoreManager, 1f);
    }

    public IEnumerator PlayTally(ScoreManager scoreManager, float bankMultiplier)
    {
        if (IsTallying) yield break;
        IsTallying = true;

        EnsureTextBindings();
        CacheInitialStateIfNeeded();

        if (scoreManager != null && lockScoreManagerDuringTally)
            scoreManager.SetScoringLocked(true);

        // If references are missing, fall back to instant bank.
        if (scoreManager == null || pointsText == null || multText == null || xText == null || roundTotalText == null)
        {
            if (scoreManager != null)
                scoreManager.BankCurrentBallScore(bankMultiplier);

            if (scoreManager != null && lockScoreManagerDuringTally)
                scoreManager.SetScoringLocked(false);

            IsTallying = false;
            yield break;
        }

        // Ensure visible before animation starts.
        SetVisible(pointsText, true);
        SetVisible(multText, true);
        SetVisible(xText, true);

        // Step 1: Points + Mult move toward X.
        Vector3 xPos = xText.transform.position;
        yield return MoveTogetherTo(pointsText.transform, _pointsStartPos, xPos,
            multText.transform, _multStartPos, xPos,
            moveToXDuration);

        // Step 2: Hide Points/Mult, turn X into total.
        SetVisible(pointsText, false);
        SetVisible(multText, false);

        float m = bankMultiplier;
        if (m <= 0f) m = 1f;

        float banked = scoreManager.points * scoreManager.mult * m;
        xText.text = banked.ToString();

        if (holdAtXDuration > 0f)
            yield return new WaitForSeconds(holdAtXDuration);

        // Step 3: Total (X text) moves to Round Total.
        Vector3 roundPos = roundTotalText.transform.position;
        yield return MoveTo(xText.transform, xPos, roundPos, moveToRoundTotalDuration);

        if (endHoldDuration > 0f)
            yield return new WaitForSeconds(endHoldDuration);

        // Commit bank into round total + reset scoring for next ball.
        scoreManager.BankCurrentBallScore(bankMultiplier);

        // Hide the moving total, then reset all visuals back to "ready for next ball".
        SetVisible(xText, false);

        // Reset transforms and UI state.
        pointsText.transform.position = _pointsStartPos;
        multText.transform.position = _multStartPos;
        xText.transform.position = _xStartPos;
        xText.text = _xStartString;

        // ScoreManager refreshed points/mult/roundTotal text values; now make them visible again.
        SetVisible(pointsText, true);
        SetVisible(multText, true);
        SetVisible(xText, true);

        if (scoreManager != null && lockScoreManagerDuringTally)
            scoreManager.SetScoringLocked(false);

        IsTallying = false;
    }

    private void EnsureTextBindings()
    {
        if (IsLiveSceneText(pointsText) && IsLiveSceneText(multText) && IsLiveSceneText(xText) && IsLiveSceneText(roundTotalText))
            return;

        // Prefer binding within a Score Panel root if present (works regardless of which additive scene it lives in).
        GameObject scorePanel = GameObject.Find(ScorePanelRootName);
        if (scorePanel != null)
        {
            if (!IsLiveSceneText(pointsText)) pointsText = FindTmpTextInChildrenByName(scorePanel.transform, PointsObjectName);
            if (!IsLiveSceneText(multText)) multText = FindTmpTextInChildrenByName(scorePanel.transform, MultObjectName);
            if (!IsLiveSceneText(xText)) xText = FindTmpTextInChildrenByName(scorePanel.transform, XObjectName);
        }

        if (!IsLiveSceneText(roundTotalText))
        {
            // Round total may live outside the Score Panel (e.g., another HUD element).
            // Try exact name match first.
            roundTotalText = FindTmpTextInLoadedScenesByName(RoundTotalObjectName);
        }

        // As a last resort, also allow X to be found globally (if your panel structure changes).
        if (!IsLiveSceneText(xText))
            xText = FindTmpTextInLoadedScenesByName(XObjectName);

        if (!IsLiveSceneText(pointsText))
            pointsText = FindTmpTextInLoadedScenesByName(PointsObjectName);
        if (!IsLiveSceneText(multText))
            multText = FindTmpTextInLoadedScenesByName(MultObjectName);
    }

    private static TMP_Text FindTmpTextInChildrenByName(Transform root, string childName)
    {
        if (root == null) return null;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private static TMP_Text FindTmpTextInLoadedScenesByName(string objectName)
    {
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text t = allTexts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
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

    private static void SetVisible(TMP_Text t, bool visible)
    {
        if (t == null) return;
        t.alpha = visible ? 1f : 0f;
    }

    private static IEnumerator MoveTo(Transform t, Vector3 from, Vector3 to, float duration)
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
            // SmoothStep reads nicer for UI motion.
            float s = u * u * (3f - 2f * u);
            t.position = Vector3.LerpUnclamped(from, to, s);
            yield return null;
        }

        t.position = to;
    }

    private static IEnumerator MoveTogetherTo(
        Transform a, Vector3 aFrom, Vector3 aTo,
        Transform b, Vector3 bFrom, Vector3 bTo,
        float duration)
    {
        if (duration <= 0f)
        {
            if (a != null) a.position = aTo;
            if (b != null) b.position = bTo;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float s = u * u * (3f - 2f * u);

            if (a != null) a.position = Vector3.LerpUnclamped(aFrom, aTo, s);
            if (b != null) b.position = Vector3.LerpUnclamped(bFrom, bTo, s);

            yield return null;
        }

        if (a != null) a.position = aTo;
        if (b != null) b.position = bTo;
    }
}

