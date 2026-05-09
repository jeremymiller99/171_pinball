using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ArtifactCard : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject artifactPrefab;
    [SerializeField] private ArtifactManager artifactManager;
    [SerializeField] private UIScript uiScript;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private float amplitudePixels;
    [SerializeField] private float frequencyHz;
    [SerializeField] private Vector2 cardBasePos;
    [SerializeField] private Quaternion cardBaseRot;
    [SerializeField] private Vector3 cardBaseScale;
    [Header("Mouse Hover Settings")]
    [SerializeField] private bool pointerOver;
    [SerializeField] private float maxTiltDegrees;
    [SerializeField] private float tiltSmoothing;
    [Header("Controller Hover Settings")]
    [SerializeField] private float maxScaleIncrease;
    [SerializeField] private float scaleSmoothing;
    [SerializeField] private bool isSelected;


    private void Awake()
    {
        uiScript = ServiceLocator.Get<UIScript>();
        artifactManager = ServiceLocator.Get<ArtifactManager>();
        parentCanvas = artifactManager.GetComponentInParent<Canvas>();
        cardRect = GetComponent<RectTransform>();
        cardBasePos = cardRect.anchoredPosition;
        cardBaseRot = cardRect.localRotation;
        cardBaseScale = cardRect.localScale;
    }
    public void Populate(ArtifactDefinition artifactDefinition)
    {
        nameText.text = artifactDefinition.DisplayName;
        descriptionText.text = artifactDefinition.Description;
        artifactPrefab = artifactDefinition.Prefab;
    }

    public void OnClick()
    {
        artifactManager.AddArtifactToPlay(artifactPrefab);
    }

    
    private void Update()
    {

        float t = Time.unscaledTime;

        Vector2 pos = cardBasePos;
        pos.y += Mathf.Sin(t * (Mathf.PI * 2f) * frequencyHz) * amplitudePixels;

        cardRect.anchoredPosition = pos;

        if (!uiScript.usingMouse && isSelected)
        {
            if (cardRect.localScale.x > cardBaseScale.x + maxScaleIncrease) {
                scaleSmoothing =  -Mathf.Abs(scaleSmoothing);
            } else if (cardRect.localScale.x < cardBaseScale.x - maxScaleIncrease) {
                scaleSmoothing = Mathf.Abs(scaleSmoothing);
            }
            cardRect.localScale += Vector3.one * scaleSmoothing * Time.unscaledDeltaTime;
        }

        if (!uiScript.usingMouse) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        float targetX = 0f;
        float targetY = 0f;
        cardRect.localScale = cardBaseScale;

        if (pointerOver && parentCanvas != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    cardRect,
                    pointer,
                    parentCanvas.worldCamera,
                    out Vector2 local))
            {
                Rect r = cardRect.rect;
                float nx = r.width > 0.01f ? Mathf.Clamp(local.x / (r.width * 0.5f), -1f, 1f) : 0f;
                float ny = r.height > 0.01f ? Mathf.Clamp(local.y / (r.height * 0.5f), -1f, 1f) : 0f;
                float max = maxTiltDegrees;
                targetX = ny * max;
                targetY = -nx * max;
            }
        }

        Quaternion targetRot = cardBaseRot * Quaternion.Euler(targetX, targetY, 0f);
        float smooth = tiltSmoothing;
        float lerp = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        cardRect.localRotation = Quaternion.Slerp(cardRect.localRotation, targetRot, lerp);

        if (pointerOver && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClick();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
    }

    public void OnSelect()
    {
        isSelected = true;
    }

    public void OnDeselect()
    {
        isSelected = false;
        cardRect.localScale = cardBaseScale;
    }

}
