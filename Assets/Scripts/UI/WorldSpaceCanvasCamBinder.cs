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
        // In additive setups this can be null if the camera isn't tagged or is temporarily disabled.
        var cam = Camera.main;
#if UNITY_2022_2_OR_NEWER
        if (!cam)
        {
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams != null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                    {
                        cam = cams[i];
                        break;
                    }
                }
            }
        }
#else
        if (!cam)
        {
            var cams = FindObjectsOfType<Camera>(includeInactive: false);
            if (cams != null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                    {
                        cam = cams[i];
                        break;
                    }
                }
            }
        }
#endif
        if (!cam) return;

        if (_canvas && _canvas.renderMode == RenderMode.WorldSpace)
            _canvas.worldCamera = cam;
    }
}