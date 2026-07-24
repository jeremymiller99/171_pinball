using UnityEngine;

/// <summary>
/// Drop this on a scene object (a radio, a speaker, a console) to run the looping radio
/// chatter ambience from that object's position for as long as it is enabled. The loop
/// itself is owned by the AudioManager, so it is torn down with every other loop.
/// </summary>
[DisallowMultipleComponent]
public sealed class RadioChatterEmitter : MonoBehaviour
{
    private bool _started;

    // Started in Start rather than OnEnable: the AudioManager registers itself in Awake,
    // so an OnEnable on the first frame can run before there is anything to call.
    private void Start()
    {
        _started = true;
        ServiceLocator.Get<AudioManager>()?.StartRadioChatter(gameObject);
    }

    private void OnEnable()
    {
        if (!_started) return;
        ServiceLocator.Get<AudioManager>()?.StartRadioChatter(gameObject);
    }

    private void OnDisable()
    {
        ServiceLocator.Get<AudioManager>()?.StopRadioChatter();
    }
}
