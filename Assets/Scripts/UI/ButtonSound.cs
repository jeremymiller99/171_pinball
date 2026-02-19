using UnityEngine;
using FMODUnity;

public class ButtonSound : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("The FMOD event to play when this button is clicked.")]
    public EventReference clickSound;

    public void PlaySound()
    {
        // Make sure we actually assigned a sound in the Inspector
        if (!clickSound.IsNull)
        {
            // Route the sound through our persistent manager
            AudioManager.Instance.PlayOneShot(clickSound);
        }
        else
        {
            Debug.LogWarning("ButtonSound: No click sound assigned on " + gameObject.name);
        }
    }
}