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
        public static readonly string[] TableColourNames = { "Pub", "Green", "Blue", "Red" };
        public static readonly string[] CardBackNames = { "Pub", "Burgundy", "Navy" };
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

        public static readonly string[] BotLevelNames = { "Easy", "Normal", "Hard" };

        /// <summary>How well the computer players play: 0 Easy, 1 Normal, 2 Hard (same rules at every level).</summary>
        public static int BotLevel
        {
            get => PlayerPrefs.GetInt("palace.botlevel", 1);
            set => PlayerPrefs.SetInt("palace.botlevel", Mathf.Clamp(value, 0, 2));
        }

        /// <summary>Profile picture (AvatarArt index), or -1 for the first letter of your name.</summary>
        public static int Avatar
        {
            get => PlayerPrefs.GetInt("palace.avatar", -1);
            set => PlayerPrefs.SetInt("palace.avatar", Mathf.Clamp(value, -1, 255));
        }

        /// <summary>Your profile picture if it's unlocked, otherwise -1.</summary>
        public static int AvatarIfUnlocked => AvatarArt.Unlocked(Avatar) ? Avatar : -1;

        /// <summary>Optional game mode: swap hand and face-up cards before play starts.</summary>
        public static bool SwapMode
        {
            get => PlayerPrefs.GetInt("palace.swapmode", 0) == 1;
            set => PlayerPrefs.SetInt("palace.swapmode", value ? 1 : 0);
        }

        /// <summary>Show quick reactions (yours and other players'). Off hides them completely.</summary>
        public static bool ShowReactions
        {
            get => PlayerPrefs.GetInt("palace.reactions", 1) == 1;
            set => PlayerPrefs.SetInt("palace.reactions", value ? 1 : 0);
        }

        /// <summary>A phone notification the day after you play, so you don't lose your streak. Off until chosen.</summary>
        public static bool StreakReminders
        {
            get => PlayerPrefs.GetInt("palace.reminders", 0) == 1;
            set { PlayerPrefs.SetInt("palace.reminders", value ? 1 : 0); StreakReminder.Refresh(value); }
        }

        /// <summary>Has the player been asked about streak reminders yet?</summary>
        public static bool ReminderOffered
        {
            get => PlayerPrefs.GetInt("palace.reminderoffered", 0) == 1;
            set => PlayerPrefs.SetInt("palace.reminderoffered", value ? 1 : 0);
        }

        /// <summary>Has the guided first game been finished (or skipped) on this device?</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt("palace.tutorialdone", 0) == 1;
            set => PlayerPrefs.SetInt("palace.tutorialdone", value ? 1 : 0);
        }

        /// <summary>Has the player been offered the guided game yet?</summary>
        public static bool TutorialOffered
        {
            get => PlayerPrefs.GetInt("palace.tutorialoffered", 0) == 1;
            set => PlayerPrefs.SetInt("palace.tutorialoffered", value ? 1 : 0);
        }

        // ------------------------------------------------------------------ polish + accessibility

        public static readonly string[] OnOffNames = { "Off", "On" };
        public static readonly string[] CardColourNames = { "2 colours", "4 colours" };
        public static readonly string[] TextSizeNames = { "Normal", "Large" };
        public static readonly string[] HandOrderNames = { "By rank", "By suit" };
        public static readonly string[] MusicTrackNames = { "Fireside", "Lounge", "Off" };
        public static readonly string[] LanguageNames = { "English", "Polski", "Español" };

        /// <summary>Phone buzzes on burns, pick-ups and your turn (phones and tablets only).</summary>
        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("palace.vibrate", 1) == 1;
            set => PlayerPrefs.SetInt("palace.vibrate", value ? 1 : 0);
        }

        /// <summary>Four-colour deck: diamonds blue and clubs green, so every suit is easy to tell apart.</summary>
        public static bool FourColourDeck
        {
            get => PlayerPrefs.GetInt("palace.fourcolour", 0) == 1;
            set => PlayerPrefs.SetInt("palace.fourcolour", value ? 1 : 0);
        }

        /// <summary>Larger text everywhere (where it fits).</summary>
        public static bool LargeText
        {
            get => PlayerPrefs.GetInt("palace.largetext", 0) == 1;
            set => PlayerPrefs.SetInt("palace.largetext", value ? 1 : 0);
        }

        public static float TextScale => LargeText ? 1.18f : 1f;

        /// <summary>0 = sort the hand by rank, 1 = group it by suit.</summary>
        public static int HandOrder
        {
            get => PlayerPrefs.GetInt("palace.handorder", 0);
            set => PlayerPrefs.SetInt("palace.handorder", Mathf.Clamp(value, 0, 1));
        }

        /// <summary>0 Fireside (guitar), 1 Lounge (soft chords), 2 Off.</summary>
        public static int MusicTrack
        {
            get => PlayerPrefs.GetInt("palace.musictrack", 0);
            set { PlayerPrefs.SetInt("palace.musictrack", Mathf.Clamp(value, 0, 2)); AudioManager.ApplyTrack(); }
        }

        /// <summary>0 English, 1 Polish, 2 Spanish.</summary>
        public static int Language
        {
            get => PlayerPrefs.GetInt("palace.language", Loc.DeviceDefault());
            set { PlayerPrefs.SetInt("palace.language", Mathf.Clamp(value, 0, LanguageNames.Length - 1)); Loc.Refresh(); }
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
