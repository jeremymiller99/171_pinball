using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private EventReference mainMusicEvent;

    [Header("Volume Buses")]
    [SerializeField] private string masterBusPath = "bus:/";
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Header("UI Sounds")]
    [SerializeField] private EventReference buttonClickSound;
    [SerializeField] private EventReference buttonHoverSound;
    [SerializeField] private EventReference tutorialNextSound;
    [SerializeField] private EventReference tabSwitchSound;
    [SerializeField] private EventReference transitionSound;

    [Header("Alien Sounds")]
    [SerializeField] private EventReference cuteAlienArrivalSound;
    [SerializeField] private EventReference cuteAlienDepartureSound;
    [SerializeField] private EventReference bugAlienArrivalSound;
    [SerializeField] private EventReference bugAlienDepartureSound;
    [SerializeField] private EventReference gruntAlienArrivalSound;
    [SerializeField] private EventReference gruntAlienDepartureSound;
    [SerializeField] private EventReference alienShipRumbleSound;

    [Header("Shop & Meta Sounds")]
    [SerializeField] private EventReference purchaseSound;
    [SerializeField] private EventReference failedPurchaseSound;
    [SerializeField] private EventReference rerollSound;
    [SerializeField] private EventReference swapSlotSound;
    [SerializeField] private EventReference levelUpSound;

    [Header("Gameplay Interactions")]
    [SerializeField] private EventReference bumperHitSound;
    [SerializeField] private EventReference multHitSound;
    [SerializeField] private EventReference flipperUpSound;
    [SerializeField] private EventReference flipperDownSound;
    [SerializeField] private EventReference launchSound;
    [SerializeField] private EventReference portalSound;
    [SerializeField] private EventReference ballLostSound;
    [SerializeField] private EventReference textWhooshSound;
    [SerializeField] private EventReference shatterSound;
    [SerializeField] private EventReference eggCrackSound;
    [SerializeField] private EventReference explosionSound;
    [SerializeField] private EventReference ballSplitSound;

    [Header("Scoring Sounds")]
    [SerializeField] private EventReference pointsAddEvent;
    [SerializeField] private EventReference multAddEvent;
    [SerializeField] private EventReference coinAddEvent;

    [Header("Continuous Sounds")]
    [Tooltip("Looping rolling sound to play based on speed.")]
    [SerializeField] private EventReference rollingSoundEvent;
    [Tooltip("Looping burning sound.")]
    [SerializeField] private EventReference burningSoundEvent;

    private EventInstance musicInstance;
    private EventInstance rollingSoundInstance;
    private EventInstance alienShipRumbleInstance;
    private EventInstance burningSoundInstance;
    
    private int alienType = 0;
    private int activeBurnCount = 0;
    
    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus musicBus;
    private FMOD.Studio.Bus sfxBus;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeBuses();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (!mainMusicEvent.IsNull)
        {
            StartMusic(mainMusicEvent);
        }
    }

    private void InitializeBuses()
    {
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);
    }

    // Scene Load & Button Auto-Wiring

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find all buttons in the scene, including inactive ones
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();

        foreach (Button btn in allButtons)
        {
            // Make sure the button belongs to the loaded scene (prevents wiring project prefabs)
            if (btn.gameObject.scene.IsValid() && btn.gameObject.scene == scene)
            {
                WireButtonAudio(btn);
            }
        }
    }
    public void WireButtonAudio(Button btn)
    {
        // Wire the click sound (preventing duplicates)
        btn.onClick.RemoveListener(PlayButtonClick);
        btn.onClick.AddListener(PlayButtonClick);

        // Wire the hover sound using EventTrigger
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = btn.gameObject.AddComponent<EventTrigger>();
        }

        // Clean up previous hover triggers we might have created to avoid stacking
        trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerEnter);

        EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
        hoverEntry.eventID = EventTriggerType.PointerEnter;
        hoverEntry.callback.AddListener((data) => { PlayButtonHover(); });
        trigger.triggers.Add(hoverEntry);
    }

    // General Audio Methods

    public void StartMusic(EventReference musicEvent)
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }

        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        musicInstance.start();
        
        SetMusicState(0f);
        SetMusicMuffled(false);
    }

    public void SetMusicState(float stateValue)
    {
        if (musicInstance.isValid())
        {
            musicInstance.setParameterByName("modifier", stateValue);
        }
    }

    public void SetMusicMuffled(bool isMuffled)
    {
        if (musicInstance.isValid())
        {
            musicInstance.setParameterByName("muffled", isMuffled ? 1f : 0f);
        }
    }

    private void PlayOneShot(EventReference soundEvent, Vector3 worldPosition = default)
    {
        if (!soundEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(soundEvent, worldPosition);
        }
    }

    private void PlayOneShotWithParameter(EventReference soundEvent, string paramName, float paramValue, Vector3 worldPosition = default)
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

    // Specific Gameplay & UI Functions

    // UI Interactions
    public void PlayButtonClick() => PlayOneShot(buttonClickSound);
    public void PlayButtonHover() => PlayOneShot(buttonHoverSound);
    public void PlayTutorialNext() => PlayOneShot(tutorialNextSound);
    public void PlayTabSwitch() => PlayOneShot(tabSwitchSound);
    public void PlayTransition() => PlayOneShot(transitionSound);

    // Aliens

    public void PlayAlienArrival(int type)
    {
        alienType = type;
        if (alienType == 0)
        {
            PlayOneShot(cuteAlienArrivalSound);
        }
        else if (alienType == 1)
        {
            PlayOneShot(gruntAlienArrivalSound);
        }
        else if (alienType == 2)
        {
            PlayOneShot(bugAlienArrivalSound);
        }
    }

    public void PlayAlienDeparture()
    {
        if (alienType == 0)
        {
            PlayOneShot(cuteAlienDepartureSound);
        }
        else if (alienType == 1)
        {
            PlayOneShot(gruntAlienDepartureSound);
        }
        else if (alienType == 2)
        {
            PlayOneShot(bugAlienDepartureSound);
        }
    }

    public void StartAlienShipRumble()
    {
        if (alienShipRumbleSound.IsNull) return;

        if (!alienShipRumbleInstance.isValid())
        {
            alienShipRumbleInstance = RuntimeManager.CreateInstance(alienShipRumbleSound);
        }
        
        alienShipRumbleInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            alienShipRumbleInstance.start();
        }
    }

    public void StopAlienShipRumble()
    {
        if (alienShipRumbleInstance.isValid())
        {
            alienShipRumbleInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            alienShipRumbleInstance.release();
            alienShipRumbleInstance.clearHandle();
        }
    }

    // Shop & Meta
    public void PlayPurchase() => PlayOneShot(purchaseSound);
    public void PlayFailedPurchase() => PlayOneShot(failedPurchaseSound);
    public void PlayReroll() => PlayOneShot(rerollSound);
    public void PlaySwapSlot() => PlayOneShot(swapSlotSound);
    public void PlayLevelUp() => PlayOneShot(levelUpSound);

    // Gameplay Board
    public void PlayBumperHit(Vector3 position, int variant=0)
    {
        PlayOneShotWithParameter(bumperHitSound, "collision_variant", variant);
    }

    public void PlayMultHit(Vector3 position, int variant=0)
    {
        PlayOneShotWithParameter(multHitSound, "collision_variant", variant);
    }
    public void PlayFlipperUp(Vector3 position) => PlayOneShot(flipperUpSound, position);
    public void PlayFlipperDown(Vector3 position) => PlayOneShot(flipperDownSound, position);
    public void PlayLaunch(Vector3 position) => PlayOneShot(launchSound, position);
    public void PlayPortal(Vector3 position) => PlayOneShot(portalSound, position);
    public void PlayBallLost() => PlayOneShot(ballLostSound);
    public void PlayWhoosh() => PlayOneShot(textWhooshSound);

    // Uncategorized Sounds
    public void PlayShatter(Vector3 position = default) => PlayOneShot(shatterSound, position);
    public void PlayEggCrack(Vector3 position = default) => PlayOneShot(eggCrackSound, position);
    public void PlayExplosion(Vector3 position = default) => PlayOneShot(explosionSound, position);
    public void PlayBallSplit(Vector3 position = default) => PlayOneShot(ballSplitSound, position);

    // Scoring Logic

    public void PlayPointsAdd(int compTriggered)
    {
        PlayOneShotWithParameter(pointsAddEvent, "compTriggered", compTriggered);
    }

    public void PlayMultAdd(int compTriggered)
    {
        PlayOneShotWithParameter(multAddEvent, "compTriggered", compTriggered);
    }

    public void PlayStaggeredCoinSounds(int amount)
    {
        if (amount > 0)
        {
            StartCoroutine(PlayCoinSoundsRoutine(amount));
        }
    }

    private IEnumerator PlayCoinSoundsRoutine(int amount)
    {
        if (coinAddEvent.IsNull) yield break;

        for (int i = 0; i < amount; i++)
        {
            PlayOneShot(coinAddEvent);
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Continuous Rolling Sound 

    public void StartRollingSound()
    {
        if (rollingSoundEvent.IsNull) return;

        if (!rollingSoundInstance.isValid())
        {
            rollingSoundInstance = RuntimeManager.CreateInstance(rollingSoundEvent);
        }
        
        // Only start if it isn't already playing
        rollingSoundInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            rollingSoundInstance.start();
        }
    }

    public void UpdateRollingSound(float velocityNormalized)
    {
        if (rollingSoundInstance.isValid())
        {
            rollingSoundInstance.setParameterByName("velocity", velocityNormalized);
        }
    }

    public void StopRollingSound()
    {
        if (rollingSoundInstance.isValid())
        {
            rollingSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            rollingSoundInstance.release();
            rollingSoundInstance.clearHandle();
        }
    }

    // Continuous Burning Sound
    public void StartBurningSound()
    {
        if (burningSoundEvent.IsNull) return;

        activeBurnCount++; // Keep track of how many items are burning

        if (!burningSoundInstance.isValid())
        {
            burningSoundInstance = RuntimeManager.CreateInstance(burningSoundEvent);
        }

        burningSoundInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            burningSoundInstance.start();
        }
    }

    public void StopBurningSound()
    {
        activeBurnCount--; 

        // Only actually stop the sound if nothing else is still burning
        if (activeBurnCount <= 0)
        {
            activeBurnCount = 0; // Prevent negative values just in case
            if (burningSoundInstance.isValid())
            {
                burningSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                burningSoundInstance.release();
                burningSoundInstance.clearHandle();
            }
        }
    }

    // Volume Controls 

    public void SetMasterVolume(float volume) => masterBus.setVolume(volume);
    public void SetMusicVolume(float volume) => musicBus.setVolume(volume);
    public void SetSFXVolume(float volume) => sfxBus.setVolume(volume);

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (musicInstance.isValid())
            {
                musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                musicInstance.release();
            }

            StopRollingSound();
            StopAlienShipRumble();

            // Clear burn state and stop sound if the manager gets destroyed
            activeBurnCount = 0; 
            StopBurningSound(); 
        }
    }
}