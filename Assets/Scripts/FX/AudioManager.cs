using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private EventReference mainMusicEvent;
    
    private EventInstance musicInstance;

    private void Awake()
    {
        // singleton moment
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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

    private void OnDestroy()
    {
        if (Instance == this && musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }
    }
}