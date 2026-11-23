# Palace – The Classic Shithead Card Game

A mobile-first (Android / iOS, landscape) and Windows card game made in Unity 6, faithfully following our school's house rules.

## Running it
1. Open the project in **Unity 6000.6.10f1** (Universal 2D template).
2. First time only, or after pulling changes to the Palace tools: **Palace → Setup → Run Full Setup**.
   This generates the art and builds the prefabs and the `MainMenu` and `Game` scenes.
3. Open `Assets/Palace/Scenes/MainMenu.unity` and press **Play**.

## What's in the game
- Splash screen, main menu, How to Play, Settings (music, sound, animation speed, frame rate, table colour, card back) and Credits
- Single player against 1–9 computer opponents (2–10 players; a second deck is used from 6 players)
- The full house rules: 2 reset, 7 higher/lower, 8 skips (stacking), 10 burn + extra turn, Queen reverse,
  Ace high or burn, optional same-rank multi-card plays, hand → face-up → face-down stages, finishing order and results
- Multiplayer: same Wi-Fi (automatic discovery) or online with a join code (see Docs/Multiplayer-Setup.md)

## Project layout
| Folder | Contents |
|---|---|
| `Assets/Palace/Scripts/Core` | **Rules engine**: pure C#, no Unity code. `PalaceGame` is the only place the rules live. |
| `Assets/Palace/Scripts/Game` | `GameController` (connects the rules to the table, runs the bots, plays animations) |
| `Assets/Palace/Scripts/Table`, `Visuals`, `UI`, `Menu`, `App` | Views, menus, settings, audio |
| `Assets/Palace/Editor` | Setup tools: art generator, scene builder |
| `Tests/Palace.Core.Tests` | Automated rules tests, run with `dotnet run` |
| `Docs` | Design notes, open rule questions, progress log |

## Running the rules tests
```
cd Tests/Palace.Core.Tests
dotnet run -c Release
```
This runs one scenario per rule, then 3,600 simulated bot games covering 2–10 players.
