using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 100f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 1f;

    private TMP_Text text;
    private RectTransform rectTransform;
    private Color startColor;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
        startColor = text.color;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        rectTransform.anchoredPosition += Vector2.up * moveSpeed * Time.deltaTime;
        
        startColor.a -= fadeSpeed * Time.deltaTime;
        text.color = startColor;
    }

    public void SetText(string value)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = value;
    }
}

