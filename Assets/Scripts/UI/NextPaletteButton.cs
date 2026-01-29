using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Very small helper: put this on a Unity UI Button to advance the board palette.
/// Works when the palette switcher lives in a different additively-loaded scene (ex: Board_Alpha).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class NextPaletteButton : MonoBehaviour
{
    [Tooltip("Optional. If not set, we auto-find it in loaded scenes.")]
    [SerializeField] private BoardAlphaPaletteSwitcher paletteSwitcher;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        _button.onClick.AddListener(HandleClick);

        // Resolve once on enable (scenes may be loaded additively).
        if (paletteSwitcher == null)
            paletteSwitcher = FindFirstObjectByType<BoardAlphaPaletteSwitcher>();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (paletteSwitcher == null)
            paletteSwitcher = FindFirstObjectByType<BoardAlphaPaletteSwitcher>();

        if (paletteSwitcher == null)
        {
            Debug.LogWarning($"{nameof(NextPaletteButton)}: Could not find {nameof(BoardAlphaPaletteSwitcher)} in loaded scenes.");
            return;
        }

        // Wraps automatically (0..PaletteCount-1..0)
        paletteSwitcher.NextPalette();
    }
}

