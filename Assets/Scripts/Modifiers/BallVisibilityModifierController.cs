using System.Collections;
using UnityEngine;

/// <summary>
/// Applies the "ball visibility cycle" modifier effect (e.g. Guess Where).
/// When the active round modifier has cycleBallVisibility enabled, the ball disappears
/// every N seconds for M seconds (physics unchanged). Works with any ball type.
///
/// Attach this to the same scene as GameRulesManager (e.g. GameplayCore) — on the
/// GameRulesManager GameObject or any persistent gameplay object. It will find
/// GameRulesManager and the active ball automatically if not assigned.
/// </summary>
public class BallVisibilityModifierController : MonoBehaviour
{
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Transition (juice)")]
    [Tooltip("Duration of the fade-out when the ball disappears. Longer = slower, smoother fade.")]
    [Min(0.05f)]
    [SerializeField] private float vanishDuration = 1f;

    [Tooltip("Duration of the fade-in when the ball reappears. Longer = slower, smoother fade.")]
    [Min(0.05f)]
    [SerializeField] private float appearDuration = 1f;

    [Tooltip("Shrink/grow the ball visually during transition. OFF by default: scaling the collider can let the ball slip through flippers/gaps. Enable only if your ball has a 'Visual' child (mesh only, no collider).")]
    [SerializeField] private bool useScaleAnimation = false;

    [Tooltip("Ease curve for vanish (0=opaque, 1=transparent).")]
    [SerializeField] private AnimationCurve vanishCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Ease curve for appear (0=transparent, 1=opaque).")]
    [SerializeField] private AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine _cycleCoroutine;
    private GameObject _trackedBall;
    private Vector3 _originalScale = Vector3.one;

    private void Start()
    {
        if (gameRulesManager == null)
            gameRulesManager = FindGameRulesManager();
        if (ballSpawner == null)
            ballSpawner = FindBallSpawner();

        if (gameRulesManager != null)
            gameRulesManager.RoundStarted += OnRoundStarted;
    }

    private static GameRulesManager FindGameRulesManager()
    {
#if UNITY_2022_2_OR_NEWER
        return FindFirstObjectByType<GameRulesManager>();
#else
        return FindObjectOfType<GameRulesManager>();
#endif
    }

    private static BallSpawner FindBallSpawner()
    {
#if UNITY_2022_2_OR_NEWER
        return FindFirstObjectByType<BallSpawner>();
#else
        return FindObjectOfType<BallSpawner>();
#endif
    }

    private void OnDestroy()
    {
        if (gameRulesManager != null)
            gameRulesManager.RoundStarted -= OnRoundStarted;
        StopCycle();
    }

    private void OnRoundStarted()
    {
        StopCycle();
        if (gameRulesManager?.ActiveModifier?.cycleBallVisibility == true)
            StartCoroutine(WaitForBallThenRunCycle());
    }

    private void Update()
    {
        // If we're not running a cycle but the modifier is active and we have a ball, start
        if (_cycleCoroutine == null && gameRulesManager?.ActiveModifier?.cycleBallVisibility == true)
        {
            GameObject ball = GetActiveBall();
            if (ball != null && ball != _trackedBall)
                _cycleCoroutine = StartCoroutine(RunVisibilityCycle(ball));
        }

        // If the active ball is gone (drained), stop and re-show it
        if (_trackedBall != null && GetActiveBall() != _trackedBall)
            StopCycle();
    }

    private IEnumerator WaitForBallThenRunCycle()
    {
        while (gameRulesManager != null && gameRulesManager.ActiveModifier != null &&
               gameRulesManager.ActiveModifier.cycleBallVisibility)
        {
            GameObject ball = GetActiveBall();
            if (ball != null)
            {
                _cycleCoroutine = StartCoroutine(RunVisibilityCycle(ball));
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator RunVisibilityCycle(GameObject ball)
    {
        _trackedBall = ball;
        var mod = gameRulesManager.ActiveModifier;
        if (mod == null || !mod.cycleBallVisibility)
        {
            SetBallVisible(ball, true);
            _trackedBall = null;
            _cycleCoroutine = null;
            yield break;
        }

        float interval = Mathf.Max(0.1f, mod.ballHideInterval);
        float duration = Mathf.Max(0.1f, mod.ballHideDuration);

        // Ensure ball starts visible, full opacity and scale (cache original scale for restore)
        CacheOriginalScale(ball);
        SetBallVisible(ball, true);
        SetBallAlpha(ball, 1f);
        SetBallScale(ball, 1f);

        while (ball != null && _trackedBall == ball && gameRulesManager != null &&
               gameRulesManager.ActiveModifier == mod && mod.cycleBallVisibility)
        {
            // Wait so ball is fully invisible at the interval mark (account for vanish duration)
            float visibleTime = Mathf.Max(0.1f, interval - vanishDuration);
            yield return new WaitForSeconds(visibleTime);

            if (ball == null || _trackedBall != ball) yield break;
            yield return VanishBall(ball);

            yield return new WaitForSeconds(duration);

            if (ball == null || _trackedBall != ball) yield break;
            yield return AppearBall(ball);
        }

        SetBallVisible(ball, true);
        SetBallAlpha(ball, 1f);
        SetBallScale(ball, 1f);
        _trackedBall = null;
        _cycleCoroutine = null;
    }

    private void StopCycle()
    {
        if (_trackedBall != null)
        {
            SetBallVisible(_trackedBall, true);
            SetBallAlpha(_trackedBall, 1f);
            SetBallScale(_trackedBall, 1f);
        }
        _trackedBall = null;
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
    }

    private IEnumerator VanishBall(GameObject ball)
    {
        if (ball == null) yield break;

        float elapsed = 0f;
        while (elapsed < vanishDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / vanishDuration);
            float curveT = vanishCurve.Evaluate(t);
            SetBallAlpha(ball, 1f - curveT);
            if (useScaleAnimation)
                SetBallScale(ball, Mathf.Lerp(1f, 0.001f, curveT));
            yield return null;
        }
        SetBallAlpha(ball, 0f);
        if (useScaleAnimation) SetBallScale(ball, 0.001f);
        SetBallVisible(ball, false);
    }

    private IEnumerator AppearBall(GameObject ball)
    {
        if (ball == null) yield break;

        SetBallVisible(ball, true);
        SetBallAlpha(ball, 0f);
        if (useScaleAnimation) SetBallScale(ball, 0.001f);
        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / appearDuration);
            float curveT = appearCurve.Evaluate(t);
            SetBallAlpha(ball, curveT);
            if (useScaleAnimation)
                SetBallScale(ball, Mathf.Lerp(0.001f, 1f, curveT));
            yield return null;
        }
        SetBallAlpha(ball, 1f);
        SetBallScale(ball, 1f);
    }

    private void SetBallVisible(GameObject ball, bool visible)
    {
        if (ball == null) return;
        foreach (var r in ball.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    private void CacheOriginalScale(GameObject ball)
    {
        if (ball == null) return;
        Transform visual = GetVisualOnlyChild(ball.transform);
        Transform target = visual != null ? visual : ball.transform;
        _originalScale = target.localScale;
        // If no visual child, we never scale (SetBallScale will no-op), so ball stays correct
    }

    private void SetBallScale(GameObject ball, float scale)
    {
        if (ball == null) return;
        Transform visual = GetVisualOnlyChild(ball.transform);
        // Only scale a visual-only child so the collider (on the root) stays full size. Never scale the root.
        if (visual == null) return;
        visual.localScale = new Vector3(_originalScale.x * scale, _originalScale.y * scale, _originalScale.z * scale);
    }

    /// <summary>
    /// Returns a child that has Renderers but no Collider/Rigidbody (safe to scale without affecting physics).
    /// Common names: "Visual", "Model", "Mesh". Returns null if none found — then we scale the root (use scale animation off).
    /// </summary>
    private static Transform GetVisualOnlyChild(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.GetComponent<Renderer>() != null || child.GetComponentInChildren<Renderer>() != null)
            {
                if (child.GetComponent<Collider>() == null && child.GetComponent<Rigidbody>() == null)
                    return child;
            }
        }
        return null;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void SetBallAlpha(GameObject ball, float alpha)
    {
        if (ball == null) return;
        foreach (var r in ball.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            var mat = r.material;
            if (mat.HasProperty(ColorId))
            {
                var c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
            if (mat.HasProperty(BaseColorId))
            {
                var c = mat.GetColor(BaseColorId);
                c.a = alpha;
                mat.SetColor(BaseColorId, c);
            }
        }
    }

    private GameObject GetActiveBall()
    {
        // Prefer GameRulesManager.ActiveBall — it resolves BallSpawner at round start,
        // so it's reliable even when the board loads after this controller.
        if (gameRulesManager != null && gameRulesManager.ActiveBall != null)
            return gameRulesManager.ActiveBall;
        if (ballSpawner != null && ballSpawner.ActiveBall != null)
            return ballSpawner.ActiveBall;
        return null;
    }
}
