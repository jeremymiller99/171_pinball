using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private EventReference mainMusicEvent;
    [Header("Volume Buses")]
    // Make sure these string paths match exactly what you named your buses in FMOD Studio!
    [SerializeField] private string masterBusPath = "bus:/";
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";
    
    private EventInstance musicInstance;
    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus musicBus;
    private FMOD.Studio.Bus sfxBus;

    private void Awake()
    {
        // singleton moment
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeBuses();
        DontDestroyOnLoad(gameObject);
    }

    private void InitializeBuses()
    {
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);
    }

    private void Start()
    {
        if (!mainMusicEvent.IsNull)
        {
            StartMusic(mainMusicEvent);
        }
    }


    public void StartMusic(EventReference musicEvent)
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }

        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        musicInstance.start();
    }


    public void PlayOneShot(EventReference soundEvent, Vector3 worldPosition = default)
    {
        if (!soundEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(soundEvent, worldPosition);
        }
    }

    public void PlayOneShotWithParameter(EventReference soundEvent, string paramName, float paramValue, Vector3 worldPosition = default)
    {
        if (!soundEvent.IsNull)
        {
            EventInstance instance = RuntimeManager.CreateInstance(soundEvent);
            
            instance.setParameterByName(paramName, paramValue);
            
            if (worldPosition != default)
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(worldPosition));
            }
            
            instance.start();
            instance.release();
        }
    }

    public void SetMasterVolume(float volume)
    {
        masterBus.setVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        musicBus.setVolume(volume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxBus.setVolume(volume);
    }

    private void OnDestroy()
    {
        if (Instance == this && musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }
    }
}