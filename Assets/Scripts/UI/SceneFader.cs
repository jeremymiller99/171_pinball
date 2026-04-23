using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SceneFader : MonoBehaviour
{
    public const float DefaultFadeOutDuration = 0.45f;
    public const float DefaultFadeInDuration = 0.45f;

    private static SceneFader _instance;

    public static SceneFader Instance
    {
        get
        {
            if (_instance == null) Bootstrap();
            return _instance;
        }
    }

    private Canvas _canvas;
    private CanvasGroup _group;
    private Image _image;
    private Coroutine _routine;
    private bool _pendingFadeIn;
    private float _pendingFadeInDuration = DefaultFadeInDuration;
    private bool _holdingBlack;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("~SceneFader");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneFader>();
        _instance.BuildOverlay();
    }

    private void BuildOverlay()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var imageGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(transform, false);

        var rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _image = imageGo.GetComponent<Image>();
        _image.color = Color.black;
        _image.raycastTarget = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (!_pendingFadeIn) return;
        if (_holdingBlack) return;

        _pendingFadeIn = false;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine(1f, 0f, _pendingFadeInDuration, releaseBlockerAtEnd: true));
    }

    public void FadeAndLoadScene(string sceneName)
    {
        FadeAndLoadScene(sceneName, DefaultFadeOutDuration, DefaultFadeInDuration, holdBlackUntilReady: false);
    }

    public void FadeAndLoadScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        FadeAndLoadScene(sceneName, fadeOutDuration, fadeInDuration, holdBlackUntilReady: false);
    }

    /// <summary>
    /// If <paramref name="holdBlackUntilReady"/> is true, the screen stays fully black after
    /// the new scene loads. The caller must invoke <see cref="FadeIn"/> once the scene is ready
    /// (e.g., after additive sub-scenes have finished loading).
    /// </summary>
    public void FadeAndLoadScene(string sceneName, float fadeOutDuration, float fadeInDuration, bool holdBlackUntilReady)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeOutThenLoad(sceneName, fadeOutDuration, fadeInDuration, holdBlackUntilReady));
    }

    public void FadeIn()
    {
        FadeIn(_pendingFadeInDuration);
    }

    public void FadeIn(float duration)
    {
        _holdingBlack = false;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine(_group.alpha, 0f, duration, releaseBlockerAtEnd: true));
    }

    private IEnumerator FadeOutThenLoad(string sceneName, float fadeOutDuration, float fadeInDuration, bool holdBlackUntilReady)
    {
        _group.blocksRaycasts = true;
        yield return FadeRoutine(_group.alpha, 1f, fadeOutDuration, releaseBlockerAtEnd: false);

        _pendingFadeIn = true;
        _pendingFadeInDuration = fadeInDuration;
        _holdingBlack = holdBlackUntilReady;
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool releaseBlockerAtEnd)
    {
        if (duration <= 0f)
        {
            _group.alpha = to;
        }
        else
        {
            float elapsed = 0f;
            _group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            _group.alpha = to;
        }

        if (releaseBlockerAtEnd && Mathf.Approximately(to, 0f))
        {
            _group.blocksRaycasts = false;
        }

        _routine = null;
    }
}
