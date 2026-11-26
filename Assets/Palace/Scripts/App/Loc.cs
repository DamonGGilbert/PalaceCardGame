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
            Add("THE CLASSIC CARD GAME", "KLASYCZNA GRA KARCIANA", "EL CLÁSICO JUEGO DE CARTAS");
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
            Add("Game design, house rules and programming", "Projekt gry, zasady domowe i programowanie", "Diseño del juego, reglas de la casa y programación");
            Add("Additional programming, art and sound", "Dodatkowe programowanie, grafika i dźwięk", "Programación adicional, arte y sonido");
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
            Add("Small Bag of Coins", "Mały worek monet", "Bolsa pequeña de monedas");
            Add("Medium Bag of Coins", "Średni worek monet", "Bolsa mediana de monedas");
            Add("Big Bag of Coins", "Duży worek monet", "Bolsa grande de monedas");
            Add("2,000 coins", "2000 monet", "2.000 monedas");
            Add("8,000 coins", "8000 monet", "8.000 monedas");
            Add("20,000 coins: best value", "20 000 monet: najlepsza oferta", "20.000 monedas: la mejor oferta");
            Add("Royal Gold", "Królewskie złoto", "Oro real");
            Add("Royal", "Królewski", "Real");
            Add("card", "karta", "carta");
            Add("cards", "karty", "cartas");
            Add("FOUR OF A KIND!", "KARETA!", "¡PÓKER!");
            Add("Remove Ads", "Usuń reklamy", "Quitar anuncios");
            Add("Collect bonus coins without watching ads", "Odbieraj bonusowe monety bez reklam", "Consigue monedas extra sin ver anuncios");
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
            Add("Netherlands", "Holandia", "Países Bajos");
            Add("Belgium", "Belgia", "Bélgica");
            Add("Luxembourg", "Luksemburg", "Luxemburgo");
            Add("Austria", "Austria", "Austria");
            Add("Switzerland", "Szwajcaria", "Suiza");
            Add("Sweden", "Szwecja", "Suecia");
            Add("Norway", "Norwegia", "Noruega");
            Add("Denmark", "Dania", "Dinamarca");
            Add("Finland", "Finlandia", "Finlandia");
            Add("Iceland", "Islandia", "Islandia");
            Add("Greece", "Grecja", "Grecia");
            Add("Czechia", "Czechy", "Chequia");
            Add("Hungary", "Węgry", "Hungría");
            Add("Romania", "Rumunia", "Rumanía");
            Add("Bulgaria", "Bułgaria", "Bulgaria");
            Add("Ukraine", "Ukraina", "Ucrania");
            Add("Russia", "Rosja", "Rusia");
            Add("Lithuania", "Litwa", "Lituania");
            Add("Latvia", "Łotwa", "Letonia");
            Add("Estonia", "Estonia", "Estonia");
            Add("Armenia", "Armenia", "Armenia");
            Add("Turkey", "Turcja", "Turquía");
            Add("Japan", "Japonia", "Japón");
            Add("Vietnam", "Wietnam", "Vietnam");
            Add("India", "Indie", "India");
            Add("Nigeria", "Nigeria", "Nigeria");
            Add("Ivory Coast", "Wybrzeże Kości Słoniowej", "Costa de Marfil");
            Add("Mali", "Mali", "Malí");
            Add("Jamaica", "Jamajka", "Jamaica");
            Add("Colombia", "Kolumbia", "Colombia");
            Add("Peru", "Peru", "Perú");
            Add("Chile", "Chile", "Chile");

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

            // ---------------------------------------------------------- computer skill + guided game
            Add("Computer skill", "Poziom komputera", "Nivel del ordenador");
            Add("Easy", "Łatwy", "Fácil");
            Add("Hard", "Trudny", "Difícil");
            Add("Easy: relaxed computers that often waste their best cards.", "Łatwy: wyluzowani przeciwnicy, którzy często marnują najlepsze karty.", "Fácil: rivales tranquilos que a menudo malgastan sus mejores cartas.");
            Add("Normal: computers that play sensibly.", "Normalny: przeciwnicy grający rozsądnie.", "Normal: rivales que juegan con sentido.");
            Add("Hard: computers that save their best cards and target whoever is winning.", "Trudny: przeciwnicy oszczędzają najlepsze karty i celują w tego, kto wygrywa.", "Difícil: rivales que guardan sus mejores cartas y van a por quien va ganando.");
            Add("Guided game", "Gra z poradnikiem", "Partida guiada");
            Add("Play the guided game", "Zagraj z poradnikiem", "Jugar la partida guiada");
            Add("New to Shedhead?", "Pierwszy raz w Shedhead?", "¿Nuevo en Shedhead?");
            Add("Play a short guided game first. It explains every special card the moment it comes up.", "Zagraj najpierw krótką grę z poradnikiem. Wyjaśnia każdą kartę specjalną, gdy tylko się pojawi.", "Juega primero una partida guiada corta. Explica cada carta especial en cuanto aparece.");
            Add("Teach me", "Naucz mnie", "Enséñame");
            Add("I know how to play", "Umiem grać", "Ya sé jugar");
            Add("Guided game complete!", "Gra z poradnikiem ukończona!", "¡Partida guiada completada!");
            Add("Tip {0}", "Wskazówka {0}", "Consejo {0}");
            Add("Got it", "Jasne", "Entendido");
            Add("Skip tips", "Pomiń wskazówki", "Saltar consejos");
            Add("Welcome to Shedhead! The aim is to get rid of all your cards. The last player still holding cards is the Shedhead.",
                "Witaj w Shedhead! Celem jest pozbycie się wszystkich kart. Ostatni gracz, który wciąż ma karty, zostaje Shedheadem.",
                "¡Bienvenido a Shedhead! El objetivo es quedarte sin cartas. El último jugador que aún tenga cartas es el Shedhead.");
            Add("You have 3 cards in your hand, 3 face-up cards on the table and 3 face-down cards under them. Play your hand first, then your face-up cards, and your face-down cards last, without looking.",
                "Masz 3 karty w ręce, 3 odkryte karty na stole i 3 zakryte karty pod nimi. Najpierw graj z ręki, potem odkryte karty, a zakryte na końcu, bez patrzenia.",
                "Tienes 3 cartas en la mano, 3 boca arriba en la mesa y 3 boca abajo debajo de ellas. Juega primero la mano, luego las boca arriba y las boca abajo al final, sin mirar.");
            Add("Your turn! Play a card equal to or higher than the top card of the pile. Drag it onto the pile, or tap it and press PLAY. Cards you can't play are darkened.",
                "Twoja tura! Zagraj kartę równą lub wyższą od karty na wierzchu stosu. Przeciągnij ją na stos albo dotknij jej i naciśnij ZAGRAJ. Karty, których nie możesz zagrać, są przyciemnione.",
                "¡Tu turno! Juega una carta igual o mayor que la de arriba del montón. Arrástrala al montón, o tócala y pulsa JUGAR. Las cartas que no puedes jugar salen oscurecidas.");
            Add("You have more than one card of the same rank. Tap them all, then press PLAY, to get rid of them together.",
                "Masz więcej niż jedną kartę tej samej wartości. Dotknij wszystkich i naciśnij ZAGRAJ, aby pozbyć się ich naraz.",
                "Tienes más de una carta del mismo valor. Tócalas todas y pulsa JUGAR para soltarlas juntas.");
            Add("While there are cards in the deck, you draw back up to 3 cards after your turn.",
                "Dopóki w talii są karty, po swojej turze dobierasz do 3 kart.",
                "Mientras queden cartas en el mazo, robas hasta tener 3 después de tu turno.");
            Add("2 is a reset card. It can go on anything, and the next player can then play any card.",
                "2 to karta resetu. Można ją położyć na wszystko, a następny gracz może wtedy zagrać dowolną kartę.",
                "El 2 reinicia. Se puede poner sobre cualquier carta, y el siguiente jugador puede jugar lo que quiera.");
            Add("7: whoever plays it calls HIGHER or LOWER. The next player must then play higher than 7, or lower than 7.",
                "7: kto ją zagra, wybiera WYŻEJ lub NIŻEJ. Następny gracz musi zagrać kartę wyższą lub niższą niż 7.",
                "7: quien lo juega elige MAYOR o MENOR. El siguiente jugador debe jugar más alto que 7, o más bajo que 7.");
            Add("8 skips the next player. Two 8s together skip two players. With only 2 players left, an 8 is a normal card.",
                "8 pomija następnego gracza. Dwie 8 naraz pomijają dwóch graczy. Gdy zostało tylko 2 graczy, 8 jest zwykłą kartą.",
                "El 8 salta al siguiente jugador. Dos 8 juntos saltan a dos jugadores. Si solo quedan 2 jugadores, el 8 es una carta normal.");
            Add("10 burns the pile: it's removed from the game and the same player goes again. A 10 can go on anything (except after LOWER on a 7).",
                "10 spala stos: znika z gry, a ten sam gracz gra jeszcze raz. 10 można położyć na wszystko (oprócz NIŻEJ na 7).",
                "El 10 quema el montón: sale del juego y el mismo jugador vuelve a jugar. El 10 va sobre cualquier carta (salvo tras MENOR en un 7).");
            Add("A Queen reverses the direction of play. The arrows around the table show which way it's going.",
                "Dama zmienia kierunek gry. Strzałki wokół stołu pokazują, w którą stronę idzie gra.",
                "La Reina invierte el sentido del juego. Las flechas alrededor de la mesa muestran hacia dónde va.");
            Add("An Ace is the highest card. When you play one you choose what it counts as (any value that matches or beats the pile), or use it as a 10 to burn the pile.",
                "As to najwyższa karta. Gdy go zagrasz, wybierasz, jako co się liczy (dowolna wartość równa lub wyższa od stosu), albo używasz go jako 10, by spalić stos.",
                "El As es la carta más alta. Al jugarlo eliges qué vale (cualquier valor igual o mayor que el montón), o lo usas como un 10 para quemar el montón.");
            Add("None of your cards can go on the pile, so you have to pick it all up. Press PICK UP PILE. It happens to everyone!",
                "Żadna z Twoich kart nie pasuje na stos, więc musisz go wziąć. Naciśnij WEŹ STOS. Każdemu się to zdarza!",
                "Ninguna de tus cartas vale para el montón, así que tienes que recogerlo. Pulsa RECOGER MONTÓN. ¡Le pasa a todo el mundo!");
            Add("Your hand is empty and the deck has run out, so now you play your face-up cards. Same rules as before.",
                "Masz pustą rękę, a talia się skończyła, więc teraz grasz odkrytymi kartami. Zasady te same co wcześniej.",
                "Tienes la mano vacía y el mazo se ha acabado, así que ahora juegas tus cartas boca arriba. Mismas reglas que antes.");
            Add("Last stage! Tap one of your face-down cards to turn it over. If it can't go on the pile, you pick up the pile along with it.",
                "Ostatni etap! Dotknij jednej z zakrytych kart, aby ją odkryć. Jeśli nie pasuje na stos, bierzesz stos razem z nią.",
                "¡Última fase! Toca una de tus cartas boca abajo para darle la vuelta. Si no vale para el montón, recoges el montón junto con ella.");
            Add("Tip: tap the pile (or View pile) to see every card in it and plan your next move.",
                "Wskazówka: dotknij stosu (lub Pokaż stos), aby zobaczyć wszystkie jego karty i zaplanować ruch.",
                "Consejo: toca el montón (o Ver montón) para ver todas sus cartas y planear tu jugada.");
            Add("A player has run out of cards and finished! The game carries on until only one player still has cards.",
                "Gracz pozbył się kart i skończył! Gra toczy się dalej, aż karty zostaną tylko jednemu graczowi.",
                "¡Un jugador se ha quedado sin cartas y ha terminado! La partida sigue hasta que solo un jugador tenga cartas.");

            // ---------------------------------------------------------- progress: levels, banners, results
            Add("Progress", "Postępy", "Progreso");
            Add("Daily", "Dzienne", "Diarios");
            Add("Achievements", "Osiągnięcia", "Logros");
            Add("Stats", "Statystyki", "Estadísticas");
            Add("Level {0}", "Poziom {0}", "Nivel {0}");
            Add("Level {0}!", "Poziom {0}!", "¡Nivel {0}!");
            Add("+{0} XP", "+{0} PD", "+{0} XP");
            Add("{0} / {1} XP", "{0} / {1} PD", "{0} / {1} XP");
            Add("Achievement unlocked!", "Odblokowano osiągnięcie!", "¡Logro desbloqueado!");
            Add("Daily challenge complete!", "Wyzwanie dnia ukończone!", "¡Reto diario completado!");
            Add("All 3 daily challenges done!", "Wszystkie 3 wyzwania dnia ukończone!", "¡Los 3 retos diarios completados!");
            Add("All daily challenges done. New ones tomorrow!", "Wszystkie wyzwania dnia ukończone. Nowe jutro!", "Retos diarios completados. ¡Mañana hay nuevos!");
            Add("1 daily challenge left today", "Zostało 1 wyzwanie na dziś", "Te queda 1 reto diario hoy");
            Add("{0} daily challenges left today", "Wyzwania na dziś do zrobienia: {0}", "Retos diarios pendientes hoy: {0}");
            Add("Day streak: {0}", "Seria dni: {0}", "Racha de días: {0}");
            Add("Play every day for a bigger daily reward: {0} coins tomorrow.", "Graj codziennie, a dzienna nagroda rośnie: jutro {0} monet.", "Juega cada día para ganar más: mañana {0} monedas.");
            Add("Done!", "Zrobione!", "¡Hecho!");
            Add("Unlocked", "Odblokowane", "Desbloqueado");
            Add("{0} of {1} unlocked", "Odblokowano {0} z {1}", "{0} de {1} desbloqueados");
            Add("Daily bonus collected. Come back tomorrow for new challenges!", "Dzienny bonus odebrany. Wróć jutro po nowe wyzwania!", "Bonus diario conseguido. ¡Vuelve mañana para nuevos retos!");
            Add("Finish all 3 for a bonus: +{0} coins and +{1} XP", "Ukończ wszystkie 3, a dostaniesz bonus: +{0} monet i +{1} PD", "Completa los 3 para un bonus: +{0} monedas y +{1} XP");
            Add("New challenges in {0}h {1}m", "Nowe wyzwania za {0} godz. {1} min", "Nuevos retos en {0} h {1} min");
            Add("Daily reward", "Dzienna nagroda", "Recompensa diaria");
            Add("Welcome back!", "Witaj ponownie!", "¡Bienvenido de nuevo!");
            Add("Day {0} in a row!", "Dzień {0} z rzędu!", "¡{0} días seguidos!");
            Add("Come back tomorrow for {0} coins.", "Wróć jutro po {0} monet.", "Vuelve mañana para conseguir {0} monedas.");
            Add("Collect", "Odbierz", "Recoger");

            // ---------------------------------------------------------- daily challenges
            Add("Play 3 games", "Zagraj 3 gry", "Juega 3 partidas");
            Add("Win a game", "Wygraj grę", "Gana una partida");
            Add("Win 2 games", "Wygraj 2 gry", "Gana 2 partidas");
            Add("Burn the pile 3 times", "Spal stos 3 razy", "Quema el montón 3 veces");
            Add("Skip 3 players with 8s", "Pomiń 3 graczy ósemkami", "Salta a 3 jugadores con ochos");
            Add("Reverse play twice with a Queen", "Zmień kierunek gry Damą 2 razy", "Invierte el sentido con una Reina 2 veces");
            Add("Play 2 or more cards at once 4 times", "Zagraj 2 lub więcej kart naraz 4 razy", "Juega 2 o más cartas a la vez 4 veces");
            Add("Finish in the top half twice", "Skończ w górnej połowie 2 razy", "Termina en la mitad de arriba 2 veces");
            Add("Finish a game without picking up the pile", "Skończ grę bez brania stosu", "Termina una partida sin recoger el montón");
            Add("Play a 7 and make the call 3 times", "Zagraj 7 i wybierz kierunek 3 razy", "Juega un 7 y elige 3 veces");
            Add("Burn the pile with an Ace", "Spal stos Asem", "Quema el montón con un As");
            Add("Finish a game with 5 or more players", "Skończ grę z 5 lub więcej graczami", "Termina una partida con 5 o más jugadores");
            Add("Beat the Hard computers", "Pokonaj trudnych przeciwników", "Gana a los rivales difíciles");
            Add("Play 5 special cards (2, 7, 8, 10, Q or A)", "Zagraj 5 kart specjalnych (2, 7, 8, 10, D lub A)", "Juega 5 cartas especiales (2, 7, 8, 10, R o A)");

            // ---------------------------------------------------------- achievements
            Add("Class Dismissed", "Lekcja zaliczona", "Clase terminada");
            Add("Finish the guided game.", "Ukończ grę z poradnikiem.", "Termina la partida guiada.");
            Add("Winner Winner", "Zwycięzca", "Ganador, ganador");
            Add("Win your first game.", "Wygraj pierwszą grę.", "Gana tu primera partida.");
            Add("Card Shark", "Karciany rekin", "Tiburón de las cartas");
            Add("Win 10 games.", "Wygraj 10 gier.", "Gana 10 partidas.");
            Add("Top Dog", "Szef stołu", "El jefe");
            Add("Win 50 games.", "Wygraj 50 gier.", "Gana 50 partidas.");
            Add("Regular", "Stały bywalec", "Habitual");
            Add("Play 10 games.", "Zagraj 10 gier.", "Juega 10 partidas.");
            Add("Pub Legend", "Legenda pubu", "Leyenda del bar");
            Add("Play 50 games.", "Zagraj 50 gier.", "Juega 50 partidas.");
            Add("Lifer", "Weteran", "Veterano");
            Add("Play 200 games.", "Zagraj 200 gier.", "Juega 200 partidas.");
            Add("Pyromaniac", "Piroman", "Pirómano");
            Add("Burn the pile 25 times.", "Spal stos 25 razy.", "Quema el montón 25 veces.");
            Add("Inferno", "Inferno", "Infierno");
            Add("Burn the pile 100 times.", "Spal stos 100 razy.", "Quema el montón 100 veces.");
            Add("On Fire", "W ogniu", "En racha");
            Add("Win 3 games in a row.", "Wygraj 3 gry z rzędu.", "Gana 3 partidas seguidas.");
            Add("Unstoppable", "Nie do zatrzymania", "Imparable");
            Add("Win 5 games in a row.", "Wygraj 5 gier z rzędu.", "Gana 5 partidas seguidas.");
            Add("Crowd Pleaser", "Ulubieniec tłumu", "El favorito del público");
            Add("Win a game with 6 or more players.", "Wygraj grę z 6 lub więcej graczami.", "Gana una partida con 6 o más jugadores.");
            Add("Full House", "Pełny stół", "Mesa llena");
            Add("Win a 10-player game.", "Wygraj grę z 10 graczami.", "Gana una partida de 10 jugadores.");
            Add("Giant Slayer", "Pogromca gigantów", "Matagigantes");
            Add("Win against Hard computers.", "Wygraj z trudnymi przeciwnikami.", "Gana a los rivales difíciles.");
            Add("Boss Level", "Poziom boss", "Nivel jefe");
            Add("Win 10 games against Hard computers.", "Wygraj 10 gier z trudnymi przeciwnikami.", "Gana 10 partidas a los rivales difíciles.");
            Add("Untouchable", "Nietykalny", "Intocable");
            Add("Win without picking up the pile once.", "Wygraj, ani razu nie biorąc stosu.", "Gana sin recoger el montón ni una vez.");
            Add("Comeback Kid", "Mistrz powrotów", "Rey de la remontada");
            Add("Win after picking up 10 or more cards at once.", "Wygraj po wzięciu naraz 10 lub więcej kart.", "Gana después de recoger 10 o más cartas de golpe.");
            Add("Hoarder", "Zbieracz", "Acaparador");
            Add("Pick up 15 or more cards at once.", "Weź naraz 15 lub więcej kart.", "Recoge 15 o más cartas de golpe.");
            Add("Skip Day", "Dzień wolny", "Día libre");
            Add("Skip 3 players with one play.", "Pomiń 3 graczy jednym ruchem.", "Salta a 3 jugadores en una jugada.");
            Add("Four of a Kind", "Kareta", "Póker");
            Add("Play four cards of the same rank at once.", "Zagraj naraz cztery karty tej samej wartości.", "Juega cuatro cartas del mismo valor a la vez.");
            Add("Social Butterfly", "Dusza towarzystwa", "Alma de la fiesta");
            Add("Finish a multiplayer game.", "Ukończ grę wieloosobową.", "Termina una partida multijugador.");
            Add("Party Animal", "Imprezowicz", "Fiestero");
            Add("Finish 10 multiplayer games.", "Ukończ 10 gier wieloosobowych.", "Termina 10 partidas multijugador.");
            Add("Wear the Crown", "Korona dla Ciebie", "Lleva la corona");
            Add("Be the Shedhead for the first time.", "Zostań Shedheadem po raz pierwszy.", "Sé el Shedhead por primera vez.");
            Add("Daily Grind", "Codzienna harówka", "Rutina diaria");
            Add("Complete 10 daily challenges.", "Ukończ 10 wyzwań dnia.", "Completa 10 retos diarios.");
            Add("Week Streak", "Tydzień z rzędu", "Semana seguida");
            Add("Play 7 days in a row.", "Graj 7 dni z rzędu.", "Juega 7 días seguidos.");
            Add("Double Digits", "Dwucyfrowy", "Doble dígito");
            Add("Reach level 10.", "Osiągnij poziom 10.", "Llega al nivel 10.");
            Add("Card Thrower", "Rzucacz kart", "Lanzacartas");
            Add("Play 1,000 cards.", "Zagraj 1000 kart.", "Juega 1000 cartas.");

            // ---------------------------------------------------------- stats
            Add("Games played", "Rozegrane gry", "Partidas jugadas");
            Add("Wins", "Wygrane", "Victorias");
            Add("Win rate", "Odsetek wygranych", "Porcentaje de victorias");
            Add("Times you were the Shedhead", "Ile razy byłeś Shedheadem", "Veces que fuiste el Shedhead");
            Add("Current win streak", "Obecna seria wygranych", "Racha de victorias actual");
            Add("Best win streak", "Najlepsza seria wygranych", "Mejor racha de victorias");
            Add("Wins against Hard computers", "Wygrane z trudnymi przeciwnikami", "Victorias contra rivales difíciles");
            Add("Multiplayer games", "Gry wieloosobowe", "Partidas multijugador");
            Add("Cards played", "Zagrane karty", "Cartas jugadas");
            Add("Piles burned", "Spalone stosy", "Montones quemados");
            Add("Times you picked up", "Ile razy brałeś stos", "Veces que recogiste");
            Add("Biggest pick-up", "Największy wzięty stos", "Mayor montón recogido");
            Add("Players skipped", "Pominięci gracze", "Jugadores saltados");
            Add("Daily challenges done", "Ukończone wyzwania dnia", "Retos diarios completados");
            Add("Best day streak", "Najdłuższa seria dni", "Mejor racha de días");

            // ---------------------------------------------------------- quick reactions
            Add("React", "Reaguj", "Reaccionar");
            Add("Reactions", "Reakcje", "Reacciones");
            Add("Nice one!", "Niezłe!", "¡Buena esa!");
            Add("Well played!", "Dobrze zagrane!", "¡Bien jugado!");
            Add("Unlucky!", "Pech!", "¡Mala suerte!");
            Add("Ouch, sorry!", "Auć, sorki!", "¡Uy, perdona!");
            Add("Good game!", "Dobra gra!", "¡Buena partida!");
            Add("So close!", "Było blisko!", "¡Casi!");
            Add("Big brain move!", "Ale mózg!", "¡Jugada de genio!");
            Add("You're on fire!", "Ale jesteś w formie!", "¡Estás que lo petas!");
            Add("Not the pile!", "Tylko nie stos!", "¡El montón no!");
            Add("Respect!", "Szacun!", "¡Respeto!");
            Add("Hehe, oops!", "Hehe, ups!", "¡Jeje, ups!");
            Add("My turn to shine!", "Teraz moja kolej!", "¡Ahora me toca brillar!");
            Add("4 colours: diamonds are blue and clubs are green, so every suit stands out. Reactions Off hides everyone's quick reactions.",
                "4 kolory: karo są niebieskie, a trefle zielone, więc każdy kolor się wyróżnia. Reakcje wyłączone ukrywają szybkie reakcje wszystkich graczy.",
                "4 colores: los diamantes son azules y los tréboles verdes, así cada palo destaca. Con Reacciones en No se ocultan las reacciones rápidas de todos.");

            // ---------------------------------------------------------- swap before play
            Add("Swap before play", "Zamiana przed grą", "Intercambio antes de jugar");
            Add("READY", "GOTOWE", "LISTO");
            Add("Swap cards: tap a hand card, then a face-up card. Press READY when you're happy.",
                "Zamień karty: dotknij karty w ręce, a potem odkrytej karty. Naciśnij GOTOWE, gdy skończysz.",
                "Cambia cartas: toca una carta de la mano y luego una boca arriba. Pulsa LISTO cuando quieras.");
            Add("Now tap a face-up card to swap with it", "Teraz dotknij odkrytej karty, aby ją zamienić", "Ahora toca una carta boca arriba para cambiarla");
            Add("Now tap a card in your hand to swap with it", "Teraz dotknij karty w ręce, aby ją zamienić", "Ahora toca una carta de tu mano para cambiarla");
            Add("Waiting for 1 player to be ready...", "Czekamy, aż 1 gracz będzie gotowy...", "Esperando a que 1 jugador esté listo...");
            Add("Waiting for {0} players to be ready...", "Czekamy na gotowość graczy: {0}...", "Esperando a que {0} jugadores estén listos...");
            Add("{0} is ready", "{0} jest gotowy", "{0} está listo");
            Add("Swapping is over", "Zamiana się skończyła", "El intercambio ha terminado");

            // ---------------------------------------------------------- game modes
            Add("Game mode", "Tryb gry", "Modo de juego");
            Add("Change", "Zmień", "Cambiar");
            Add("Done", "Gotowe", "Hecho");
            Add("Custom", "Własny", "Personalizado");
            Add("Big Battle", "Wielka bitwa", "Gran batalla");
            Add("Strategy", "Strategia", "Estrategia");
            Add("Chaos", "Chaos", "Caos");
            Add("Auto", "Auto", "Auto");
            Add("Decks", "Talie", "Barajas");
            Add("Cards in hand", "Karty w ręce", "Cartas en la mano");
            Add("Four of a kind burns", "Czwórka pali stos", "Cuatro iguales queman");
            Add("1 deck", "1 talia", "1 baraja");
            Add("{0} decks", "talie: {0}", "{0} barajas");
            Add("{0} cards in hand", "karty w ręce: {0}", "{0} cartas en la mano");
            Add("swap first", "najpierw zamiana", "intercambio primero");
            Add("no four of a kind burns", "czwórka nie pali", "cuatro iguales no queman");
            Add("{0} players, 1 deck ({1} cards)", "Graczy: {0}, 1 talia ({1} kart)", "{0} jugadores, 1 baraja ({1} cartas)");
            Add("{0} players, {1} decks ({2} cards)", "Graczy: {0}, talie: {1} ({2} kart)", "{0} jugadores, {1} barajas ({2} cartas)");
            Add("Custom: your own mix of the options below.", "Własny: twoje połączenie opcji poniżej.", "Personalizado: tu propia mezcla de las opciones de abajo.");
            Add("The school rules: one deck (two for big tables), 3 cards in hand.",
                "Zasady ze szkoły: jedna talia (dwie przy dużym stole), 3 karty w ręce.",
                "Las reglas del colegio: una baraja (dos en mesas grandes), 3 cartas en la mano.");
            Add("Two decks shuffled together: a longer game with twice the 10s and 2s, and more four of a kinds.",
                "Dwie potasowane talie: dłuższa gra z dwa razy większą liczbą dziesiątek i dwójek oraz częstszymi czwórkami.",
                "Dos barajas mezcladas: una partida más larga con el doble de 10 y 2, y más cuatro iguales.");
            Add("Swap before play, and 4 cards in hand for more choice every turn.",
                "Zamiana przed grą i 4 karty w ręce, czyli większy wybór w każdej turze.",
                "Intercambio antes de jugar y 4 cartas en la mano para elegir más en cada turno.");
            Add("Three decks, 5 cards in hand and Swap before play. Anything can happen!",
                "Trzy talie, 5 kart w ręce i zamiana przed grą. Wszystko może się zdarzyć!",
                "Tres barajas, 5 cartas en la mano e intercambio antes de jugar. ¡Puede pasar de todo!");
            Add("Four cards of the same rank in a row on top of the pile burn it, even if different players put them there.\n\n" +
                "Whoever completes the four goes again, just like after a 10.\n\n" +
                "Turn it off for longer games where only 10s burn the pile.",
                "Cztery karty tej samej wartości z rzędu na wierzchu stosu palą go, nawet jeśli położyli je różni gracze.\n\n" +
                "Kto dołoży czwartą kartę, gra jeszcze raz, tak jak po 10.\n\n" +
                "Wyłącz to, by grać dłużej: wtedy stos palą tylko dziesiątki.",
                "Cuatro cartas del mismo valor seguidas encima del montón lo queman, aunque las pusieran jugadores distintos.\n\n" +
                "Quien completa los cuatro vuelve a jugar, igual que tras un 10.\n\n" +
                "Desactívalo para partidas más largas en las que solo los 10 queman el montón.");
            Add("How many 52-card decks are shuffled together. Auto uses one deck, or two for 6 or more players.\n\n" +
                "More decks mean a longer game with more 10s, 2s and Aces, and four of a kinds come up more often.\n\n" +
                "If the table needs more cards for the deal, another deck is added automatically.",
                "Ile 52-kartowych talii tasujemy razem. Auto to jedna talia albo dwie przy 6 lub więcej graczach.\n\n" +
                "Więcej talii to dłuższa gra z większą liczbą dziesiątek, dwójek i asów, a czwórki zdarzają się częściej.\n\n" +
                "Jeśli do rozdania potrzeba więcej kart, kolejna talia dochodzi sama.",
                "Cuántas barajas de 52 cartas se mezclan. Auto usa una baraja, o dos con 6 jugadores o más.\n\n" +
                "Más barajas significan una partida más larga con más 10, 2 y ases, y los cuatro iguales salen más a menudo.\n\n" +
                "Si la mesa necesita más cartas para repartir, se añade otra baraja automáticamente.");
            Add("No such player", "Nie ma takiego gracza", "Ese jugador no existe");
            Add("You've already pressed Ready", "Już naciśnięto Gotowe", "Ya has pulsado Listo");

            Add(UIKit.SwapInfoText,
                "Opcjonalny tryb gry. Po rozdaniu, a przed pierwszą turą, każdy może zamieniać karty między ręką a odkrytymi kartami.\n\n" +
                "Dotknij karty w ręce, a potem jednej z odkrytych kart, aby je zamienić. Naciśnij GOTOWE, gdy skończysz. Gra zaczyna się, gdy wszyscy są gotowi.\n\n" +
                "Wskazówka: mocne karty (10, 2, Asy i wysokie karty) połóż odkryte na koniec gry, a niskie zostaw w ręce, by zagrać je wcześnie.",
                "Un modo de juego opcional. Tras el reparto, y antes del primer turno, todos pueden cambiar cartas entre su mano y sus cartas boca arriba.\n\n" +
                "Toca una carta de tu mano y luego una de tus cartas boca arriba para cambiarlas. Pulsa LISTO cuando quieras. La partida empieza cuando todos están listos.\n\n" +
                "Consejo: pon las cartas fuertes (10, 2, Ases y cartas altas) boca arriba para el final, y guarda las bajas en la mano para jugarlas pronto.");

            // ---------------------------------------------------------- rematch
            Add("Rematch", "Rewanż", "Revancha");
            Add("Rematch:", "Rewanż:", "Revancha:");
            Add("Ask for a rematch", "Poproś o rewanż", "Pedir la revancha");
            Add("Asked!", "Wysłano!", "¡Pedida!");
            Add("{0} wants a rematch!", "{0} chce rewanżu!", "¡{0} quiere la revancha!");
            Add("Play a game", "Zagraj grę", "Jugar una partida");

            // ---------------------------------------------------------- streak reminders
            Add("Daily reminder", "Codzienne przypomnienie", "Recordatorio diario");
            Add("Want a reminder tomorrow so you keep your streak?", "Przypomnieć Ci jutro, żebyś nie stracił serii?", "¿Te lo recordamos mañana para que no pierdas la racha?");
            Add("Remind me", "Przypomnij mi", "Recuérdamelo");
            Add("No thanks", "Nie, dzięki", "No, gracias");
            Add("Notifications are off", "Powiadomienia są wyłączone", "Las notificaciones están desactivadas");
            Add("Shedhead isn't allowed to send notifications. You can allow them in your phone's settings.",
                "Shedhead nie może wysyłać powiadomień. Możesz na nie zezwolić w ustawieniach telefonu.",
                "Shedhead no tiene permiso para enviar notificaciones. Puedes permitirlas en los ajustes del móvil.");
            Add("OK", "OK", "Vale");
            Add("Your daily reward is waiting. Fancy a quick game?", "Czeka na Ciebie dzienna nagroda. Szybka partyjka?", "Tu recompensa diaria te espera. ¿Una partida rápida?");
            Add("Keep your streak going! Day {0} pays {1} coins.", "Nie przerywaj serii! Dzień {0} to {1} monet.", "¡Sigue la racha! El día {0} da {1} monedas.");

            // ---------------------------------------------------------- face-up pick-up (Q11)
            Add("No card can be played. Tap a face-up card to take it with the pile.", "Nie możesz nic zagrać. Dotknij odkrytej karty, aby wziąć ją razem ze stosem.", "No puedes jugar ninguna. Toca una carta boca arriba para llevártela con el montón.");
            Add("Tap the face-up card you want to take with the pile", "Dotknij odkrytej karty, którą chcesz wziąć ze stosem", "Toca la carta boca arriba que quieres llevarte con el montón");
            Add("Choose a face-up card to take with the pile", "Wybierz odkrytą kartę do wzięcia ze stosem", "Elige una carta boca arriba para llevártela con el montón");
            Add("None of your face-up cards can go on the pile. Tap the one you want to take into your hand, and the whole pile comes with it.",
                "Żadnej odkrytej karty nie można położyć na stos. Dotknij tej, którą chcesz wziąć do ręki, a cały stos przyjdzie razem z nią.",
                "Ninguna de tus cartas boca arriba vale para el montón. Toca la que quieras llevarte a la mano y el montón entero irá con ella.");

            // ---------------------------------------------------------- profile pictures
            Add("Picture", "Zdjęcie", "Foto");
            Add("Your initial", "Twój inicjał", "Tu inicial");
            Add("Unlocks at level {0}", "Odblokowanie na poziomie {0}", "Se desbloquea en el nivel {0}");
            Add("You're level {0}. Play games to level up and unlock more pictures.", "Masz poziom {0}. Graj, by awansować i odblokować więcej zdjęć.", "Estás en el nivel {0}. Juega para subir de nivel y desbloquear más fotos.");
            Add("Spade", "Pik", "Picas");
            Add("Heart", "Kier", "Corazón");
            Add("Diamond", "Karo", "Diamante");
            Add("Club", "Trefl", "Trébol");
            Add("Star", "Gwiazda", "Estrella");
            Add("Smile", "Uśmiech", "Sonrisa");
            Add("Flame", "Płomień", "Llama");
            Add("Lightning", "Błyskawica", "Rayo");
            Add("Moon", "Księżyc", "Luna");
            Add("Sun", "Słońce", "Sol");
            Add("Cat", "Kot", "Gato");
            Add("Ghost", "Duszek", "Fantasmita");
            Add("Clover", "Koniczyna", "Trébol de la suerte");
            Add("Crown", "Korona", "Corona");
            Add("Rainbow", "Tęcza", "Arcoíris");

            // ---------------------------------------------------------- fonts, effects, win celebrations
            Add("Font", "Czcionka", "Fuente");
            Add("Win", "Wygrana", "Victoria");
            Add("Classic", "Klasyczna", "Clásica");
            Add("Typewriter", "Maszyna do pisania", "Máquina de escribir");
            Add("Marker", "Flamaster", "Rotulador");
            Add("Pixel", "Piksel", "Píxel");
            Add("Gold Rush", "Gorączka złota", "Fiebre del oro");
            Add("Galaxy", "Galaktyka", "Galaxia");
            Add("Toxic", "Toksyczny", "Tóxico");
            Add("Party", "Impreza", "Fiesta");
            Add("Shadow", "Cień", "Sombra");
            Add("Confetti", "Konfetti", "Confeti");
            Add("Fireworks", "Fajerwerki", "Fuegos artificiales");
            Add("Coin Shower", "Deszcz monet", "Lluvia de monedas");
            Add("Starburst", "Wybuch gwiazd", "Estallido de estrellas");
            Add("Bubbles", "Bańki", "Burbujas");
            Add("Name fonts: Google Fonts (SIL Open Font License and Apache License 2.0)",
                "Czcionki imion: Google Fonts (licencje SIL Open Font License i Apache 2.0)",
                "Fuentes de nombres: Google Fonts (licencias SIL Open Font License y Apache 2.0)");

            // ---------------------------------------------------------- premium range
            Add("PREMIUM", "PREMIUM", "PREMIUM");
            Add("Carbon Fibre", "Włókno węglowe", "Fibra de carbono");
            Add("Neon Grid", "Neonowa siatka", "Rejilla de neón");
            Add("Marble", "Marmur", "Mármol");
            Add("Gold Filigree", "Złota filigrana", "Filigrana de oro");
            Add("Royal Damask", "Królewski adamaszek", "Damasco real");
            Add("Dragon Scale", "Smocza łuska", "Escamas de dragón");
            Add("Holographic", "Holograficzny", "Holográfico");
            Add("Marble Hall", "Marmurowa sala", "Salón de mármol");
            Add("Royal Velvet", "Królewski aksamit", "Terciopelo real");
            Add("Neon Arcade", "Neonowy salon gier", "Salón recreativo neón");
            Add("Royal Ivory", "Królewska kość słoniowa", "Marfil real");
            Add("Obsidian Gold", "Obsydian i złoto", "Obsidiana y oro");
            Add("Rose Quartz", "Kwarc różowy", "Cuarzo rosa");
            Add("Emerald Prestige", "Szmaragdowy prestiż", "Prestigio esmeralda");
            Add("Neon Noir", "Neonowa noc", "Neón nocturno");

            // ---------------------------------------------------------- how to play
            Add(MainMenuController.RulesText,
                "<b><color=#F2C24D>Cel</color></b>\nPozbądź się wszystkich kart. Gra toczy się, aż wszyscy skończą, więc liczy się każde miejsce od 1. do ostatniego.\n\n" +
                "<b><color=#F2C24D>Przygotowanie</color></b>\nKażdy dostaje 3 zakryte karty, 3 odkryte karty na nich i 3 karty do ręki. Reszta tworzy talię. Przy 6 lub więcej graczach używa się dwóch talii.\n\n" +
                "<b><color=#F2C24D>Twoja tura</color></b>\nZagraj kartę równą lub wyższą od karty na wierzchu stosu (2 najniższa, As najwyższy). Możesz zagrać kilka kart tej samej wartości naraz, ale nie musisz. Dopóki jest talia, dobieraj do 3 kart. Jeśli nie możesz zagrać, bierzesz cały stos.\n\n" +
                "<b><color=#F2C24D>Karty specjalne</color></b>\n" +
                "<b>2</b>  Reset. Można ją położyć na wszystko; następny gracz może zagrać dowolną kartę.\n" +
                "<b>7</b>  Wybierz WYŻEJ lub NIŻEJ. Następny gracz musi zagrać wyżej lub niżej niż 7.\n" +
                "<b>8</b>  Pominięcie. Każda 8 pomija jednego gracza (dwie 8 pomijają dwóch). Gdy zostało tylko 2 graczy, 8 jest zwykłą kartą.\n" +
                "<b>10</b>  Spalenie. Usuwa stos z gry, a Ty grasz jeszcze raz.\n" +
                "<b>Dama</b>  Zmienia kierunek gry.\n" +
                "<b>As</b>  Wybierz, jako co się liczy: dowolna wartość równa lub wyższa od stosu (na Walecie może być W, D, K lub A, nigdy 9). Na NIŻEJ niż 7 może być 2-6. Albo użyj go jako 10, by spalić stos.\n" +
                "<b>Kareta</b>  Cztery karty tej samej wartości z rzędu na stosie (zagrane razem albo po kolei przez różnych graczy) spalają go, a kto dołożył czwartą, gra jeszcze raz.\n\n" +
                "<b><color=#F2C24D>Trzy etapy</color></b>\n1. Graj z ręki, aż skończy się talia i opróżnisz rękę.\n" +
                "2. Potem zagraj odkryte karty. Jeśli żadnej nie możesz zagrać, bierzesz stos i jedną wybraną odkrytą kartę do ręki.\n3. Na końcu zakryte karty, odkrywane po jednej bez patrzenia. Jeśli karty nie można zagrać, bierzesz z nią stos i musisz znowu opróżnić rękę.\n\n" +
                "<b><color=#F2C24D>Opcjonalnie: Zamiana przed grą</color></b>\nWłącz ją w trybie Jeden gracz (lub jako gospodarz pokoju). Po rozdaniu możesz zamieniać karty z ręki z odkrytymi kartami: dotknij karty w ręce, a potem odkrytej karty. Naciśnij GOTOWE, gdy skończysz; gra zaczyna się, gdy wszyscy są gotowi.\n\n" +
                "<b><color=#F2C24D>Sterowanie</color></b>\nPrzeciągnij kartę na stos, aby ją zagrać. Albo dotknij kart, aby je wybrać (dotknij ponownie, aby odznaczyć) i naciśnij ZAGRAJ. Aby zagrać kilka kart tej samej wartości, dotknij wszystkich i przeciągnij jedną. Karty, których nie możesz zagrać, są przyciemnione; dotknij jednej, aby zobaczyć dlaczego. Strzałki wokół stołu pokazują kierunek gry. Dotknij stosu, aby zobaczyć wszystkie jego karty.\n\n" +
                "<b><color=#F2C24D>Klawiatura (PC)</color></b>\n1-9 wybiera kartę, Enter lub Spacja zagrywa, P bierze stos, H lub L wybiera wyżej lub niżej, J Q K A lub 2-9 ustala wartość Asa (B spala), S sortuje rękę, V pokazuje stos, F przyspiesza grę po Twoim końcu, Esc otwiera menu.",
                "<b><color=#F2C24D>Objetivo</color></b>\nQuédate sin cartas. La partida sigue hasta que todos terminan, así que cada puesto, del 1.º al último, cuenta.\n\n" +
                "<b><color=#F2C24D>Preparación</color></b>\nCada jugador recibe 3 cartas boca abajo, 3 boca arriba encima y 3 en la mano. El resto forma el mazo. Con 6 o más jugadores se usan dos barajas.\n\n" +
                "<b><color=#F2C24D>Tu turno</color></b>\nJuega una carta igual o mayor que la de arriba del montón (el 2 es la más baja, el As la más alta). Puedes jugar varias cartas del mismo valor a la vez, pero no es obligatorio. Mientras quede mazo, roba hasta tener 3 cartas. Si no puedes jugar, recoges todo el montón.\n\n" +
                "<b><color=#F2C24D>Cartas especiales</color></b>\n" +
                "<b>2</b>  Reinicio. Va sobre cualquier carta; el siguiente jugador puede jugar lo que quiera.\n" +
                "<b>7</b>  Canta MAYOR o MENOR. El siguiente jugador debe jugar más alto o más bajo que 7.\n" +
                "<b>8</b>  Salto. Cada 8 salta a un jugador (dos 8 saltan a dos). Si solo quedan 2 jugadores, el 8 es una carta normal.\n" +
                "<b>10</b>  Quemar. Retira el montón del juego y vuelves a jugar.\n" +
                "<b>Reina</b>  Cambia el sentido del juego.\n" +
                "<b>As</b>  Elige cuánto vale: cualquier valor que iguale o supere el montón (sobre una Jota puede ser J, Q, K o A, nunca 9). Sobre un 7 MENOR puede ser 2-6. O úsalo como un 10 para quemar el montón.\n" +
                "<b>Póker</b>  Cuatro cartas del mismo valor seguidas en el montón (jugadas juntas o una tras otra por distintos jugadores) lo queman, y quien completa las cuatro vuelve a jugar.\n\n" +
                "<b><color=#F2C24D>Las tres fases</color></b>\n1. Juega de tu mano hasta que se acabe el mazo y vacíes la mano.\n" +
                "2. Luego juega tus cartas boca arriba. Si no puedes jugar ninguna, recoges el montón y te llevas a la mano una carta boca arriba a tu elección.\n3. Por último tus cartas boca abajo, girándolas de una en una sin mirar. Si la carta no se puede jugar, recoges el montón con ella y debes vaciar la mano otra vez.\n\n" +
                "<b><color=#F2C24D>Opcional: Intercambio antes de jugar</color></b>\nActívalo en Un jugador (o como anfitrión en una sala). Tras el reparto, cambia las cartas de tu mano por tus cartas boca arriba: toca una carta de la mano y luego una boca arriba. Pulsa LISTO cuando quieras; la partida empieza cuando todos están listos.\n\n" +
                "<b><color=#F2C24D>Controles</color></b>\nArrastra una carta al montón para jugarla. O toca cartas para elegirlas (toca otra vez para quitarlas) y pulsa JUGAR. Para jugar varias del mismo valor, tócalas todas y arrastra una. Las cartas que no puedes jugar se ven oscuras; toca una para ver por qué. Las flechas alrededor de la mesa muestran el sentido del juego. Toca el montón para ver todas sus cartas.\n\n" +
                "<b><color=#F2C24D>Teclado (PC)</color></b>\n1-9 elige una carta, Enter o Espacio juega, P recoge el montón, H o L canta mayor o menor, J Q K A o 2-9 eligen el valor del As (B quema), S ordena tu mano, V muestra el montón, F acelera cuando ya has terminado, Esc abre el menú.");

            // Most specific patterns first, so "Needs J or higher" matches "Needs {0} or higher", not "{0} or higher".
            patterns.Sort((a, b) => (b.prefix.Length + b.suffix.Length).CompareTo(a.prefix.Length + a.suffix.Length));
        }
    }
}
