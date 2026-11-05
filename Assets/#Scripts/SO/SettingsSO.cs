using System;
using System.IO;
using UnityEngine;

namespace CardClash
{
    [CreateAssetMenu(fileName = "Settings", menuName = "CardClash/Settings")]
    public class SettingsSO : ScriptableObject
    {
        [Header("Profile")]
        public string UserName = string.Empty;

        [Header("Audio")]
        public bool MusicEnabled = true;
        public bool SoundEnabled = true;

        [Header("Vibration")]
        public bool VibrationEnabled = true;

        private string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        private void ResetToDefaults()
        {
            UserName = string.Empty;
            MusicEnabled = true;
            SoundEnabled = true;
            VibrationEnabled = true;
        }

        public void Load()
        {
            try
            {
                ResetToDefaults();

                if (!File.Exists(FilePath))
                    return;

                var json = File.ReadAllText(FilePath);
                JsonUtility.FromJsonOverwrite(json, this);
            }
            catch (Exception e)
            {
                Debug.LogError($"UserSettingsSO: Failed to load settings. {e}");
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"UserSettingsSO: Failed to save settings. {e}");
            }
        }
    }
}
