using System.Collections.Generic;
using System.Text;

namespace Palace
{
    /// <summary>
    /// Keeps rude, sexual and hateful names out of the game. Covers the game's languages (English, Polish,
    /// Spanish) and the others players are most likely to type: French, German, Italian, Portuguese, Dutch,
    /// Turkish, Swedish, Russian/Ukrainian (Cyrillic and Latin letters), and common romanised Hindi/Urdu,
    /// Arabic and Chinese swear words, plus hate symbols.
    ///
    /// Names are compared after undoing the usual disguises: capitals, accents (ą, ñ, ß...), number and
    /// symbol swaps (sh1t, @ss, $lut), Cyrillic letters that look like English ones (fuсk with a Russian с),
    /// dots and spaces between letters (f.u.c.k) and stretched letters (fuuuck). Two lists:
    ///  - Anywhere: words that never turn up inside an ordinary name, so they're caught even inside
    ///    longer text ("xXfuckXx").
    ///  - Whole word: short words that ARE inside ordinary names (Dickens, Hancock, Cassandra, Monique,
    ///    Figaro, Merdan...), so they're only caught on their own ("dick", "big dick", "d.i.c.k").
    /// No filter is perfect; add words to the lists below if testers find gaps (lower case; accents and
    /// Cyrillic are fine, they're normalised the same way as names).
    /// </summary>
    public static class NameFilter
    {
        private static readonly string[] Anywhere = Prepare(
            // English
            "fuck", "fvck", "fck", "phuck", "shit", "cunt", "nigger", "nigga", "faggot", "fagot", "wank", "bitch",
            "biatch", "whore", "slut", "porn", "penis", "vagina", "pussy", "twat", "bollock", "dildo", "horny",
            "boob", "titties", "titty", "jizz", "blowjob", "handjob", "orgasm", "masturb", "hitler", "retard",
            "bastard", "asshole", "arsehole", "dickhead", "cocksuck", "bellend", "tosser", "butthole", "buttplug",
            "scrotum", "testic", "erection", "ejacul", "sperm", "nipple", "hooker", "paedo", "pedophil", "incest",
            "bestial", "sexy", "nsfw", "hentai", "fetish", "bdsm", "kinky", "bigdick", "bigcock", "mydick",
            "mycock", "suckmy", "tranny", "stripper", "siegheil", "whitepower",
            // Polish
            "kurwa", "kurwy", "chuj", "pierdol", "jebac", "jeban", "zajeb", "pizd", "skurw", "kutas",
            "cipka", "szmata", "ciota",
            // Spanish
            "mierda", "joder", "jodete", "cabron", "pendejo", "chinga", "maricon", "follar", "gilipollas",
            "mamada", "culero", "malparido", "hijoputa", "hijodeputa",
            // French
            "putain", "merde", "connard", "connasse", "salope", "encul", "couille", "branleur", "batard",
            "nichon", "foutre", "enfoir",
            // German
            "scheis", "fick", "fotze", "wichser", "arschloch", "schlampe", "hurensohn", "missgeburt",
            "schwuchtel", "muschi", "titten", "verpiss",
            // Italian
            "cazzo", "vaffanculo", "stronzo", "puttana", "coglion", "minchia", "mignotta", "zoccola", "frocio",
            "porcodio",
            // Portuguese
            "caralho", "buceta", "arrombad", "filhodaputa", "piroca", "xoxota", "punheta", "fodase",
            // Dutch
            "klootzak", "godverdom", "neuken", "flikker", "kankerlijer",
            // Turkish
            "orospu", "siktir", "yarrak", "amcik", "pezevenk",
            // Swedish
            "fitta", "knulla", "javla",
            // Russian / Ukrainian, in Cyrillic and in Latin letters
            "хуй", "хуе", "хуя", "пизд", "ебат", "ебан", "блять", "бляд", "мудак", "пидор", "пидар", "залуп",
            "шлюх", "гандон", "дрочи",
            "blyat", "blyad", "ebat", "eblan", "mudak", "pidor", "pidar", "zalup", "gandon", "zhopa",
            // Hindi / Urdu, Arabic and Chinese, as usually typed in Latin letters
            "chutiya", "chutia", "madarchod", "bhenchod", "behenchod", "bhosdi", "gandu",
            "sharmouta", "sharmuta", "kosomak",
            "caonima", "tamade");

        private static readonly string[] WholeWord = Prepare(
            // English
            "ass", "asses", "arse", "dick", "dicks", "cock", "cocks", "tit", "tits", "sex", "cum", "anal", "anus",
            "rape", "raped", "rapist", "nazi", "nazis", "homo", "dyke", "spic", "chink", "kike", "coon", "semen",
            "clit", "prick", "knob", "piss", "crap", "damn", "xxx", "nude", "nudes", "naked", "kkk", "milf",
            "pedo", "fuk", "kunt", "fag", "fags", "slag", "hoe", "hoes", "thot", "smut", "heil",
            // Polish
            "cipa", "dupa", "dupek", "cwel", "pedal",
            // Spanish
            "puta", "puto", "putas", "marica", "zorra", "polla", "verga", "culo", "cono", "pinche", "tetas", "carajo",
            // French
            "pute", "putes", "nique", "niquer", "bite", "pede", "chatte", "cul", "fdp", "ntm",
            // German
            "hure", "arsch", "schwanz", "spast", "mongo", "nutte", "nutten",
            // Italian
            "troia", "figa", "merda",
            // Portuguese
            "porra", "foda", "viado", "cacete",
            // Dutch
            "kut", "tering", "kanker", "lul", "hoer", "mongool",
            // Turkish
            "amk", "kahpe", "pic",
            // Swedish
            "kuk", "hora",
            // Russian / Ukrainian
            "сука", "жопа", "говно", "бля", "suka", "govno", "blya",
            // Numbers with a rude or hateful meaning
            "69", "420", "1488");

        /// <summary>True if the name contains a word that isn't allowed.</summary>
        public static bool IsBlocked(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string plain = Normalise(name);       // lower case, accents and look-alike symbols undone
            var tokens = new List<string>(Tokens(name, plain));

            // Each word on its own; and the letters joined up when the name is spelled out in short pieces
            // ("f u c k", "f*ck", "a.s.s"). Not otherwise, or "Sushi Tom" would read as "sushitom".
            bool spelledOut = false;
            foreach (string t in tokens) if (t.Length <= 2 && char.IsLetter(t[0])) spelledOut = true;
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
                if (char.IsLetter(c)) { sb.Append(c); continue; }
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

        /// <summary>The word lists, put through the same clean-up as names (numbers stay as they are).</summary>
        private static string[] Prepare(params string[] words)
        {
            var result = new string[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                bool number = true;
                foreach (char c in words[i]) if (c < '0' || c > '9') number = false;
                result[i] = number ? words[i] : LettersOnly(Normalise(words[i]));
            }
            return result;
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
                // Accented letters
                case 'ą': case 'á': case 'à': case 'â': case 'ä': case 'ã': case 'å': return 'a';
                case 'ć': case 'ç': case 'č': return 'c';
                case 'ę': case 'é': case 'è': case 'ê': case 'ë': return 'e';
                case 'í': case 'ì': case 'î': case 'ï': case 'ı': return 'i';
                case 'ğ': return 'g';
                case 'ł': return 'l';
                case 'ń': case 'ñ': return 'n';
                case 'ó': case 'ò': case 'ô': case 'ö': case 'õ': case 'ø': case 'ő': return 'o';
                case 'ś': case 'š': case 'ş': case 'ß': return 's';
                case 'ú': case 'ù': case 'û': case 'ü': case 'ű': return 'u';
                case 'ź': case 'ż': case 'ž': return 'z';
                case 'ý': case 'ÿ': return 'y';
                // Cyrillic letters that look like English ones (Russian words in the lists get the same treatment)
                case 'а': return 'a';
                case 'е': case 'ё': return 'e';
                case 'к': return 'k';
                case 'о': return 'o';
                case 'р': return 'p';
                case 'с': return 'c';
                case 'у': return 'y';
                case 'х': return 'x';
                case 'і': return 'i';
                default: return c;
            }
        }

        private static string LettersOnly(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (char.IsLetter(c)) sb.Append(c);
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
