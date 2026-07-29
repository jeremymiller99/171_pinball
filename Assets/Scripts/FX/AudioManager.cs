using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using FMODUnity;
using FMOD.Studio;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    [Header("Music Settings")]
    [SerializeField] private EventReference mainMusicEvent;
    [Tooltip("Scenes treated as the main menu. Loading one of these returns the music " +
             "loop to its default variant so a run's modifier state doesn't follow the " +
             "player back out to the menu.")]
    [SerializeField] private string[] mainMenuSceneNames = { "MainMenu 1", "MainMenu" };

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
    [Tooltip("Plays over the whole ship-select animation. Restarted from the top if the " +
             "ship is changed again before it finishes.")]
    [SerializeField] private EventReference shipSelectSound;

    [Header("Gameplay Interactions")]
    [SerializeField] private EventReference bumperHitSound;
    [Tooltip("Hit sound for rollovers.")]
    [FormerlySerializedAs("multHitSound")]
    [SerializeField] private EventReference rolloverHitSound;
    [Tooltip("Hit sound for slings, which are a separate component type from bumpers. " +
             "Leave null to fall back to whatever event is assigned on the sling " +
             "prefab itself.")]
    [FormerlySerializedAs("kickerHitSound")]
    [SerializeField] private EventReference slingHitSound;
    [Tooltip("Per-type component hit sounds. Add a row to give a new component type " +
             "its own hit event with no code change. Bumper/Rollover/Sling above are " +
             "auto-seeded here for back-compat; a row for the same type overrides them.")]
    [SerializeField] private List<ComponentHitSound> componentHitSounds =
        new List<ComponentHitSound>();
    [SerializeField] private EventReference flipperUpSound;
    [SerializeField] private EventReference flipperDownSound;
    [SerializeField] private EventReference launchSound;
    [Tooltip("Plays as the hangar door slides open during the launch/start sequence.")]
    [SerializeField] private EventReference launchDoorSound;
    [SerializeField] private EventReference portalSound;
    [SerializeField] private EventReference ballLostSound;
    [SerializeField] private EventReference textWhooshSound;
    [SerializeField] private EventReference shatterSound;
    [SerializeField] private EventReference eggCrackSound;
    [SerializeField] private EventReference explosionSound;
    [SerializeField] private EventReference ballSplitSound;
    
    // Newly Added Gameplay Sounds
    [SerializeField] private EventReference fireworksSound;
    [SerializeField] private EventReference frenzyGateSound;
    [FormerlySerializedAs("dropTargetDownSound")]
    [SerializeField] private EventReference dropperDownSound;
    [FormerlySerializedAs("dropTargetUpSound")]
    [SerializeField] private EventReference dropperUpSound;
    [SerializeField] private EventReference frenzyActivatedSound;

    [Header("Scoring Sounds")]
    [SerializeField] private EventReference pointsAddEvent;
    [SerializeField] private EventReference multAddEvent;
    [SerializeField] private EventReference coinAddEvent;

    [Header("Continuous Sounds")]
    [Tooltip("Looping rolling sound to play based on speed.")]
    [SerializeField] private EventReference rollingSoundEvent;
    [Tooltip("Looping burning sound.")]
    [SerializeField] private EventReference burningSoundEvent;
    [Tooltip("Looping electric hum sound.")]
    [SerializeField] private EventReference hummingSoundEvent;
    [Tooltip("Looping computer whirr sound.")]
    [SerializeField] private EventReference whirringSoundEvent;
    [Tooltip("Looping emergency siren sound.")]
    [SerializeField] private EventReference sirenSoundEvent;
    [Tooltip("Looping radio chatter ambience (env_radio_chatter_loop).")]
    [SerializeField] private EventReference radioChatterSoundEvent;

    private EventInstance musicInstance;
    private EventInstance rollingSoundInstance;
    private EventInstance hummingSoundInstance;
    private EventInstance whirringSoundInstance;
    private EventInstance sirenSoundInstance;
    private EventInstance radioChatterInstance;
    private EventInstance alienShipRumbleInstance;
    private EventInstance shipSelectInstance;

    // Burning is per-emitter: every object on fire gets its own instance pinned to
    // its own transform, so two fires in different places are heard in both places.
    private readonly Dictionary<GameObject, EventInstance> burningSoundInstances =
        new Dictionary<GameObject, EventInstance>();
    private readonly List<GameObject> orphanedBurnSources = new List<GameObject>();

    private int alienType = 0;

    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus musicBus;
    private FMOD.Studio.Bus sfxBus;

    private void Awake()
    {
        var existing = ServiceLocator.Get<AudioManager>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Register<AudioManager>(this);
        InitializeBuses();
        ApplySavedVolumes();
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

    // Music is deliberately not started here: it begins when a board scene finishes
    // loading (see BoardLoader), so the menu stays quiet until the player is on a table.

    private void Update()
    {
        ReleaseOrphanedBurningSounds();
    }

    private const float DefaultVolume = 0.8f;

    // Default value of the music event's "modifier" parameter - the variant the loop
    // plays outside of any round modifier.
    private const float DefaultMusicState = 0f;

    private void InitializeBuses()
    {
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);
    }

    private void ApplySavedVolumes()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", DefaultVolume);
        float music = PlayerPrefs.GetFloat("MusicVolume", DefaultVolume);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", DefaultVolume);
        masterBus.setVolume(master);
        musicBus.setVolume(music);
        sfxBus.setVolume(sfx);
    }

    // Scene Load & Button Auto-Wiring

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Coming back out to the menu from a run: stop the music and let it fade out, so
        // the menu is quiet again (music only plays on a board). Hooked off the scene
        // load rather than the individual "quit to menu" call sites so every route back
        // to the menu is covered.
        if (IsMainMenuScene(scene))
        {
            StopMusic();
        }

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
    private bool IsMainMenuScene(Scene scene)
    {
        if (mainMenuSceneNames == null) return false;

        for (int i = 0; i < mainMenuSceneNames.Length; i++)
        {
            if (scene.name == mainMenuSceneNames[i])
            {
                return true;
            }
        }

        return false;
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

        /*
        I commented this out because I need a Select event on the "Enter Shop Button"
        GameObject in order for controller support to work :(
        - Drew
        */
        //trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.Select);
        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener((data) =>
        {
            ServiceLocator.Get<HapticManager>()?.PlayUINavigationHaptic();
        });
        trigger.triggers.Add(selectEntry);
    }

    // General Audio Methods

    /// <summary>
    /// Starts the main music loop for gameplay. Called when a board scene finishes
    /// loading. Safe to call on every board load: if the loop is already running it is
    /// left alone rather than restarted, so moving between boards doesn't retrigger it.
    /// </summary>
    public void StartBoardMusic()
    {
        if (mainMusicEvent.IsNull) return;

        if (musicInstance.isValid())
        {
            musicInstance.getPlaybackState(out PLAYBACK_STATE state);
            if (state == PLAYBACK_STATE.PLAYING)
            {
                return;
            }
        }

        StartMusic(mainMusicEvent);
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

        ResetMusicToDefault();
    }

    /// <summary>
    /// Returns the music loop to its default variant and clears any muffling, without
    /// restarting it. Used when the player leaves a run for the menu so the round's
    /// modifier state doesn't carry over.
    /// </summary>
    public void ResetMusicToDefault()
    {
        SetMusicState(DefaultMusicState);
        SetMusicMuffled(false);
        SetMusicLoss(0f);
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

    /// <summary>
    /// Sets the music "loss" parameter (0 = normal, 1 = loss). The parameter has a seek
    /// speed in FMOD, so this glides rather than snapping - set it and let it ease.
    /// </summary>
    public void SetMusicLoss(float value)
    {
        if (musicInstance.isValid())
        {
            musicInstance.setParameterByName("loss", value);
        }
    }

    /// <summary>
    /// Stops the music with a fade-out and releases it. Used when leaving a run for the
    /// menu; the next board load starts a fresh instance via StartBoardMusic.
    /// </summary>
    public void StopMusic()
    {
        if (!musicInstance.isValid()) return;

        musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicInstance.release();
        musicInstance.clearHandle();
    }

    // 3D Playback Core
    //
    // Every SFX gets 3D attributes. Sounds with a real world position use it; sounds
    // that are conceptually non-diegetic (UI, shop, scoring) are placed *at the listener*
    // rather than left at their default of Vector3.zero. That distinction matters: an
    // event authored as 3D in FMOD Studio and played at (0,0,0) is panned and attenuated
    // toward the world origin, which is why unpositioned UI clicks drift off-centre.
    // Anchoring them to the listener keeps them centred and at full volume even if the
    // event picks up a spatializer later.

    /// <summary>Current FMOD listener position, or the origin if the listener isn't up yet.</summary>
    private static Vector3 GetListenerPosition()
    {
        if (RuntimeManager.StudioSystem.isValid() &&
            RuntimeManager.StudioSystem.getListenerAttributes(0, out FMOD.ATTRIBUTES_3D attributes) == FMOD.RESULT.OK)
        {
            return new Vector3(attributes.position.x, attributes.position.y, attributes.position.z);
        }

        return Vector3.zero;
    }

    /// <summary>Non-diegetic one-shot: audible dead-centre regardless of camera.</summary>
    private void PlayOneShot2D(EventReference soundEvent)
    {
        if (soundEvent.IsNull) return;
        RuntimeManager.PlayOneShot(soundEvent, GetListenerPosition());
    }

    /// <summary>Positional one-shot fired at a fixed point in the world.</summary>
    private void PlayOneShot(EventReference soundEvent, Vector3 worldPosition)
    {
        if (soundEvent.IsNull) return;
        RuntimeManager.PlayOneShot(soundEvent, worldPosition);
    }

    /// <summary>
    /// Positional one-shot that tracks a moving object for its lifetime. Falls back to
    /// listener-centred playback when the source is gone, so a destroyed emitter can't
    /// drop the sound at the world origin.
    /// </summary>
    private void PlayOneShotAttached(EventReference soundEvent, GameObject source)
    {
        if (soundEvent.IsNull) return;

        if (source == null)
        {
            PlayOneShot2D(soundEvent);
            return;
        }

        RuntimeManager.PlayOneShotAttached(soundEvent, source);
    }

    private void PlayOneShotWithParameter(EventReference soundEvent, string paramName, float paramValue, Vector3 worldPosition)
    {
        if (soundEvent.IsNull) return;

        EventInstance instance = RuntimeManager.CreateInstance(soundEvent);
        instance.setParameterByName(paramName, paramValue);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(worldPosition));
        instance.start();
        instance.release();
    }

    private void PlayOneShotWithParameter2D(EventReference soundEvent, string paramName, float paramValue)
    {
        PlayOneShotWithParameter(soundEvent, paramName, paramValue, GetListenerPosition());
    }

    // Looping-instance helpers
    //
    // Looping events are created once and pinned to their emitter via
    // AttachInstanceToGameObject, which re-sends 3D attributes every frame so the sound
    // follows the object. Attaching also requires an explicit detach before release.

    /// <summary>Positions a loop: attached to <paramref name="source"/>, or at the listener if there isn't one.</summary>
    private static void AnchorInstance(EventInstance instance, GameObject source)
    {
        if (source != null)
        {
            RuntimeManager.AttachInstanceToGameObject(instance, source);
        }
        else
        {
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(GetListenerPosition()));
        }
    }

    /// <summary>Creates the loop if needed, (re)anchors it to the source, and starts it if idle.</summary>
    private static void StartLoop(ref EventInstance instance, EventReference soundEvent, GameObject source)
    {
        if (soundEvent.IsNull) return;

        if (!instance.isValid())
        {
            instance = RuntimeManager.CreateInstance(soundEvent);
        }

        // Re-anchor every call so a loop that outlives its original emitter
        // (a new ball, a second alien ship) tracks the current one.
        AnchorInstance(instance, source);

        instance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            instance.start();
        }
    }

    private static void StopLoop(ref EventInstance instance)
    {
        if (!instance.isValid()) return;

        RuntimeManager.DetachInstanceFromGameObject(instance);
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
        instance.clearHandle();
    }

    // Specific Gameplay & UI Functions

    // UI Interactions - deliberately non-diegetic, anchored to the listener.
    public void PlayButtonClick() => PlayOneShot2D(buttonClickSound);
    public void PlayButtonHover() => PlayOneShot2D(buttonHoverSound);
    public void PlayTutorialNext() => PlayOneShot2D(tutorialNextSound);
    public void PlayTabSwitch() => PlayOneShot2D(tabSwitchSound);
    public void PlayTransition() => PlayOneShot2D(transitionSound);

    // Aliens

    /// <summary>
    /// Plays the arrival sting for the given alien type at the ship's location.
    /// Pass the ship so the sound tracks it if it is still moving.
    /// </summary>
    public void PlayAlienArrival(int type, GameObject source = null)
    {
        alienType = type;
        if (alienType == 0)
        {
            PlayOneShotAttached(cuteAlienArrivalSound, source);
        }
        else if (alienType == 1)
        {
            PlayOneShotAttached(gruntAlienArrivalSound, source);
        }
        else if (alienType == 2)
        {
            PlayOneShotAttached(bugAlienArrivalSound, source);
        }
    }

    public void PlayAlienDeparture(GameObject source = null)
    {
        if (alienType == 0)
        {
            PlayOneShotAttached(cuteAlienDepartureSound, source);
        }
        else if (alienType == 1)
        {
            PlayOneShotAttached(gruntAlienDepartureSound, source);
        }
        else if (alienType == 2)
        {
            PlayOneShotAttached(bugAlienDepartureSound, source);
        }
    }

    /// <summary>Starts the ship rumble loop pinned to the ship, so it pans as the ship flies in and out.</summary>
    public void StartAlienShipRumble(GameObject source = null)
    {
        StartLoop(ref alienShipRumbleInstance, alienShipRumbleSound, source);
    }

    public void StopAlienShipRumble()
    {
        StopLoop(ref alienShipRumbleInstance);
    }

    // Shop & Meta - non-diegetic.
    public void PlayPurchase() => PlayOneShot2D(purchaseSound);
    public void PlayFailedPurchase() => PlayOneShot2D(failedPurchaseSound);
    public void PlayReroll() => PlayOneShot2D(rerollSound);
    public void PlaySwapSlot() => PlayOneShot2D(swapSlotSound);
    public void PlayLevelUp() => PlayOneShot2D(levelUpSound);

    /// <summary>
    /// Plays the ship-select sting for the whole selection animation. Held as an instance
    /// (not a one-shot) so re-selecting cuts the in-progress sting and restarts from the
    /// top instead of layering a second copy over it. Pass the animating object to have
    /// the sound track it; pass null to play it at the listener.
    /// </summary>
    public void PlayShipSelect(GameObject source = null)
    {
        if (shipSelectSound.IsNull) return;

        // Hard cut, not a fade: it's the same event each time, so an overlapping tail
        // would flam against the restart.
        StopShipSelect();

        shipSelectInstance = RuntimeManager.CreateInstance(shipSelectSound);
        AnchorInstance(shipSelectInstance, source);
        shipSelectInstance.start();
    }

    public void StopShipSelect()
    {
        if (!shipSelectInstance.isValid()) return;

        RuntimeManager.DetachInstanceFromGameObject(shipSelectInstance);
        shipSelectInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        shipSelectInstance.release();
        shipSelectInstance.clearHandle();
    }

    // Gameplay Board - all diegetic, emitted from where the event happens on the table.

    /// <summary>A component type paired with the hit event it should play.</summary>
    [System.Serializable]
    public struct ComponentHitSound
    {
        public BoardComponentType type;
        public EventReference hitSound;
    }

    private Dictionary<BoardComponentType, EventReference> _hitSoundLookup;

    // Built once, lazily. Legacy per-type fields are seeded first so existing
    // inspector wiring keeps working with no re-assignment; explicit list rows then
    // override them, and any new type is a one-row inspector addition.
    private bool TryGetComponentHitSound(
        BoardComponentType type, out EventReference soundEvent)
    {
        if (_hitSoundLookup == null)
        {
            _hitSoundLookup = new Dictionary<BoardComponentType, EventReference>();

            if (!bumperHitSound.IsNull)
                _hitSoundLookup[BoardComponentType.Bumper] = bumperHitSound;
            if (!rolloverHitSound.IsNull)
                _hitSoundLookup[BoardComponentType.Rollover] = rolloverHitSound;
            if (!slingHitSound.IsNull)
                _hitSoundLookup[BoardComponentType.Sling] = slingHitSound;

            if (componentHitSounds != null)
            {
                foreach (ComponentHitSound row in componentHitSounds)
                {
                    if (!row.hitSound.IsNull)
                        _hitSoundLookup[row.type] = row.hitSound;
                }
            }
        }

        return _hitSoundLookup.TryGetValue(type, out soundEvent) && !soundEvent.IsNull;
    }

    public void PlayBumperHit(Vector3 position, int variant=0)
    {
        PlayOneShotWithParameter(bumperHitSound, "collision_variant", variant, position);
    }

    public void PlayRolloverHit(Vector3 position, int variant=0)
    {
        PlayOneShotWithParameter(rolloverHitSound, "collision_variant", variant, position);
    }

    public void PlaySlingHit(Vector3 position, int variant=0)
    {
        PlayOneShotWithParameter(slingHitSound, "collision_variant", variant, position);
    }

    /// <summary>
    /// Plays a per-component hit event (the one assigned on the component's own prefab)
    /// at its position. The comp_*_param events pick their sound from "collision_variant",
    /// so the variant has to be set here - firing one of them without it lands on the
    /// parameter's default, which is silent if that slot has no audio behind it.
    /// Setting a parameter an event doesn't have is a harmless no-op in FMOD.
    /// </summary>
    public void PlayComponentHit(EventReference soundEvent, Vector3 position, int variant = 0)
    {
        PlayOneShotWithParameter(soundEvent, "collision_variant", variant, position);
    }

    /// <summary>
    /// Plays the shared hit sound owned by this manager for a board component type, so a
    /// type's sound lives in one place instead of being copied onto every prefab variant.
    /// Returns false for types that have no sound assigned yet, letting the caller fall
    /// back to whatever event is on the component itself. Give a type its sound by adding
    /// a row to <c>componentHitSounds</c> in the inspector — no code change needed.
    /// </summary>
    public bool TryPlayComponentHit(BoardComponentType type, Vector3 position, int variant = 0)
    {
        if (!TryGetComponentHitSound(type, out EventReference soundEvent))
        {
            return false;
        }

        PlayComponentHit(soundEvent, position, variant);
        return true;
    }

    public void PlayFlipperUp(Vector3 position) => PlayOneShot(flipperUpSound, position);
    public void PlayFlipperDown(Vector3 position) => PlayOneShot(flipperDownSound, position);
    public void PlayLaunch(Vector3 position) => PlayOneShot(launchSound, position);
    public void PlayLaunchDoor(Vector3 position) => PlayOneShot(launchDoorSound, position);
    public void PlayPortal(Vector3 position) => PlayOneShot(portalSound, position);
    public void PlayBallLost(Vector3 position) => PlayOneShot(ballLostSound, position);
    public void PlayShatter(Vector3 position) => PlayOneShot(shatterSound, position);
    public void PlayEggCrack(Vector3 position) => PlayOneShot(eggCrackSound, position);
    public void PlayExplosion(Vector3 position) => PlayOneShot(explosionSound, position);
    public void PlayBallSplit(Vector3 position) => PlayOneShot(ballSplitSound, position);
    public void PlayFireworks(Vector3 position) => PlayOneShot(fireworksSound, position);
    public void PlayFrenzyGate(Vector3 position) => PlayOneShot(frenzyGateSound, position);
    public void PlayDropperDown(Vector3 position) => PlayOneShot(dropperDownSound, position);
    public void PlayDropperUp(Vector3 position) => PlayOneShot(dropperUpSound, position);
    public void PlayFrenzyActivated(Vector3 position) => PlayOneShot(frenzyActivatedSound, position);

    // Text whoosh belongs to the score popup, so it plays wherever that popup lives.
    public void PlayWhoosh(Vector3 position) => PlayOneShot(textWhooshSound, position);

    // Scoring Logic - non-diegetic readout, kept centred so it never ducks behind the table.

    public void PlayPointsAdd(int compTriggered)
    {
        PlayOneShotWithParameter2D(pointsAddEvent, "compTriggered", compTriggered);
    }

    public void PlayMultAdd(int compTriggered)
    {
        PlayOneShotWithParameter2D(multAddEvent, "compTriggered", compTriggered);
    }

    public void PlayCoinAdd() => PlayOneShot2D(coinAddEvent);

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
            PlayOneShot2D(coinAddEvent);
            yield return new WaitForSeconds(0.025f);
        }
    }

    // Continuous Rolling Sound

    /// <summary>
    /// Starts (or retargets) the rolling loop on the active ball. Call again with a new
    /// ball when the active ball changes - the loop follows whichever ball is passed last.
    /// Passing the ball's Rigidbody also feeds FMOD its velocity for doppler.
    /// </summary>
    public void StartRollingSound(GameObject ball = null, Rigidbody ballBody = null)
    {
        if (rollingSoundEvent.IsNull) return;

        if (!rollingSoundInstance.isValid())
        {
            rollingSoundInstance = RuntimeManager.CreateInstance(rollingSoundEvent);
        }

        if (ball != null && ballBody != null)
        {
            RuntimeManager.AttachInstanceToGameObject(rollingSoundInstance, ball, ballBody);
        }
        else
        {
            AnchorInstance(rollingSoundInstance, ball);
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
        StopLoop(ref rollingSoundInstance);
    }

    // Continuous Burning Sound
    //
    // One instance per burning object rather than a shared refcounted loop, so several
    // fires on different parts of the table are each heard from their own position.

    /// <summary>Starts a burning loop pinned to <paramref name="source"/>. Repeat calls for the same source are ignored.</summary>
    public void StartBurningSound(GameObject source)
    {
        if (burningSoundEvent.IsNull || source == null) return;

        if (burningSoundInstances.TryGetValue(source, out EventInstance existing) && existing.isValid())
        {
            existing.getPlaybackState(out PLAYBACK_STATE existingState);
            if (existingState == PLAYBACK_STATE.PLAYING)
            {
                return;
            }
        }

        EventInstance instance = RuntimeManager.CreateInstance(burningSoundEvent);
        RuntimeManager.AttachInstanceToGameObject(instance, source);
        instance.start();
        burningSoundInstances[source] = instance;
    }

    /// <summary>Stops the burning loop for one object. Other fires keep playing.</summary>
    public void StopBurningSound(GameObject source)
    {
        if (source == null) return;

        if (burningSoundInstances.TryGetValue(source, out EventInstance instance))
        {
            StopLoop(ref instance);
            burningSoundInstances.Remove(source);
        }
    }

    /// <summary>
    /// Reaps burning loops whose emitter was destroyed mid-burn (ball drained, target
    /// removed) without a matching StopBurningSound. FMOD only drops such instances from
    /// its attach list - the loop itself would otherwise keep playing at its last
    /// position - so they have to be stopped and released here.
    /// </summary>
    private void ReleaseOrphanedBurningSounds()
    {
        if (burningSoundInstances.Count == 0) return;

        orphanedBurnSources.Clear();
        foreach (var pair in burningSoundInstances)
        {
            if (pair.Key == null)
            {
                orphanedBurnSources.Add(pair.Key);
            }
        }

        foreach (GameObject source in orphanedBurnSources)
        {
            EventInstance instance = burningSoundInstances[source];
            StopLoop(ref instance);
            burningSoundInstances.Remove(source);
        }

        orphanedBurnSources.Clear();
    }

    /// <summary>Stops every burning loop at once (board-wide fire ending, teardown).</summary>
    public void StopAllBurningSounds()
    {
        foreach (var pair in burningSoundInstances)
        {
            EventInstance instance = pair.Value;
            StopLoop(ref instance);
        }

        burningSoundInstances.Clear();
    }

    // Continuous Humming Sound
    public void StartHummingSound(GameObject source = null)
    {
        StartLoop(ref hummingSoundInstance, hummingSoundEvent, source);
    }

    public void StopHummingSound()
    {
        StopLoop(ref hummingSoundInstance);
    }

    // Continuous PC Whirring Sound
    public void StartWhirringSound(GameObject source = null)
    {
        StartLoop(ref whirringSoundInstance, whirringSoundEvent, source);
    }

    public void StopWhirringSound()
    {
        StopLoop(ref whirringSoundInstance);
    }

    // Continuous Emergency Siren Sound
    public void StartSirenSound(GameObject source = null)
    {
        StartLoop(ref sirenSoundInstance, sirenSoundEvent, source);
    }

    public void StopSirenSound()
    {
        StopLoop(ref sirenSoundInstance);
    }

    // Continuous Radio Chatter Ambience
    public void StartRadioChatter(GameObject source = null)
    {
        StartLoop(ref radioChatterInstance, radioChatterSoundEvent, source);
    }

    public void StopRadioChatter()
    {
        StopLoop(ref radioChatterInstance);
    }

    // Volume Controls 

    public void SetMasterVolume(float volume) => masterBus.setVolume(volume);
    public void SetMusicVolume(float volume) => musicBus.setVolume(volume);
    public void SetSFXVolume(float volume) => sfxBus.setVolume(volume);

    private void OnDestroy()
    {
        if (ServiceLocator.Get<AudioManager>() == this)
        {
            ServiceLocator.Unregister<AudioManager>();

            if (musicInstance.isValid())
            {
                musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                musicInstance.release();
            }

            StopRollingSound();
            StopAlienShipRumble();
            StopHummingSound();
            StopWhirringSound();
            StopSirenSound();
            StopRadioChatter();
            StopShipSelect();
            StopAllBurningSounds();
        }
    }
}