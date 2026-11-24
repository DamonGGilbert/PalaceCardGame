using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Palace
{
    /// <summary>
    /// Keyboard shortcuts for PC players. Works with the new Input System (this project's default)
    /// or the old Input Manager. Phones simply never report key presses.
    /// </summary>
    public static class KeyInput
    {
        /// <summary>A letter (a-z) or digit (0-9) pressed this frame.</summary>
        public static bool Pressed(char c)
        {
            c = char.ToLowerInvariant(c);
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (c >= 'a' && c <= 'z') return kb[Key.A + (c - 'a')].wasPressedThisFrame;
            if (c >= '0' && c <= '9')
            {
                Key row = c == '0' ? Key.Digit0 : Key.Digit1 + (c - '1');
                return kb[row].wasPressedThisFrame || kb[Key.Numpad0 + (c - '0')].wasPressedThisFrame;
            }
            return false;
#else
            if (c >= 'a' && c <= 'z') return Input.GetKeyDown(KeyCode.A + (c - 'a'));
            if (c >= '0' && c <= '9') return Input.GetKeyDown(KeyCode.Alpha0 + (c - '0')) || Input.GetKeyDown(KeyCode.Keypad0 + (c - '0'));
            return false;
#endif
        }

        public static bool Confirm()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        public static bool Escape()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>The digit 1-9 (or 0 meaning 10) pressed this frame, or -1.</summary>
        public static int Digit()
        {
            for (char c = '1'; c <= '9'; c++) if (Pressed(c)) return c - '0';
            return Pressed('0') ? 10 : -1;
        }
    }
}
