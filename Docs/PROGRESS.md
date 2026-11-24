# Palace – Progress Log

## Stage 1 – Project foundation ✅
- [x] Project structure, landscape/PC settings, original generated art, Card and Seat prefabs
- [x] Responsive 2–10 player table layout with guaranteed no-overlap
- [x] Project on GitHub (local repo created 2026-11-23; publish via GitHub Desktop)

## Stages 2–4 – Card system, rules, full single-player game ✅ (branch `feature/playable-game`)
- [x] Rules engine `Palace.Core.PalaceGame`: deck(s), shuffle, deal 3/3/3, every house rule, three stages,
      replenishing, picking up, finishing order, game over
- [x] Rule options in `HouseRules.cs` (proposed defaults for Q1–Q21, each switchable in one place)
- [x] Computer players (`BotBrain`): use only information a real player could see
- [x] `GameController`: taps → commands, bot turns, event-by-event animations and sounds
- [x] Hand view (fans out for big hands), playable cards highlighted, tap a darkened card to see why
- [x] Prompts for 7 HIGHER/LOWER and Ace high/burn; Pick Up button only when forced
- [x] Results screen (positions, winner, player count, Play Again / Main Menu), pause menu
- [x] Splash, Main Menu, Single Player (name + bot slider 1–9), Multiplayer (placeholder), How to Play, Settings, Credits
- [x] Settings saved on the device: music, SFX, animation speed (Off/Fast/Normal), 30/60 FPS, table colour, card back
- [x] Procedural sound effects and background music (no audio files)
- [x] Tests: 64 automated rule checks plus 3,600 simulated games (`Tests/Palace.Core.Tests`), and an automated
      end-to-end run of a real Windows build

## Play-test round 1 ✅
- [x] Ace as a high card counts as a chosen value that matches or beats the pile (equal allowed); on a LOWER 7 it can be 2-6; or burn
- [x] Direction arrows around the felt; skip / 7 / reset / reverse / Ace effects; burning-cards effect
- [x] Drag and drop cards onto the pile
- [x] Hand-size fan + count bubble (gold = last card, orange 8+, red 15+)
- [x] Pause after a pick-up; slower computer players with a Computer speed setting (Relaxed / Normal / Quick)
- [x] Pile viewer: tap the pile to see every card and a count per rank

## Stage 5 – Multiplayer ✅ (Wi-Fi tested; online needs Unity Cloud linking)
- [x] Host / join on the same Wi-Fi with automatic discovery (or type the address)
- [x] Online host / join with a code (Unity Lobby + Relay via Multiplayer Services 2.5)
- [x] Game room: players, ready status, add/remove computer players, Start
- [x] Host-authoritative: the host runs the rules; each device receives only its own private view
- [x] Everyone sees themselves at the bottom; leavers are replaced by a computer player
- [x] Tests: privacy and consistency of every network update (85 checks); two builds played a full game over the network
- [x] Single player: Speed 1x/2x/4x once you've finished
- [ ] **You:** link the project to Unity Cloud and turn on Lobby + Relay (Docs/Multiplayer-Setup.md)

## Art style + cosmetics ✅
- [x] Hand-painted pub style (default): walnut and brass table, painted felt, pub backdrop, parchment cards,
      painted suits, illustrated court cards, wooden menus with brass buttons
- [x] Rebrand tagline: "The classic Shedhead card game" (last place is the Shedhead). Audience: teens and young adults
- [x] Cosmetics: 7 tables (incl. animated Lagoon water), 3 decks, 5 card backs, 4 banners, 5 name styles, 2 effect themes
- [x] Coins earned per finish; Customise screen to buy and equip; Water effect theme (SPLASH, bubbles, blue arrows)
- [x] Alegreya serif font (SIL OFL) for titles, buttons, names and card ranks

## Monetization ✅ (code) — store setup is yours
- [x] Shop tab: coin packs, Water/Midnight bundles, Remove Ads (instant bonus), Supporter Pack (exclusive items)
- [x] Unity IAP 5 backend (auto-enabled when the package is installed), test store in editor/dev builds
- [x] Rewarded ads only (opt-in, 50 coins, 5/day); test ad in dev builds
- [ ] **You:** install In App Purchasing; create the 7 products in Play Console + App Store Connect (Docs/Monetization-Setup.md)
- [ ] **Next:** connect Unity LevelPlay for real rewarded ads (needs your LevelPlay app keys + ad unit IDs)


## Polish, languages, flags, cloud save ✅ (code) — needs a check in the editor
- [x] Country flags: England, Scotland, United Kingdom, Ireland, Poland, Spain, France, Germany, Italy, USA —
      a painted flag table (250 coins), flag card back in a gilt frame (150) and flag name banner (100) for each
- [x] Customise grid scrolls (17 tables now)
- [x] Languages: English, Polish, Spanish (Settings → Display → Language; first launch follows the phone's language).
      Menus, game messages, rules, shop and How to Play are translated. **Worth a check by a native speaker.**
- [x] Settings split into tabs: Sound & speed / Display / Cloud save
- [x] Music track: Fireside (synthesised fingerpicked guitar), Lounge (soft chords) or Off
- [x] Vibration on burns, skips, pick-ups and your turn (phones; Settings toggle)
- [x] Accessibility: 4-colour deck (diamonds blue, clubs green) and Large text
- [x] Curved hand fan; hand order by rank or by suit (S key toggles in game)
- [x] PC keyboard: 1-9 pick, Enter/Space play, P pick up, H/L sevens, J Q K A / 2-9 / B for Aces, V pile, F speed, Esc menu
- [x] Cloud save code: guest backup + optional Unity account sign-in, merging that never loses unlocks or purchases
- [ ] **You:** open the project, run **Palace → Setup → 2 - Generate Art** (paints the flags), then play-test
- [ ] **You:** to switch cloud save on, follow Docs/Cloud-Save-Setup.md (install Cloud Save, enable Player Accounts)

## Tutorial, bot skill levels, progression ✅ (code) — needs a check in the editor
- [x] Computer skill: Easy / Normal / Hard (Single Player screen). Same rules at every level. Measured over thousands of
      bot games (`dotnet run` in Tests/Palace.Core.Tests): Hard beats Easy 72% and Normal 56% one-on-one, and wins 32% of
      4-player games against Normal bots (a fair share is 25%)
- [x] Guided first game: you + 2 Easy bots, stacked deal, 16 tips that appear the first time each thing happens
      (special cards, pick-ups, face-up / face-down stages, pile viewer). Bots wait while a tip is open. Offered the first
      time you press Single Player; also on the Single Player and How to Play screens
- [x] Levels and XP (every game earns XP, more for finishing higher; level-ups pay coins)
- [x] 27 achievements with coin rewards, progress bars and an unlock banner during play
- [x] 3 daily challenges (same for everyone each day, reset at local midnight) + a bonus for finishing all 3
- [x] Daily streak reward on the first visit each day (10 coins a day in a row, up to 70)
- [x] Progress screen (Daily / Achievements / Stats), level badge on the main menu, red dot = challenges left
- [x] Results screen shows the XP bar filling, level-ups and everything earned that game
- [x] Stats, XP and achievements are included in the cloud backup
- [ ] **You:** play the guided game once and check the tips read well and appear at the right moments
- [x] 2-player 8s stalemate fixed: with only 2 players left an 8 is a normal card (your choice, Q24 in DESIGN.md)
- All reward numbers (XP, coins, targets) are in the tables at the top of `Scripts/App/Progress.cs`

## Reactions, swap mode, rematch, streak reminders ✅ (code) — needs a check in the editor
- [x] Quick reactions: 12 friendly preset phrases ("Nice one!", "Unlucky!", "Good game!", "Big brain move!"...) in a
      speech bubble over your seat. Presets only, no typing, so nothing needs moderating. One every 2 seconds, and the
      host enforces a limit online. Computer players react kindly now and then. Settings → Display → Reactions Off hides them all
- [x] Optional mode **Swap before play** (Single Player screen, or the host's toggle in a game room): after the deal,
      tap a hand card then a face-up card to swap them, then press READY. Play starts when everyone is ready. Normal and Hard
      bots put their 10s, 2s, Aces and high cards face up. Engine-tested, including online sync
- [x] Rematch button on the results screen. Online, the host starts the rematch for everyone, and the other players can
      press "Ask for a rematch" (the host sees who asked)
- [x] Daily streak reminder (optional, off by default): one notification the next day at 6pm
- [ ] **You:** to switch reminders on, install the Mobile Notifications package (Docs/Notifications-Setup.md)
- [ ] **You:** try a Swap-before-play game and check the swapping feels clear

## Play-test round 2 fixes ✅ (code)
- [x] Empty table on start fixed (the guitar music generator crashed before the deal)
- [x] HIGHER / LOWER and the Ace buttons no longer open the pile viewer (the pile's tap area was on top)
- [x] All fixed-size text shrinks to fit its box, in every language
- [x] Your rule (Q11): stuck on face-up cards = pick up the pile **and** one face-up card of your choice
      (tap it). Bots take their weakest. Switch: `HouseRules.FaceUpPickUpTakesOne`
- [x] "i" button explaining Swap before play
- [x] React panel scrolls: 12 code-drawn emojis (smile, laughing, wink, wow, aww, cool, heart eyes, heart,
      fire, crown, star, cards) plus the phrases; computer players use emojis too
- [x] Profile pictures (Customise → Picture): 16 simple code-drawn logos, 6 free, the rest unlock at levels
      2–15. Shown in your seat; online players see each other's; computer players have their own
- [x] Menu animations: drifting hearts and stars, bobbing title and cards, buttons pop in and squish
      when pressed, screens pop open, pulsing challenges dot (all off with Settings → Animations: Off)
- [x] Unity offers to run Generate Art when it's missing (flags, cosmetics and the painted table need it)

## Waiting on you
- [ ] Confirm or change the proposed rule defaults in `DESIGN.md` (each is one line in `HouseRules.cs`)
- [ ] Play it and report anything that feels wrong

## Next: Stage 5 – Multiplayer
- Decide on services (Unity Gaming Services Lobby + Relay, free tier) and confirm costs together
- Local network play (same Wi-Fi) and online play with join codes
- Host-authoritative: the host runs `PalaceGame`; clients send commands and receive events
- Hidden information: each client only receives its own hand

## Later
- Stage 6: more polish (recorded sounds if wanted, more animation)
- Stage 7: device testing on Android and iOS

## Known limitations / notes
- The hand area is positioned relative to the table, so 4:3 tablets have spare space below it.
- **2-player 8s (decided, Q24):** with only 2 players left an 8 is a normal card. Skipping the only other player could
  trap both players in an endless loop. `HouseRules.EightSkipsWithTwoLeft` turns the old behaviour back on.
- `AutoPlayHarness` only runs when the game is launched with `-palace-autoplay` (used for automated testing).
