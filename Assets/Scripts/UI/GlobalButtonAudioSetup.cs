using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GlobalButtonAudioSetup : MonoBehaviour
{
    public void HookupAllButtons()
    {
        if (AudioManager.Instance == null) return;
        
        ButtonSound globalSound = AudioManager.Instance.GetComponent<ButtonSound>();
        if (globalSound == null)
        {
            Debug.LogWarning("No ButtonSound attached to the AudioManager!");
            return;
        }

        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button btn in allButtons)
        {
            btn.onClick.RemoveListener(globalSound.PlaySound);
            btn.onClick.AddListener(globalSound.PlaySound);

            EventTrigger trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = btn.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerEnter);

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
            hoverEntry.eventID = EventTriggerType.PointerEnter;
            hoverEntry.callback.AddListener((data) => { globalSound.PlayHoverSound(); });
            
            trigger.triggers.Add(hoverEntry);
        }
    }
}