using System;
using System.Collections.Generic;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Translations: English (the text written in the code), Polish and Spanish.
    ///
    /// Loc.T("Back") returns the text in the chosen language, or the English if there is no
    /// translation yet, so a missing entry never breaks anything. Loc.F("{0}'s turn", name) does
    /// the same for text with values in it. Text made through UIBuild.Text / UIKit is translated
    /// automatically. To add a translation, add a line to the table at the bottom:
    ///     Add("English text", "Polish text", "Spanish text");
    /// Translations were written for Palace and are worth a check by a native speaker before release.
    /// </summary>
    public static class Loc
    {
        public const int English = 0, Polish = 1, Spanish = 2;

        private static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
        private static readonly List<(string prefix, string suffix, string key)> patterns = new List<(string, string, string)>();

        /// <summary>0 English, 1 Polish, 2 Spanish (Settings > Language).</summary>
        public static int Current
        {
            get
            {
                if (!Application.isPlaying) return English;   // editor tools always build in English
                if (TestLanguage >= 0) return TestLanguage;
                if (cached < 0) cached = GameSettings.Language;
                return cached;
            }
        }

        private static int cached = -1;

        /// <summary>TESTING ONLY (command line -palace-lang N): show a language without saving it.</summary>
        public static int TestLanguage = -1;

        /// <summary>Call after the language setting changes.</summary>
        public static void Refresh() => cached = -1;

        /// <summary>First launch: match the phone's language when we have it.</summary>
        public static int DeviceDefault()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Polish: return Polish;
                case SystemLanguage.Spanish: return Spanish;
                default: return English;
            }
        }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            int lang = Current;
            if (lang == English) return english;
            if (table.TryGetValue(english, out var t)) return Pick(t, lang, english);

            // Messages with a value in the middle, e.g. "Needs J or higher" matches "Needs {0} or higher".
            foreach (var p in patterns)
            {
                if (english.Length <= p.prefix.Length + p.suffix.Length) continue;
                if (!english.StartsWith(p.prefix, StringComparison.Ordinal) || !english.EndsWith(p.suffix, StringComparison.Ordinal)) continue;
                string middle = english.Substring(p.prefix.Length, english.Length - p.prefix.Length - p.suffix.Length);
                return string.Format(Pick(table[p.key], lang, p.key), T(middle));
            }
            return english;
        }

        public static string F(string englishFormat, params object[] args)
        {
            try { return string.Format(T(englishFormat), args); }
            catch (FormatException) { return string.Format(englishFormat, args); }
        }

        /// <summary>1st / 2nd / 3rd in English, "1." in Polish, "1.º" in Spanish.</summary>
        public static string Ordinal(int n)
        {
            switch (Current)
            {
                case Polish: return n + ".";
                case Spanish: return n + ".º";
                default:
                    int mod100 = n % 100;
                    string suffix = mod100 >= 11 && mod100 <= 13 ? "th" : (n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th");
                    return n + suffix;
            }
        }

        private static string Pick(string[] t, int lang, string english)
        {
            string s = lang - 1 < t.Length ? t[lang - 1] : null;
            return string.IsNullOrEmpty(s) ? english : s;
        }

        private static void Add(string en, string pl, string es)
        {
            table[en] = new[] { pl, es };
            int at = en.IndexOf("{0}", StringComparison.Ordinal);
            if (at >= 0 && en.IndexOf("{1}", StringComparison.Ordinal) < 0)
                patterns.Add((en.Substring(0, at), en.Substring(at + 3), en));
        }

        // ==================================================================
        // The table: English, Polish, Spanish
        // ==================================================================

        static Loc()
        {
            // ---------------------------------------------------------- main menu
            Add("THE CLASSIC SHEDHEAD CARD GAME", "KLASYCZNA GRA KARCIANA SHEDHEAD", "EL CLÁSICO JUEGO DE CARTAS SHEDHEAD");
            Add("Single Player", "Jeden gracz", "Un jugador");
            Add("Multiplayer", "Gra wieloosobowa", "Multijugador");
            Add("Customise", "Personalizuj", "Personalizar");
            Add("How to Play", "Jak grać", "Cómo jugar");
            Add("Settings", "Ustawienia", "Ajustes");
            Add("Credits", "Twórcy", "Créditos");
            Add("Exit", "Wyjdź", "Salir");
            Add("Back", "Wstecz", "Atrás");
            Add("{0} coins", "Monety: {0}", "{0} monedas");

            // ---------------------------------------------------------- single player
            Add("Your name", "Twoje imię", "Tu nombre");
            Add("Enter your name", "Wpisz swoje imię", "Escribe tu nombre");
            Add("Computer opponents: {0}", "Przeciwnicy komputerowi: {0}", "Rivales del ordenador: {0}");
            Add("{0} players at the table, 1 deck", "Graczy przy stole: {0}, 1 talia", "{0} jugadores en la mesa, 1 baraja");
            Add("{0} players at the table, {1} decks", "Graczy przy stole: {0}, talie: {1}", "{0} jugadores en la mesa, {1} barajas");
            Add("Start Game", "Zacznij grę", "Empezar");

            // ---------------------------------------------------------- settings
            Add("Sound & speed", "Dźwięk i tempo", "Sonido y velocidad");
            Add("Display", "Wygląd", "Pantalla");
            Add("Cloud save", "Zapis w chmurze", "Guardado en la nube");
            Add("Music", "Muzyka", "Música");
            Add("Music track", "Utwór", "Pista musical");
            Add("Sound effects", "Efekty dźwiękowe", "Efectos de sonido");
            Add("Vibration", "Wibracje", "Vibración");
            Add("Animations", "Animacje", "Animaciones");
            Add("Computer speed", "Tempo komputera", "Velocidad del ordenador");
            Add("Frame rate", "Klatki na sekundę", "Fotogramas");
            Add("Language", "Język", "Idioma");
            Add("Text size", "Rozmiar tekstu", "Tamaño del texto");
            Add("Card colours", "Kolory kart", "Colores de cartas");
            Add("Hand order", "Kolejność kart", "Orden de la mano");
            Add("Off", "Wył.", "No");
            Add("On", "Wł.", "Sí");
            Add("Fast", "Szybko", "Rápido");
            Add("Normal", "Normalnie", "Normal");
            Add("Relaxed", "Spokojnie", "Tranquilo");
            Add("Quick", "Szybko", "Rápido");
            Add("Large", "Duży", "Grande");
            Add("2 colours", "2 kolory", "2 colores");
            Add("4 colours", "4 kolory", "4 colores");
            Add("By rank", "Wg wartości", "Por valor");
            Add("By suit", "Wg koloru", "Por palo");
            Add("Fireside", "Przy kominku", "Junto al fuego");
            Add("Lounge", "Salon", "Salón");
            Add("Changes are saved automatically.", "Zmiany zapisują się automatycznie.", "Los cambios se guardan solos.");
            Add("Table, cards and card backs are on the Customise screen.", "Stół, karty i rewersy zmienisz w Personalizacji.", "La mesa, las cartas y los reversos están en Personalizar.");
            Add("4 colours: diamonds are blue and clubs are green, so every suit stands out.",
                "4 kolory: karo jest niebieskie, a trefl zielony, więc każdy kolor łatwo odróżnić.",
                "4 colores: los diamantes son azules y los tréboles verdes, para distinguir cada palo.");
            Add("Back up your coins, unlocks and purchases. Sign in with a free Unity account to take them to another phone or PC.",
                "Zachowaj kopię monet, odblokowań i zakupów. Zaloguj się darmowym kontem Unity, aby przenieść je na inny telefon lub PC.",
                "Guarda una copia de tus monedas, desbloqueos y compras. Inicia sesión con una cuenta gratuita de Unity para llevarlas a otro móvil o PC.");
            Add("Sign in (Unity account)", "Zaloguj (konto Unity)", "Iniciar sesión (Unity)");
            Add("Sync now", "Synchronizuj", "Sincronizar");
            Add("Signed in with your Unity account.", "Zalogowano kontem Unity.", "Sesión iniciada con tu cuenta de Unity.");
            Add("Not signed in: progress is backed up, but only this device can restore it.",
                "Nie zalogowano: postęp jest zapisany, ale przywróci go tylko to urządzenie.",
                "Sin sesión: tu progreso tiene copia, pero solo este dispositivo puede recuperarlo.");
            Add("Cloud save isn't included in this version yet.", "Zapis w chmurze nie jest jeszcze dostępny w tej wersji.", "El guardado en la nube aún no está en esta versión.");
            Add("Syncing...", "Synchronizacja...", "Sincronizando...");
            Add("Opening sign-in...", "Otwieranie logowania...", "Abriendo el inicio de sesión...");
            Add("Progress saved to your account.", "Postęp zapisany na Twoim koncie.", "Progreso guardado en tu cuenta.");
            Add("Progress backed up on this device's cloud save.", "Postęp zapisany w chmurze tego urządzenia.", "Progreso guardado en la nube de este dispositivo.");
            Add("Cloud save isn't set up yet (the game needs linking to Unity Cloud).", "Zapis w chmurze nie jest jeszcze skonfigurowany (gra wymaga połączenia z Unity Cloud).", "El guardado en la nube aún no está configurado (el juego debe vincularse a Unity Cloud).");
            Add("No internet connection - progress is still saved on this device.", "Brak internetu - postęp jest nadal zapisany na tym urządzeniu.", "Sin conexión - el progreso sigue guardado en este dispositivo.");
            Add("Couldn't sync right now - progress is still saved on this device.", "Nie udało się zsynchronizować - postęp jest nadal zapisany na tym urządzeniu.", "No se pudo sincronizar - el progreso sigue guardado en este dispositivo.");
            Add("Install the Cloud Save package to enable this (see Docs/Cloud-Save-Setup.md).", "Zainstaluj pakiet Cloud Save, aby to włączyć (zob. Docs/Cloud-Save-Setup.md).", "Instala el paquete Cloud Save para activarlo (ver Docs/Cloud-Save-Setup.md).");

            // ---------------------------------------------------------- credits
            Add("Game design and house rules", "Projekt gry i zasady domowe", "Diseño del juego y reglas de la casa");
            Add("Programming, art and sound", "Programowanie, grafika i dźwięk", "Programación, arte y sonido");
            Add("Built with Claude (Anthropic)", "Zbudowane z Claude (Anthropic)", "Creado con Claude (Anthropic)");
            Add("Made with Unity", "Stworzone w Unity", "Hecho con Unity");
            Add("Fonts: Alegreya and Liberation Sans (SIL Open Font License)", "Czcionki: Alegreya i Liberation Sans (SIL Open Font License)", "Fuentes: Alegreya y Liberation Sans (SIL Open Font License)");
            Add("All card art, table art and sounds are generated in code for this game.", "Cała grafika kart i stołów oraz dźwięki są generowane w kodzie tej gry.", "Todo el arte de cartas y mesas y los sonidos se generan por código para este juego.");
            Add("Translations: English, Polski, Español", "Tłumaczenia: English, Polski, Español", "Traducciones: English, Polski, Español");

            // ---------------------------------------------------------- customise + shop
            Add("Table", "Stół", "Mesa");
            Add("Cards", "Karty", "Cartas");
            Add("Card back", "Rewers", "Reverso");
            Add("Banner", "Baner", "Cartel");
            Add("Name style", "Styl imienia", "Estilo de nombre");
            Add("Effects", "Efekty", "Efectos");
            Add("Shop", "Sklep", "Tienda");
            Add("Equip", "Załóż", "Equipar");
            Add("Equipped", "Założone", "Equipado");
            Add("Owned", "Posiadane", "Comprado");
            Add("Claim", "Odbierz", "Reclamar");
            Add("Watch an ad", "Obejrzyj reklamę", "Ver un anuncio");
            Add("Buy  {0}", "Kup  {0}", "Comprar  {0}");
            Add("Bonus Coins", "Monety bonusowe", "Monedas extra");
            Add("+{0} coins", "+{0} monet", "+{0} monedas");
            Add("+{0} coins!", "+{0} monet!", "¡+{0} monedas!");
            Add("{0} left today", "Dziś zostało: {0}", "Quedan {0} hoy");
            Add("Supporter Pack (Shop)", "Pakiet Wspierającego (Sklep)", "Pack Mecenas (Tienda)");
            Add("Restore purchases", "Przywróć zakupy", "Restaurar compras");
            Add("Win games to earn coins: 1st place pays 50, and every finish earns something.",
                "Wygrywaj, aby zdobywać monety: 1. miejsce daje 50, a każde ukończenie coś przynosi.",
                "Gana partidas para conseguir monedas: el 1.º puesto da 50 y cada final da algo.");
            Add("{0} equipped.", "Założono: {0}.", "{0} equipado.");
            Add("You need {0} more coins for {1}.", "Brakuje Ci {0} monet na: {1}.", "Te faltan {0} monedas para {1}.");
            Add("Unlocked {0}!", "Odblokowano: {0}!", "¡Desbloqueado: {0}!");
            Add("No bonus available right now.", "Bonus jest teraz niedostępny.", "No hay bonificación ahora mismo.");
            Add("You already own {0}.", "Masz już: {0}.", "Ya tienes {0}.");
            Add("Thank you! {0} has been added.", "Dziękujemy! Dodano: {0}.", "¡Gracias! Se ha añadido {0}.");
            Add("Pouch of Coins", "Sakiewka monet", "Bolsita de monedas");
            Add("Bag of Coins", "Worek monet", "Saco de monedas");
            Add("Chest of Coins", "Skrzynia monet", "Cofre de monedas");
            Add("Water Pack", "Pakiet Woda", "Pack Agua");
            Add("Midnight Pack", "Pakiet Północ", "Pack Medianoche");
            Add("Remove Ads", "Usuń reklamy", "Quitar anuncios");
            Add("Supporter Pack", "Pakiet Wspierającego", "Pack Mecenas");
            Add("500 coins", "500 monet", "500 monedas");
            Add("1,500 coins", "1500 monet", "1.500 monedas");
            Add("4,000 coins: best value", "4000 monet: najlepsza oferta", "4.000 monedas: la mejor oferta");
            Add("Lagoon table + Water effects", "Stół Laguna + efekty wody", "Mesa Laguna + efectos de agua");
            Add("Midnight Gold deck, back and table", "Talia, rewers i stół Złota Północ", "Baraja, reverso y mesa Oro Medianoche");
            Add("Collect bonus coins without watching ads", "Odbieraj bonusowe monety bez reklam", "Consigue monedas extra sin ver anuncios");
            Add("5,000 coins + exclusive Supporter banner and Royal name", "5000 monet + baner Wspierającego i królewskie imię", "5.000 monedas + cartel de Mecenas y nombre Real exclusivos");
            Add("Connecting to the store...", "Łączenie ze sklepem...", "Conectando con la tienda...");
            Add("The store is unavailable right now.", "Sklep jest teraz niedostępny.", "La tienda no está disponible ahora.");
            Add("Couldn't load the shop.", "Nie udało się wczytać sklepu.", "No se pudo cargar la tienda.");
            Add("The shop isn't available in this version.", "Sklep nie jest dostępny w tej wersji.", "La tienda no está disponible en esta versión.");
            Add("Test store: purchases are free (editor / development builds only).", "Sklep testowy: zakupy są darmowe (tylko wersje deweloperskie).", "Tienda de prueba: las compras son gratis (solo versiones de desarrollo).");
            Add("Test store: nothing to restore.", "Sklep testowy: nic do przywrócenia.", "Tienda de prueba: nada que restaurar.");
            Add("Purchase didn't go through ({0}).", "Zakup się nie powiódł ({0}).", "La compra no se completó ({0}).");
            Add("Your purchase is waiting for approval.", "Zakup czeka na zatwierdzenie.", "Tu compra está pendiente de aprobación.");
            Add("That item isn't available right now.", "Ten przedmiot jest teraz niedostępny.", "Ese artículo no está disponible ahora.");
            Add("Purchases restored.", "Przywrócono zakupy.", "Compras restauradas.");
            Add("Couldn't restore purchases: {0}", "Nie udało się przywrócić zakupów: {0}", "No se pudieron restaurar las compras: {0}");

            // cosmetic names
            Add("Pub Green", "Zieleń pubu", "Verde de pub");
            Add("Casino Green", "Zieleń kasyna", "Verde casino");
            Add("Crimson Velvet", "Karmazynowy aksamit", "Terciopelo carmesí");
            Add("Midnight Blue", "Granat północy", "Azul medianoche");
            Add("Casino Blue", "Błękit kasyna", "Azul casino");
            Add("Casino Red", "Czerwień kasyna", "Rojo casino");
            Add("Lagoon (animated)", "Laguna (animowana)", "Laguna (animada)");
            Add("Parlour", "Salonik", "Salita");
            Add("Casino", "Kasyno", "Casino");
            Add("Midnight Gold", "Złota Północ", "Oro Medianoche");
            Add("Oxblood Rosette", "Bordowa rozeta", "Roseta granate");
            Add("Casino Burgundy", "Burgund kasyna", "Burdeos casino");
            Add("Casino Navy", "Granat kasyna", "Marino casino");
            Add("Emerald Rosette", "Szmaragdowa rozeta", "Roseta esmeralda");
            Add("Midnight Rosette", "Rozeta północy", "Roseta medianoche");
            Add("Walnut", "Orzech", "Nogal");
            Add("Slate", "Łupek", "Pizarra");
            Add("Stitched Leather", "Szyta skóra", "Cuero cosido");
            Add("Polished Brass", "Polerowany mosiądz", "Latón pulido");
            Add("Supporter Gold", "Złoto Wspierającego", "Oro de Mecenas");
            Add("Plain", "Zwykły", "Sencillo");
            Add("Gilded", "Złocony", "Dorado");
            Add("Frost", "Szron", "Escarcha");
            Add("Neon", "Neon", "Neón");
            Add("Blaze", "Płomień", "Llamarada");
            Add("Royal (Supporter)", "Królewski (Wspierający)", "Real (Mecenas)");
            Add("Fire & Embers", "Ogień i żar", "Fuego y brasas");
            Add("Water", "Woda", "Agua");
            Add("England", "Anglia", "Inglaterra");
            Add("Scotland", "Szkocja", "Escocia");
            Add("United Kingdom", "Wielka Brytania", "Reino Unido");
            Add("Ireland", "Irlandia", "Irlanda");
            Add("Poland", "Polska", "Polonia");
            Add("Spain", "Hiszpania", "España");
            Add("France", "Francja", "Francia");
            Add("Germany", "Niemcy", "Alemania");
            Add("Italy", "Włochy", "Italia");
            Add("USA", "USA", "EE. UU.");

            // ---------------------------------------------------------- multiplayer
            Add("Same Wi-Fi", "Ta sama sieć Wi-Fi", "Misma Wi-Fi");
            Add("Online", "Online", "En línea");
            Add("Host a game", "Załóż grę", "Crear partida");
            Add("Join a game", "Dołącz do gry", "Unirse a partida");
            Add("Join with a code", "Dołącz kodem", "Unirse con código");
            Add("Join on Wi-Fi", "Dołącz przez Wi-Fi", "Unirse por Wi-Fi");
            Add("Join", "Dołącz", "Unirse");
            Add("Games on this Wi-Fi:", "Gry w tej sieci Wi-Fi:", "Partidas en esta Wi-Fi:");
            Add("Or type the host's address:", "Lub wpisz adres gospodarza:", "O escribe la dirección del anfitrión:");
            Add("Ask the host for their join code", "Poproś gospodarza o kod gry", "Pide al anfitrión su código");
            Add("Code", "Kod", "Código");
            Add("Game room", "Pokój gry", "Sala de juego");
            Add("+ Computer player", "+ Gracz komputerowy", "+ Jugador CPU");
            Add("- Computer player", "- Gracz komputerowy", "- Jugador CPU");
            Add("Start game", "Zacznij grę", "Empezar partida");
            Add("I'm ready", "Jestem gotów", "Estoy listo");
            Add("Not ready", "Nie gotów", "No estoy listo");
            Add("Leave", "Wyjdź", "Salir");
            Add("computer", "komputer", "ordenador");
            Add("ready", "gotów", "listo");
            Add("not ready", "niegotów", "no listo");
            Add("(host)", "(gospodarz)", "(anfitrión)");
            Add("you", "ty", "tú");
            Add("Join code:", "Kod gry:", "Código:");
            Add("On Wi-Fi at", "W sieci Wi-Fi pod adresem", "En la Wi-Fi en");
            Add("Connected on Wi-Fi", "Połączono przez Wi-Fi", "Conectado por Wi-Fi");
            Add("Looking for games...", "Szukanie gier...", "Buscando partidas...");
            Add("Looking for games... (the host must be on the same Wi-Fi)", "Szukanie gier... (gospodarz musi być w tej samej sieci Wi-Fi)", "Buscando partidas... (el anfitrión debe estar en la misma Wi-Fi)");
            Add("Connecting to {0}...", "Łączenie z {0}...", "Conectando con {0}...");
            Add("Creating an online game...", "Tworzenie gry online...", "Creando partida en línea...");
            Add("Joining...", "Dołączanie...", "Uniéndose...");
            Add("Couldn't reach that game. Are you on the same Wi-Fi?", "Nie można połączyć się z grą. Czy jesteście w tej samej sieci Wi-Fi?", "No se pudo llegar a esa partida. ¿Estáis en la misma Wi-Fi?");
            Add("Couldn't reach that game.", "Nie można połączyć się z grą.", "No se pudo llegar a esa partida.");
            Add("Couldn't start hosting on this network.", "Nie można założyć gry w tej sieci.", "No se pudo crear la partida en esta red.");
            Add("Couldn't connect to {0}", "Nie można połączyć się z {0}", "No se pudo conectar con {0}");
            Add("Couldn't create an online game. {0}", "Nie udało się utworzyć gry online. {0}", "No se pudo crear la partida en línea. {0}");
            Add("Couldn't join that game. Check the code. {0}", "Nie udało się dołączyć. Sprawdź kod. {0}", "No se pudo unir a esa partida. Revisa el código. {0}");
            Add("(The game isn't linked to Unity Cloud yet - see Docs/Multiplayer-Setup.md.)", "(Gra nie jest jeszcze połączona z Unity Cloud - zob. Docs/Multiplayer-Setup.md.)", "(El juego aún no está vinculado a Unity Cloud - ver Docs/Multiplayer-Setup.md.)");
            Add("That game has already started.", "Ta gra już się zaczęła.", "Esa partida ya ha empezado.");
            Add("That game is full.", "Ta gra jest pełna.", "Esa partida está llena.");
            Add("The host left the game.", "Gospodarz opuścił grę.", "El anfitrión ha salido de la partida.");
            Add("Lost connection to the host.", "Utracono połączenie z gospodarzem.", "Se perdió la conexión con el anfitrión.");
            Add("{0}'s game   (1 player)", "Gra: {0}   (1 gracz)", "Partida de {0}   (1 jugador)");
            Add("{0}'s game   ({1} players)", "Gra: {0}   (graczy: {1})", "Partida de {0}   ({1} jugadores)");
            Add("Waiting for players to join (or add computer players)", "Czekanie na graczy (lub dodaj graczy komputerowych)", "Esperando jugadores (o añade jugadores CPU)");
            Add("{0} players. Everyone is ready!", "Graczy: {0}. Wszyscy gotowi!", "{0} jugadores. ¡Todos listos!");
            Add("Waiting for everyone to be ready", "Czekanie, aż wszyscy będą gotowi", "Esperando a que todos estén listos");
            Add("Waiting for the host to start (1 person here)", "Czekanie na start gospodarza (1 osoba)", "Esperando a que empiece el anfitrión (1 persona)");
            Add("Waiting for the host to start ({0} people here)", "Czekanie na start gospodarza (osób: {0})", "Esperando a que empiece el anfitrión ({0} personas)");

            // ---------------------------------------------------------- the table
            Add("Deck", "Talia", "Mazo");
            Add("Deck empty", "Talia pusta", "Mazo vacío");
            Add("Pile", "Stos", "Montón");
            Add("Pile empty", "Stos pusty", "Montón vacío");
            Add("Play goes clockwise", "Gra idzie zgodnie z ruchem wskazówek", "Se juega en sentido horario");
            Add("Play goes anticlockwise", "Gra idzie przeciwnie do ruchu wskazówek", "Se juega en sentido antihorario");
            Add("View pile", "Pokaż stos", "Ver montón");
            Add("Menu", "Menu", "Menú");
            Add("Paused", "Pauza", "Pausa");
            Add("Resume", "Wznów", "Continuar");
            Add("Restart", "Od nowa", "Reiniciar");
            Add("Main Menu", "Menu główne", "Menú principal");
            Add("Leave game", "Opuść grę", "Salir de la partida");
            Add("PLAY", "ZAGRAJ", "JUGAR");
            Add("PLAY CARD", "ZAGRAJ KARTĘ", "JUGAR CARTA");
            Add("PLAY {0} CARDS", "ZAGRAJ KARTY: {0}", "JUGAR {0} CARTAS");
            Add("PICK UP PILE", "WEŹ STOS", "RECOGER MONTÓN");
            Add("Speed 1x", "Tempo 1x", "Velocidad 1x");
            Add("Speed {0}x", "Tempo {0}x", "Velocidad {0}x");
            Add("HIGHER", "WYŻEJ", "MAYOR");
            Add("LOWER", "NIŻEJ", "MENOR");
            Add("You played a 7. The next player must play...", "Zagrałeś 7. Następny gracz musi zagrać...", "Has jugado un 7. El siguiente jugador debe jugar...");
            Add("BURN THE PILE (Ace as 10)", "SPAL STOS (As jako 10)", "QUEMAR EL MONTÓN (As como 10)");
            Add("Your Ace counts as... (LOWER than 7 was called)", "Twój As liczy się jako... (wybrano NIŻEJ niż 7)", "Tu As vale... (se pidió MENOR que 7)");
            Add("Your Ace counts as... (it must match or beat the pile)", "Twój As liczy się jako... (musi być równy lub wyższy)", "Tu As vale... (debe igualar o superar el montón)");

            Add("Waiting for the host to deal...", "Czekanie, aż gospodarz rozda karty...", "Esperando a que el anfitrión reparta...");
            Add("Game over", "Koniec gry", "Fin de la partida");
            Add("{0} is calling higher or lower...", "{0} wybiera: wyżej czy niżej...", "{0} elige mayor o menor...");
            Add("{0} is choosing how to use the Ace...", "{0} decyduje, jak użyć Asa...", "{0} decide cómo usar el As...");
            Add("{0}'s turn", "Tura: {0}", "Turno de {0}");
            Add("Call HIGHER or LOWER for the next player", "Wybierz WYŻEJ lub NIŻEJ dla następnego gracza", "Elige MAYOR o MENOR para el siguiente jugador");
            Add("Choose what your Ace counts as", "Wybierz, jako co liczy się Twój As", "Elige cuánto vale tu As");
            Add("No card can be played. Pick up the pile.", "Nie możesz zagrać żadnej karty. Weź stos.", "No puedes jugar ninguna carta. Recoge el montón.");
            Add("Your turn: tap a face-down card", "Twoja tura: dotknij zakrytej karty", "Tu turno: toca una carta boca abajo");
            Add("Your turn: play a face-up card ({0})", "Twoja tura: zagraj odkrytą kartę ({0})", "Tu turno: juega una carta boca arriba ({0})");
            Add("Your turn: play {0}", "Twoja tura: zagraj {0}", "Tu turno: juega {0}");
            Add("Hand sorted by rank", "Karty ułożone wg wartości", "Mano ordenada por valor");
            Add("Hand sorted by suit", "Karty ułożone wg koloru", "Mano ordenada por palo");
            Add("Next: HIGHER than 7", "Dalej: WYŻEJ niż 7", "Siguiente: MAYOR que 7");
            Add("Next: LOWER than 7", "Dalej: NIŻEJ niż 7", "Siguiente: MENOR que 7");
            Add("Ace counts as {0}", "As liczy się jako {0}", "El As vale {0}");
            Add("{0} left. A computer player takes over.", "{0} wyszedł. Gra za niego komputer.", "{0} se fue. Un jugador CPU ocupa su lugar.");

            // announcements
            Add("BURN!", "SPALONE!", "¡QUEMADO!");
            Add("SPLASH!", "PLUSK!", "¡SPLASH!");
            Add("SKIP", "POMINIĘCIE", "¡SALTO!");
            Add("SKIP x{0}", "POMINIĘCIE x{0}", "¡SALTO x{0}!");
            Add("REVERSE", "ZMIANA KIERUNKU", "CAMBIO DE SENTIDO");
            Add("DOUBLE REVERSE", "PODWÓJNA ZMIANA", "DOBLE CAMBIO");
            Add("RESET", "RESET", "REINICIO");
            Add("HIGHER than 7", "WYŻEJ niż 7", "MAYOR que 7");
            Add("LOWER than 7", "NIŻEJ niż 7", "MENOR que 7");
            Add("ACE = {0}", "AS = {0}", "AS = {0}");
            Add("YOUR TURN", "TWOJA TURA", "TU TURNO");
            Add("JACK", "WALET", "JOTA");
            Add("QUEEN", "DAMA", "REINA");
            Add("KING", "KRÓL", "REY");
            Add("ACE", "AS", "AS");
            Add("You pick up 1 card", "Bierzesz 1 kartę", "Recoges 1 carta");
            Add("You pick up {0} cards", "Bierzesz karty: {0}", "Recoges {0} cartas");
            Add("{0} picks up 1 card", "{0} bierze 1 kartę", "{0} recoge 1 carta");
            Add("{0} picks up {1} cards", "{0} bierze karty: {1}", "{0} recoge {1} cartas");
            Add("{0}. Take a moment...", "{0}. Chwila na zastanowienie...", "{0}. Tómate un momento...");
            Add("You're the Shedhead!", "Jesteś Shedheadem!", "¡Eres el Shedhead!");
            Add("You finish {0}!", "Kończysz na miejscu {0}!", "¡Terminas en {0} lugar!");
            Add("{0} is the Shedhead!", "{0} jest Shedheadem!", "¡{0} es el Shedhead!");
            Add("{0} finishes {1}!", "{0} kończy na miejscu {1}!", "¡{0} termina en {1} lugar!");

            // pile viewer + results
            Add("The pile is empty", "Stos jest pusty", "El montón está vacío");
            Add("The pile: 1 card", "Stos: 1 karta", "El montón: 1 carta");
            Add("The pile: {0} cards", "Stos: kart {0}", "El montón: {0} cartas");
            Add("Burned so far: {0}     Left in deck: {1}", "Spalone: {0}     W talii: {1}", "Quemadas: {0}     Quedan en el mazo: {1}");
            Add("Top card: {0}", "Na wierzchu: {0}", "Carta de arriba: {0}");
            Add("(counts as {0})", "(liczy się jako {0})", "(vale {0})");
            Add("Next card must be {0}", "Następna karta: {0}", "La siguiente debe ser {0}");
            Add("In the pile:", "W stosie:", "En el montón:");
            Add("Close", "Zamknij", "Cerrar");
            Add("You win!", "Wygrywasz!", "¡Has ganado!");
            Add("{0} wins!", "{0} wygrywa!", "¡{0} gana!");
            Add("{0} players", "Graczy: {0}", "{0} jugadores");
            Add("+{0} coins  (you have {1})", "+{0} monet  (masz {1})", "+{0} monedas  (tienes {1})");
            Add("Shedhead!", "Shedhead!", "¡Shedhead!");
            Add("Play Again", "Zagraj ponownie", "Jugar otra vez");
            Add("Waiting for the host...", "Czekanie na gospodarza...", "Esperando al anfitrión...");

            // rules engine messages
            Add("any card", "dowolną kartę", "cualquier carta");
            Add("{0} or higher", "{0} lub wyżej", "{0} o más");
            Add("Needs {0} or higher", "Potrzeba {0} lub wyżej", "Necesitas {0} o más");
            Add("Must be HIGHER than 7", "Musi być WYŻEJ niż 7", "Debe ser MAYOR que 7");
            Add("Must be LOWER than 7", "Musi być NIŻEJ niż 7", "Debe ser MENOR que 7");
            Add("Waiting for a choice to be made", "Czekanie na wybór", "Esperando una elección");
            Add("It isn't your turn", "To nie Twoja tura", "No es tu turno");
            Add("Face-down cards are turned over one at a time", "Zakryte karty odkrywa się po jednej", "Las cartas boca abajo se giran de una en una");
            Add("Select a card first", "Najpierw wybierz kartę", "Elige una carta primero");
            Add("Play the cards in your hand first", "Najpierw zagraj karty z ręki", "Primero juega las cartas de tu mano");
            Add("You have no face-up cards left", "Nie masz już odkrytych kart", "No te quedan cartas boca arriba");
            Add("That card isn't available", "Ta karta jest niedostępna", "Esa carta no está disponible");
            Add("Multiple cards must all be the same rank", "Kilka kart musi mieć tę samą wartość", "Varias cartas deben tener el mismo valor");
            Add("Play your other cards first", "Najpierw zagraj pozostałe karty", "Primero juega tus otras cartas");
            Add("No card there", "Tu nie ma karty", "Ahí no hay carta");
            Add("There is nothing to pick up", "Nie ma czego brać", "No hay nada que recoger");
            Add("Turn over a face-down card", "Odkryj zakrytą kartę", "Gira una carta boca abajo");
            Add("You have a card you can play", "Masz kartę, którą możesz zagrać", "Tienes una carta que puedes jugar");
            Add("No 7 to call", "Nie ma siódemki do ogłoszenia", "No hay ningún 7 que cantar");
            Add("Choose HIGHER or LOWER", "Wybierz WYŻEJ lub NIŻEJ", "Elige MAYOR o MENOR");
            Add("No Ace choice to make", "Nie ma wyboru Asa", "No hay que elegir el As");
            Add("The Ace can't burn right now", "As nie może teraz spalić stosu", "El As no puede quemar ahora");
            Add("The Ace can be: {0}", "As może być: {0}", "El As puede ser: {0}");
            Add("Face-down cards come last", "Zakryte karty gra się na końcu", "Las cartas boca abajo van al final");

            // ---------------------------------------------------------- how to play
            Add(MainMenuController.RulesText,
                "<b><color=#F2C24D>Cel</color></b>\nPozbądź się wszystkich kart. Gra toczy się, aż wszyscy skończą, więc liczy się każde miejsce od 1. do ostatniego.\n\n" +
                "<b><color=#F2C24D>Przygotowanie</color></b>\nKażdy dostaje 3 zakryte karty, 3 odkryte karty na nich i 3 karty do ręki. Reszta tworzy talię. Przy 6 lub więcej graczach używa się dwóch talii.\n\n" +
                "<b><color=#F2C24D>Twoja tura</color></b>\nZagraj kartę równą lub wyższą od karty na wierzchu stosu (2 najniższa, As najwyższy). Możesz zagrać kilka kart tej samej wartości naraz, ale nie musisz. Dopóki jest talia, dobieraj do 3 kart. Jeśli nie możesz zagrać, bierzesz cały stos.\n\n" +
                "<b><color=#F2C24D>Karty specjalne</color></b>\n" +
                "<b>2</b>  Reset. Można ją położyć na wszystko; następny gracz może zagrać dowolną kartę.\n" +
                "<b>7</b>  Wybierz WYŻEJ lub NIŻEJ. Następny gracz musi zagrać wyżej lub niżej niż 7.\n" +
                "<b>8</b>  Pominięcie. Każda 8 pomija jednego gracza (dwie 8 pomijają dwóch).\n" +
                "<b>10</b>  Spalenie. Usuwa stos z gry, a Ty grasz jeszcze raz.\n" +
                "<b>Dama</b>  Zmienia kierunek gry.\n" +
                "<b>As</b>  Wybierz, jako co się liczy: dowolna wartość równa lub wyższa od stosu (na Walecie może być W, D, K lub A, nigdy 9). Na NIŻEJ niż 7 może być 2-6. Albo użyj go jako 10, by spalić stos.\n\n" +
                "<b><color=#F2C24D>Trzy etapy</color></b>\n1. Graj z ręki, aż skończy się talia i opróżnisz rękę.\n" +
                "2. Potem zagraj odkryte karty.\n3. Na końcu zakryte karty, odkrywane po jednej bez patrzenia. Jeśli karty nie można zagrać, bierzesz z nią stos i musisz znowu opróżnić rękę.\n\n" +
                "<b><color=#F2C24D>Sterowanie</color></b>\nPrzeciągnij kartę na stos, aby ją zagrać. Albo dotknij kart, aby je wybrać (dotknij ponownie, aby odznaczyć) i naciśnij ZAGRAJ. Aby zagrać kilka kart tej samej wartości, dotknij wszystkich i przeciągnij jedną. Karty, których nie możesz zagrać, są przyciemnione; dotknij jednej, aby zobaczyć dlaczego. Strzałki wokół stołu pokazują kierunek gry. Dotknij stosu, aby zobaczyć wszystkie jego karty.\n\n" +
                "<b><color=#F2C24D>Klawiatura (PC)</color></b>\n1-9 wybiera kartę, Enter lub Spacja zagrywa, P bierze stos, H lub L wybiera wyżej lub niżej, J Q K A lub 2-9 ustala wartość Asa (B spala), S sortuje rękę, V pokazuje stos, F przyspiesza grę po Twoim końcu, Esc otwiera menu.",
                "<b><color=#F2C24D>Objetivo</color></b>\nQuédate sin cartas. La partida sigue hasta que todos terminan, así que cada puesto, del 1.º al último, cuenta.\n\n" +
                "<b><color=#F2C24D>Preparación</color></b>\nCada jugador recibe 3 cartas boca abajo, 3 boca arriba encima y 3 en la mano. El resto forma el mazo. Con 6 o más jugadores se usan dos barajas.\n\n" +
                "<b><color=#F2C24D>Tu turno</color></b>\nJuega una carta igual o mayor que la de arriba del montón (el 2 es la más baja, el As la más alta). Puedes jugar varias cartas del mismo valor a la vez, pero no es obligatorio. Mientras quede mazo, roba hasta tener 3 cartas. Si no puedes jugar, recoges todo el montón.\n\n" +
                "<b><color=#F2C24D>Cartas especiales</color></b>\n" +
                "<b>2</b>  Reinicio. Va sobre cualquier carta; el siguiente jugador puede jugar lo que quiera.\n" +
                "<b>7</b>  Canta MAYOR o MENOR. El siguiente jugador debe jugar más alto o más bajo que 7.\n" +
                "<b>8</b>  Salto. Cada 8 salta a un jugador (dos 8 saltan a dos).\n" +
                "<b>10</b>  Quemar. Retira el montón del juego y vuelves a jugar.\n" +
                "<b>Reina</b>  Cambia el sentido del juego.\n" +
                "<b>As</b>  Elige cuánto vale: cualquier valor que iguale o supere el montón (sobre una Jota puede ser J, Q, K o A, nunca 9). Sobre un 7 MENOR puede ser 2-6. O úsalo como un 10 para quemar el montón.\n\n" +
                "<b><color=#F2C24D>Las tres fases</color></b>\n1. Juega de tu mano hasta que se acabe el mazo y vacíes la mano.\n" +
                "2. Luego juega tus cartas boca arriba.\n3. Por último tus cartas boca abajo, girándolas de una en una sin mirar. Si la carta no se puede jugar, recoges el montón con ella y debes vaciar la mano otra vez.\n\n" +
                "<b><color=#F2C24D>Controles</color></b>\nArrastra una carta al montón para jugarla. O toca cartas para elegirlas (toca otra vez para quitarlas) y pulsa JUGAR. Para jugar varias del mismo valor, tócalas todas y arrastra una. Las cartas que no puedes jugar se ven oscuras; toca una para ver por qué. Las flechas alrededor de la mesa muestran el sentido del juego. Toca el montón para ver todas sus cartas.\n\n" +
                "<b><color=#F2C24D>Teclado (PC)</color></b>\n1-9 elige una carta, Enter o Espacio juega, P recoge el montón, H o L canta mayor o menor, J Q K A o 2-9 eligen el valor del As (B quema), S ordena tu mano, V muestra el montón, F acelera cuando ya has terminado, Esc abre el menú.");

            // Most specific patterns first, so "Needs J or higher" matches "Needs {0} or higher", not "{0} or higher".
            patterns.Sort((a, b) => (b.prefix.Length + b.suffix.Length).CompareTo(a.prefix.Length + a.suffix.Length));
        }
    }
}
