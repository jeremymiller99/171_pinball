using UnityEngine;

public class ButtonSound : MonoBehaviour
{
    public void PlaySound()
    {
        FMODUnity.RuntimeManager.PlayOneShot("event:/button_click");
    }
}
