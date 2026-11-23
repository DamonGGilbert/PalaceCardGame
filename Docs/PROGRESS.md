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

## Waiting on you
- [ ] Confirm or change the proposed rule defaults in `DESIGN.md` (each is one line in `HouseRules.cs`)
- [ ] Play it and report anything that feels wrong

## Next: Stage 5 – Multiplayer
- Decide on services (Unity Gaming Services Lobby + Relay, free tier) and confirm costs together
- Local network play (same Wi-Fi) and online play with join codes
- Host-authoritative: the host runs `PalaceGame`; clients send commands and receive events
- Hidden information: each client only receives its own hand

## Later
- Stage 6: polish (more animation, real recorded sounds if wanted, haptics on mobile, keyboard shortcuts on PC)
- Stage 7: device testing on Android and iOS

## Known limitations / notes
- The hand area is positioned relative to the table, so 4:3 tablets have spare space below it.
- Bots are deliberately simple (cheapest legal card, saving 2s/10s/Aces). A harder difficulty can come later.
- `AutoPlayHarness` only runs when the game is launched with `-palace-autoplay` (used for automated testing).
