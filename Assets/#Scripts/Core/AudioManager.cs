using UnityEngine;

namespace CardClash
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        #region Constants

        private const string SoundKey = "Sound";

        #endregion

        #region Runtime References

        private AudioSource _musicAudioSource;
        [SerializeField] private AudioSource _sfxSource;

        #endregion

        #region Music Settings

        [Header("Music")]
        [SerializeField] private AudioClip _defaultMusic;
        [SerializeField] private AudioClip[] _musicTracks;
        [SerializeField] private AudioClip[] _soundEffects;

        #endregion

        private void Awake()
        {
            if (Application.isBatchMode)
                return;

            _musicAudioSource = GetComponent<AudioSource>();

            if (_defaultMusic != null)
                _musicAudioSource.clip = _defaultMusic;

            var soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;

            SetMusicEnabled(soundEnabled);
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            if (_musicAudioSource == null)
                return;

            _musicAudioSource.mute = !isEnabled;

            if (isEnabled)
                PlayMusic();
            else
                StopMusic();
        }

        public void PlayMusic()
        {
            if (_musicAudioSource == null || _musicAudioSource.isPlaying)
                return;

            _musicAudioSource.Play();
        }

        public void StopMusic()
        {
            if (_musicAudioSource == null)
                return;

            _musicAudioSource.Stop();
        }

        public void PlaySoundEffect(string soundName)
        {
            if (_sfxSource == null || string.IsNullOrEmpty(soundName))
                return;
            var clip = GetSoundEffectClip(soundName);

            if (clip != null)
                _sfxSource.PlayOneShot(clip);
        }

        private AudioClip GetSoundEffectClip(string soundName)
        {
            if (_soundEffects == null || _soundEffects.Length == 0)
                return null;

            foreach (var clip in _soundEffects)
            {
                if (clip != null && clip.name == soundName)
                    return clip;
            }

            return null;
        }
    }
}