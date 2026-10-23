using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        private const string MusicKey = "Music";
        private const string SoundKey = "SoundEffect";

        private const string MusicResourcesFolder = "Music";
        private const string SfxResourcesFolder = "Sound Effects";

        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private AudioSource _sfxSource;

        private AudioSource _musicSource;
        private Dictionary<string, AudioClip> _sfxLookup;
        private Dictionary<string, AudioClip> _musicLookup;
        private bool _musicEnabled;
        private bool _sfxEnabled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (Application.isBatchMode)
                return;

            _sfxLookup = BuildLookup(Resources.LoadAll<AudioClip>(SfxResourcesFolder));
            _musicLookup = BuildLookup(Resources.LoadAll<AudioClip>(MusicResourcesFolder));

            Debug.Log($"AudioManager: Loaded {_sfxLookup.Count} sound effects and {_musicLookup.Count} music tracks.");

            _musicSource = GetComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;

            SetMusicEnabled(PlayerPrefs.GetInt(MusicKey, 1) == 1);
            SetSfxEnabled(PlayerPrefs.GetInt(SoundKey, 1) == 1);
            PlayTrack("menu_loop");
        }

        private Dictionary<string, AudioClip> BuildLookup(AudioClip[] clips)
        {
            var count = clips != null ? clips.Length : 0;
            var lookup = new Dictionary<string, AudioClip>(count, StringComparer.Ordinal);

            for (var i = 0; i < count; i++)
            {
                var clip = clips[i];

                if (clip != null)
                    lookup.TryAdd(clip.name, clip); // first one wins on duplicates
            }

            return lookup;
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            _musicEnabled = isEnabled;

            if (isEnabled)
                PlayMusic();
            else
                StopMusic();
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            _sfxEnabled = isEnabled;
        }

        private void PlayMusic()
        {
            if (!_musicEnabled || _musicSource == null || _musicSource.clip == null || _musicSource.isPlaying)
                return;

            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null && _musicSource.isPlaying)
                _musicSource.Stop();
        }

        public void PlayTrack(string trackName)
        {
            if (_musicLookup == null || string.IsNullOrEmpty(trackName))
                return;

            if (_musicLookup.TryGetValue(trackName, out var clip))
                PlayClip(clip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (_musicSource == null || clip == null)
                return;

            // Same track already running: don't restart it.
            if (_musicSource.clip == clip && _musicSource.isPlaying)
                return;

            _musicSource.clip = clip;

            // Clip is assigned even when sound is off, so it plays once re-enabled.
            if (_musicEnabled)
                _musicSource.Play();
        }

        public void PlaySoundEffect(string soundName)
        {
            if (!_sfxEnabled || _sfxSource == null || _sfxLookup == null || string.IsNullOrEmpty(soundName))
                return;

            if (_sfxLookup.TryGetValue(soundName, out var clip))
                _sfxSource.PlayOneShot(clip);
        }
    }
}