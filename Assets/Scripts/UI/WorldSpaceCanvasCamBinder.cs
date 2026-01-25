using UnityEngine;

[DisallowMultipleComponent]
public class WorldSpaceCanvasCamBinder : MonoBehaviour
{
    Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    void OnEnable()
    {
        Bind();
    }

    void Bind()
    {
        // Camera.main finds the enabled camera tagged MainCamera.
        var cam = Camera.main;
        if (!cam) return;

        if (_canvas && _canvas.renderMode == RenderMode.WorldSpace)
            _canvas.worldCamera = cam;
    }
}