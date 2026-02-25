using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsUI : MonoBehaviour
{
    [Header("UI Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1f);

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
        AudioManager.Instance.SetMasterVolume(value);
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void ApplyMusicVolume(float value)
    {
        AudioManager.Instance.SetMusicVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    private void ApplySFXVolume(float value)
    {
        AudioManager.Instance.SetSFXVolume(value);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    private void OnDestroy()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(ApplyMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(ApplyMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(ApplySFXVolume);
    }
}