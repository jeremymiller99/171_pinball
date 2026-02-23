using UnityEngine;
using FMODUnity;

public class ButtonSound : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("The FMOD event to play when this button is clicked.")]
    public EventReference clickSound;
    
    [Tooltip("The FMOD event to play when this button is hovered.")]
    public EventReference hoverSound;

    public void PlaySound()
    {
        if (!clickSound.IsNull)
        {
            AudioManager.Instance.PlayOneShot(clickSound);
        }
        else
        {
            Debug.LogWarning("ButtonSound: No click sound assigned on " + gameObject.name);
        }
    }

    public void PlayHoverSound()
    {
        if (!hoverSound.IsNull)
        {
            AudioManager.Instance.PlayOneShot(hoverSound);
        }
    }
}