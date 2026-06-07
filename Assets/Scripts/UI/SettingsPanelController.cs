using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Audio;

namespace TR.UI
{
    
    
    
    public class SettingsPanelController : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Toggle musicMuteToggle;
        [Space]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle sfxMuteToggle;
        [Space]
        [SerializeField] private Toggle damageNumbersToggle;
        [SerializeField] private TMP_Dropdown vfxQualityDropdown; 
        [SerializeField] private TMP_Dropdown fpsCapDropdown;    
        [SerializeField] private Toggle vfxEnableToggle;         
        [Header("Keybinds")]
        [SerializeField] private TMP_Dropdown pauseHotkeyDropdown; 

        private const string PREF_MUSIC_VOL = "tr_music_volume";
        private const string PREF_MUSIC_MUTE = "tr_music_mute";
        private const string PREF_SFX_VOL = "tr_sfx_volume";
        private const string PREF_SFX_MUTE = "tr_sfx_mute";
        private const string PREF_SHOW_DAMAGE_NUMBERS = "tr_show_damage_numbers";
        private const string PREF_VFX_QUALITY = "tr_vfx_quality"; 
        private const string PREF_VFX_ENABLE = "tr_vfx_enable";   
        private const string PREF_FPS_CAP = "tr_fps_cap"; 
        private const string PREF_PAUSE_HOTKEY = "tr_pause_hotkey"; 
        private bool _suppressEvents;

        private void Awake()
        {
            float vol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 1f);
            int mute = PlayerPrefs.GetInt(PREF_MUSIC_MUTE, 0);
            float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1f);
            int sfxMute = PlayerPrefs.GetInt(PREF_SFX_MUTE, 0);
            _suppressEvents = true;
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f; musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.value = vol;
            }
            if (musicMuteToggle != null)
            {
                musicMuteToggle.isOn = mute != 0;
            }
            ApplyVolume(vol, mute != 0);
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f; sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.value = sfxVol;
            }
            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.isOn = sfxMute != 0;
            }
            ApplySfxVolume(sfxVol, sfxMute != 0);
            
            if (damageNumbersToggle != null)
            {
                bool show = PlayerPrefs.GetInt(PREF_SHOW_DAMAGE_NUMBERS, 1) != 0;
                damageNumbersToggle.isOn = show;
                TR.UI.DamageNumbers.SetEnabled(show);
            }
            
            bool vfxEnabled = PlayerPrefs.GetInt(PREF_VFX_ENABLE, 1) != 0;
            if (vfxEnableToggle != null) vfxEnableToggle.isOn = vfxEnabled;
            if (vfxQualityDropdown != null)
            {
                int qual = Mathf.Clamp(PlayerPrefs.GetInt(PREF_VFX_QUALITY, 3), 0, 3);
                vfxQualityDropdown.value = qual;
            }
            ApplyVfxEnableAndQuality(vfxEnabled, vfxQualityDropdown != null ? vfxQualityDropdown.value : 3);
            
            if (fpsCapDropdown != null)
            {
                int cap = Mathf.Clamp(PlayerPrefs.GetInt(PREF_FPS_CAP, 3), 0, 3);
                fpsCapDropdown.value = cap;
                ApplyFpsCap(cap);
            }
            
            if (pauseHotkeyDropdown != null)
            {
                int hk = Mathf.Clamp(PlayerPrefs.GetInt(PREF_PAUSE_HOTKEY, 0), 0, 3);
                pauseHotkeyDropdown.value = hk;
            }
            _suppressEvents = false;

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnSliderChanged);
            if (musicMuteToggle != null)
                musicMuteToggle.onValueChanged.AddListener(OnMuteChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            if (sfxMuteToggle != null)
                sfxMuteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
            if (damageNumbersToggle != null)
                damageNumbersToggle.onValueChanged.AddListener(OnDamageNumbersChanged);
            if (vfxQualityDropdown != null)
                vfxQualityDropdown.onValueChanged.AddListener(OnVfxQualityChanged);
            if (vfxEnableToggle != null)
                vfxEnableToggle.onValueChanged.AddListener(OnVfxEnableChanged);
            if (fpsCapDropdown != null)
                fpsCapDropdown.onValueChanged.AddListener(OnFpsCapChanged);
            if (pauseHotkeyDropdown != null)
                pauseHotkeyDropdown.onValueChanged.AddListener(OnPauseHotkeyChanged);
        }

        private void OnDestroy()
        {
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
            if (musicMuteToggle != null)
                musicMuteToggle.onValueChanged.RemoveListener(OnMuteChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            if (sfxMuteToggle != null)
                sfxMuteToggle.onValueChanged.RemoveListener(OnSfxMuteChanged);
            if (damageNumbersToggle != null)
                damageNumbersToggle.onValueChanged.RemoveListener(OnDamageNumbersChanged);
            if (vfxQualityDropdown != null)
                vfxQualityDropdown.onValueChanged.RemoveListener(OnVfxQualityChanged);
            if (vfxEnableToggle != null)
                vfxEnableToggle.onValueChanged.RemoveListener(OnVfxEnableChanged);
            if (fpsCapDropdown != null)
                fpsCapDropdown.onValueChanged.RemoveListener(OnFpsCapChanged);
            if (pauseHotkeyDropdown != null)
                pauseHotkeyDropdown.onValueChanged.RemoveListener(OnPauseHotkeyChanged);
        }

        private void OnSliderChanged(float value)
        {
            if (_suppressEvents) return;
            bool mute = musicMuteToggle != null && musicMuteToggle.isOn;
            ApplyVolume(value, mute);
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        private void OnMuteChanged(bool mute)
        {
            if (_suppressEvents) return;
            float vol = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
            ApplyVolume(vol, mute);
            PlayerPrefs.SetInt(PREF_MUSIC_MUTE, mute ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnSfxSliderChanged(float value)
        {
            if (_suppressEvents) return;
            bool mute = sfxMuteToggle != null && sfxMuteToggle.isOn;
            ApplySfxVolume(value, mute);
            PlayerPrefs.SetFloat(PREF_SFX_VOL, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        private void OnSfxMuteChanged(bool mute)
        {
            if (_suppressEvents) return;
            float vol = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;
            ApplySfxVolume(vol, mute);
            PlayerPrefs.SetInt(PREF_SFX_MUTE, mute ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyVolume(float vol, bool mute)
        {
            float effective = mute ? 0f : Mathf.Clamp01(vol);
            if (BGMManager.Instance != null)
            {
                BGMManager.Instance.SetMasterVolume(effective);
            }
        }

        private void ApplySfxVolume(float vol, bool mute)
        {
            float effective = mute ? 0f : Mathf.Clamp01(vol);
            if (TR.Audio.SFXManager.Instance != null)
            {
                TR.Audio.SFXManager.Instance.SetMasterVolume(effective);
                TR.Audio.SFXManager.Instance.SetMuted(mute);
            }
        }

        private void OnDamageNumbersChanged(bool show)
        {
            if (_suppressEvents) return;
            TR.UI.DamageNumbers.SetEnabled(show);
            PlayerPrefs.SetInt(PREF_SHOW_DAMAGE_NUMBERS, show ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnVfxQualityChanged(int val)
        {
            if (_suppressEvents) return;
            int qual = Mathf.Clamp(val, 0, 3);
            bool enabled = vfxEnableToggle == null || vfxEnableToggle.isOn;
            ApplyVfxEnableAndQuality(enabled, qual);
            PlayerPrefs.SetInt(PREF_VFX_QUALITY, qual);
            PlayerPrefs.Save();
        }

        private void OnVfxEnableChanged(bool enabled)
        {
            if (_suppressEvents) return;
            int qual = vfxQualityDropdown != null ? Mathf.Clamp(vfxQualityDropdown.value, 0, 3) : 3;
            ApplyVfxEnableAndQuality(enabled, qual);
            PlayerPrefs.SetInt(PREF_VFX_ENABLE, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnFpsCapChanged(int val)
        {
            if (_suppressEvents) return;
            int cap = Mathf.Clamp(val, 0, 3);
            ApplyFpsCap(cap);
            PlayerPrefs.SetInt(PREF_FPS_CAP, cap);
            PlayerPrefs.Save();
        }

        private void OnPauseHotkeyChanged(int val)
        {
            if (_suppressEvents) return;
            int hk = Mathf.Clamp(val, 0, 3);
            PlayerPrefs.SetInt(PREF_PAUSE_HOTKEY, hk);
            PlayerPrefs.Save();
        }

        private void ApplyVfxEnableAndQuality(bool enabled, int qual)
        {
            
            if (vfxQualityDropdown != null) vfxQualityDropdown.interactable = enabled;
            int applied = enabled ? Mathf.Clamp(qual, 0, 3) : 0;
            TR.VFX.ParticleQuality.SetQuality(applied);
        }

        private void ApplyFpsCap(int cap)
        {
            
            int target = -1;
            switch (cap)
            {
                case 0: target = 30; break;
                case 1: target = 60; break;
                case 2: target = 120; break;
                case 3: target = -1; break;
            }
            Application.targetFrameRate = target;
        }

        
        public void ResetToDefaults()
        {
            
            PlayerPrefs.DeleteKey(PREF_MUSIC_VOL);
            PlayerPrefs.DeleteKey(PREF_MUSIC_MUTE);
            PlayerPrefs.DeleteKey(PREF_SFX_VOL);
            PlayerPrefs.DeleteKey(PREF_SFX_MUTE);
            PlayerPrefs.DeleteKey(PREF_SHOW_DAMAGE_NUMBERS);
            PlayerPrefs.DeleteKey(PREF_VFX_QUALITY);
            PlayerPrefs.DeleteKey(PREF_FPS_CAP);
            PlayerPrefs.Save();

            
            _suppressEvents = true;
            
            if (musicVolumeSlider != null) musicVolumeSlider.value = 1f;
            if (musicMuteToggle != null) musicMuteToggle.isOn = false;
            ApplyVolume(1f, false);
            
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;
            if (sfxMuteToggle != null) sfxMuteToggle.isOn = false;
            ApplySfxVolume(1f, false);
            
            if (damageNumbersToggle != null) damageNumbersToggle.isOn = true;
            TR.UI.DamageNumbers.SetEnabled(true);
            
            if (vfxEnableToggle != null) vfxEnableToggle.isOn = true;
            if (vfxQualityDropdown != null) vfxQualityDropdown.value = 3; 
            ApplyVfxEnableAndQuality(true, 3);
            
            if (fpsCapDropdown != null) fpsCapDropdown.value = 3; 
            ApplyFpsCap(3);
            _suppressEvents = false;
        }

        
        public void OpenViaPanelSwitcher(PanelSwitcher switcher, string panelName = "Settings")
        {
            if (switcher != null)
            {
                switcher.ShowByName(panelName);
            }
        }
    }
}
