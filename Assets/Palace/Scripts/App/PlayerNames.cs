using System.Text;

namespace Palace
{
    /// <summary>
    /// Makes any player name safe to show. Names come from other devices in multiplayer, and the game's
    /// text supports formatting tags, so a name like "&lt;size=400&gt;" or "&lt;color=#0000&gt;" could cover
    /// the screen or hide itself on everyone else's table. This strips tag brackets and invisible
    /// characters, squashes spaces and limits the length; a name NameFilter blocks becomes the fallback.
    /// </summary>
    public static class PlayerNames
    {
        public const int MaxLength = 14;

        public static string Clean(string name, string fallback = "Player")
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            var sb = new StringBuilder(name.Length);
            bool space = false;
            foreach (char ch in name)
            {
                if (ch == '<' || ch == '>' || char.IsControl(ch) || IsInvisible(ch)) continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0) space = true; // no leading spaces, and runs of spaces become one
                    continue;
                }
                if (space) { sb.Append(' '); space = false; }
                if (sb.Length >= MaxLength) break;
                sb.Append(ch);
            }
            string clean = sb.ToString().Trim();
            if (clean.Length > MaxLength) clean = clean.Substring(0, MaxLength).Trim();
            if (NameFilter.IsBlocked(clean)) return fallback; // rude, sexual or hateful names (see NameFilter)
            return clean.Length > 0 ? clean : fallback;
        }

        // Zero-width and direction-changing characters that could make a name look like someone else's or vanish.
        private static bool IsInvisible(char ch) =>
            (ch >= (char)0x200B && ch <= (char)0x200F) || (ch >= (char)0x202A && ch <= (char)0x202E) || (ch >= (char)0x2066 && ch <= (char)0x2069) || ch == (char)0xFEFF;
    }
}
