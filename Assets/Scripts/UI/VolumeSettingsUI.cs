using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsUI : MonoBehaviour
{
    private const float DefaultVolume = 0.8f;

    [Header("UI Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", DefaultVolume);
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", DefaultVolume);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", DefaultVolume);

        if (masterSlider != null) masterSlider.value = savedMaster;
        if (musicSlider != null) musicSlider.value = savedMusic;
        if (sfxSlider != null) sfxSlider.value = savedSFX;

        ApplyMasterVolume(savedMaster);
        ApplyMusicVolume(savedMusic);
        ApplySFXVolume(savedSFX);

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(ApplyMasterVolume);
        
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(ApplyMusicVolume);
        
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(ApplySFXVolume);
    }

    private void ApplyMasterVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
    }

    private void ApplyMusicVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
    }

    private void ApplySFXVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(ApplyMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(ApplyMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(ApplySFXVolume);
    }
}