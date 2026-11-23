using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Player preferences, saved on the device with PlayerPrefs.
    /// Read anywhere with e.g. GameSettings.SfxVolume.
    /// </summary>
    public static class GameSettings
    {
        public enum AnimationSpeed { Off = 0, Fast = 1, Normal = 2 }

        public static readonly string[] AnimationNames = { "Off", "Fast", "Normal" };
        public static readonly string[] FrameRateNames = { "30 FPS", "60 FPS" };
        public static readonly string[] TableColourNames = { "Green", "Blue", "Red" };
        public static readonly string[] CardBackNames = { "Burgundy", "Navy" };
        public static readonly string[] BotSpeedNames = { "Relaxed", "Normal", "Quick" };

        /// <summary>How long computer players "think": 0 Relaxed, 1 Normal, 2 Quick.</summary>
        public static int BotSpeed
        {
            get => PlayerPrefs.GetInt("palace.botspeed", 1);
            set => PlayerPrefs.SetInt("palace.botspeed", Mathf.Clamp(value, 0, 2));
        }

        /// <summary>Average seconds a computer player waits before acting.</summary>
        public static float BotThinkSeconds => BotSpeed == 0 ? 1.9f : BotSpeed == 2 ? 0.7f : 1.25f;

        /// <summary>Extra pause after someone picks up the pile, so everyone can take it in.</summary>
        public static float PickUpPauseSeconds => BotSpeed == 0 ? 2.4f : BotSpeed == 2 ? 1.0f : 1.7f;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("palace.music", 0.5f);
            set { PlayerPrefs.SetFloat("palace.music", Mathf.Clamp01(value)); AudioManager.ApplyVolumes(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("palace.sfx", 0.8f);
            set { PlayerPrefs.SetFloat("palace.sfx", Mathf.Clamp01(value)); AudioManager.ApplyVolumes(); }
        }

        public static AnimationSpeed Animation
        {
            get => (AnimationSpeed)PlayerPrefs.GetInt("palace.anim", (int)AnimationSpeed.Normal);
            set => PlayerPrefs.SetInt("palace.anim", (int)value);
        }

        /// <summary>0 = 30 FPS (battery saver), 1 = 60 FPS.</summary>
        public static int FrameRateIndex
        {
            get => PlayerPrefs.GetInt("palace.fps", 1);
            set { PlayerPrefs.SetInt("palace.fps", value); ApplyFrameRate(); }
        }

        public static int TableColour
        {
            get => PlayerPrefs.GetInt("palace.table", 0);
            set => PlayerPrefs.SetInt("palace.table", value);
        }

        public static int CardBack
        {
            get => PlayerPrefs.GetInt("palace.back", 0);
            set => PlayerPrefs.SetInt("palace.back", value);
        }

        public static string PlayerName
        {
            get => PlayerPrefs.GetString("palace.name", "You");
            set => PlayerPrefs.SetString("palace.name", string.IsNullOrWhiteSpace(value) ? "You" : value.Trim());
        }

        public static int BotCount
        {
            get => PlayerPrefs.GetInt("palace.bots", 3);
            set => PlayerPrefs.SetInt("palace.bots", Mathf.Clamp(value, 1, 9));
        }

        /// <summary>Temporary speed used by automated tests; never saved.</summary>
        public static AnimationSpeed? AnimationOverride;

        /// <summary>Multiplier for animation durations. 0 = instant.</summary>
        public static float AnimationScale
        {
            get
            {
                switch (AnimationOverride ?? Animation)
                {
                    case AnimationSpeed.Off: return 0f;
                    case AnimationSpeed.Fast: return 0.55f;
                    default: return 1f;
                }
            }
        }

        public static void ApplyFrameRate()
        {
            Application.targetFrameRate = FrameRateIndex == 0 ? 30 : 60;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
