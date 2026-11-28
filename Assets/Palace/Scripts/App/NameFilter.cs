using System.Collections.Generic;
using System.Text;

namespace Palace
{
    /// <summary>
    /// Keeps rude, sexual and hateful names out of the game (English, Polish and Spanish).
    ///
    /// Names are compared after undoing the usual disguises: capitals, accents (ą, ñ...), number and
    /// symbol swaps (sh1t, @ss, $lut), dots and spaces between letters (f.u.c.k) and stretched letters
    /// (fuuuck). Two lists:
    ///  - Anywhere: words that never turn up inside an ordinary name, so they're caught even inside
    ///    longer text ("xXfuckXx").
    ///  - Whole word: short words that ARE inside ordinary names (Dickens, Hancock, Cassandra, Essex,
    ///    Grape, Pollard...), so they're only caught on their own ("dick", "big dick", "d.i.c.k").
    /// No filter is perfect; add words to the lists below if testers find gaps. Lists are lower case,
    /// without accents.
    /// </summary>
    public static class NameFilter
    {
        private static readonly string[] Anywhere =
        {
            // English
            "fuck", "fvck", "fck", "phuck", "shit", "cunt", "nigger", "nigga", "faggot", "fagot", "wank", "bitch",
            "biatch", "whore", "slut", "porn", "penis", "vagina", "pussy", "twat", "bollock", "dildo", "horny",
            "boob", "titties", "titty", "jizz", "blowjob", "handjob", "orgasm", "masturb", "hitler", "retard",
            "bastard", "asshole", "arsehole", "dickhead", "cocksuck", "bellend", "tosser", "butthole", "buttplug",
            "scrotum", "testic", "erection", "ejacul", "sperm", "nipple", "hooker", "paedo", "pedophil", "incest",
            "bestial", "sexy", "nsfw", "hentai", "fetish", "bdsm", "kinky", "bigdick", "bigcock", "mydick",
            "mycock", "suckmy", "tranny", "stripper",
            // Polish
            "kurwa", "kurwy", "chuj", "pierdol", "jebac", "jeban", "zajeb", "pizda", "pizdy", "skurw", "kutas",
            "cipka", "szmata", "ciota",
            // Spanish
            "mierda", "joder", "jodete", "cabron", "pendejo", "chinga", "maricon", "follar", "gilipollas",
            "mamada", "culero", "malparido", "hijoputa", "hijodeputa"
        };

        private static readonly string[] WholeWord =
        {
            // English
            "ass", "asses", "arse", "dick", "dicks", "cock", "cocks", "tit", "tits", "sex", "cum", "anal", "anus",
            "rape", "raped", "rapist", "nazi", "nazis", "homo", "dyke", "spic", "chink", "kike", "coon", "semen",
            "clit", "prick", "knob", "piss", "crap", "damn", "xxx", "nude", "nudes", "naked", "kkk", "milf",
            "pedo", "fuk", "kunt", "fag", "fags", "slag", "hoe", "hoes", "thot", "smut",
            // Polish
            "cipa", "dupa", "dupek", "cwel", "pedal",
            // Spanish
            "puta", "puto", "putas", "marica", "zorra", "polla", "verga", "culo", "cono", "pinche", "tetas", "carajo",
            // Numbers with a rude meaning
            "69", "420"
        };

        /// <summary>True if the name contains a word that isn't allowed.</summary>
        public static bool IsBlocked(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string plain = Normalise(name);       // lower case, accents and look-alike symbols undone
            var tokens = new List<string>(Tokens(name, plain));

            // Each word on its own; and the letters joined up when the name is spelled out in short pieces
            // ("f u c k", "f*ck", "a.s.s"). Not otherwise, or "Sushi Tom" would read as "sushitom".
            bool spelledOut = false;
            foreach (string t in tokens) if (t.Length <= 2 && t[0] >= 'a') spelledOut = true;
            if (spelledOut) tokens.Add(LettersOnly(plain));

            foreach (string token in tokens)
                if (ContainsAnywhereWord(token) || IsWhole(token)) return true;
            return false;
        }

        private static bool ContainsAnywhereWord(string token)
        {
            string runs2 = Squash(token, 2); // "fuuuck" -> "fuuck", "niggger" -> "nigger"
            string runs1 = Squash(token, 1); // "fuuuck" -> "fuck"
            foreach (string w in Anywhere)
            {
                if (runs2.Contains(w)) return true;
                if (!HasDoubleLetter(w) && runs1.Contains(w)) return true; // only for words without double letters ("boob" mustn't catch "Bob")
            }
            return false;
        }

        private static bool IsWhole(string token)
        {
            if (token.Length == 0) return false;
            string runs1 = Squash(token, 1);
            foreach (string w in WholeWord)
            {
                if (token == w || Squash(token, 2) == w) return true;
                string w1 = Squash(w, 1);
                if (w1.Length >= 3 && runs1 == w1) return true; // "cuuum", "diiick"
            }
            return false;
        }

        /// <summary>The separate words of a name: letter runs (after undoing disguises) and the original number runs.</summary>
        private static IEnumerable<string> Tokens(string original, string plain)
        {
            var sb = new StringBuilder();
            foreach (char c in plain)
            {
                if (c >= 'a' && c <= 'z') { sb.Append(c); continue; }
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            if (sb.Length > 0) yield return sb.ToString();

            sb.Clear();
            foreach (char c in original)
            {
                if (c >= '0' && c <= '9') { sb.Append(c); continue; }
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static string Normalise(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char raw in s.ToLowerInvariant()) sb.Append(Map(raw));
            return sb.ToString();
        }

        private static char Map(char c)
        {
            switch (c)
            {
                // Number and symbol swaps
                case '0': return 'o';
                case '1': case '!': case '|': return 'i';
                case '3': return 'e';
                case '4': case '@': return 'a';
                case '5': case '$': return 's';
                case '7': case '+': return 't';
                case '8': return 'b';
                case '9': return 'g';
                // Accented letters (Polish, Spanish and common others)
                case 'ą': case 'á': case 'à': case 'â': case 'ä': case 'ã': case 'å': return 'a';
                case 'ć': case 'ç': case 'č': return 'c';
                case 'ę': case 'é': case 'è': case 'ê': case 'ë': return 'e';
                case 'í': case 'ì': case 'î': case 'ï': return 'i';
                case 'ł': return 'l';
                case 'ń': case 'ñ': return 'n';
                case 'ó': case 'ò': case 'ô': case 'ö': case 'õ': case 'ø': return 'o';
                case 'ś': case 'š': case 'ß': return 's';
                case 'ú': case 'ù': case 'û': case 'ü': return 'u';
                case 'ź': case 'ż': case 'ž': return 'z';
                case 'ý': case 'ÿ': return 'y';
                default: return c;
            }
        }

        private static string LettersOnly(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (c >= 'a' && c <= 'z') sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Shortens every run of the same letter to at most <paramref name="max"/>.</summary>
        private static string Squash(string s, int max)
        {
            var sb = new StringBuilder(s.Length);
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                run = i > 0 && s[i] == s[i - 1] ? run + 1 : 1;
                if (run <= max) sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static bool HasDoubleLetter(string w)
        {
            for (int i = 1; i < w.Length; i++) if (w[i] == w[i - 1]) return true;
            return false;
        }
    }
}
