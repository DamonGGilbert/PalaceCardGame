# Palace – Design Notes

Living document: architecture decisions and open rule questions.

## 1. Open rule questions (needed before Stage 3)

Each question has a **proposed default**. The defaults are now implemented so the game is playable.
The adjustable ones are single settings in `Assets/Palace/Scripts/Core/HouseRules.cs`. Tell me any you want changed.

### Must decide
| # | Question | Proposed default |
|---|---|---|
| Q1 | **Deck size.** 9 cards × players: 6 players need 54 cards, 10 players need 90. One deck only covers 2–5 players. | 1 deck for 2–5 players, 2 decks for 6–10. No jokers. |
| Q2 | **Can a 10 be played on anything** (e.g. on a King), or only when ≥ the top card? Same question for an **Ace used as a 10**. | 10 and Ace-as-10 can be played on anything except during a 7-LOWER (see Q3). |
| Q3 | **7 HIGHER/LOWER**: strictly higher (8+) / strictly lower (6 or less)? Do 2s and 10s (and Ace-as-10) ignore the 7 restriction? | Strict. 2 is always allowed (it's lower than 7 anyway). 10 / Ace-as-10 are **not** allowed on a LOWER 7, but are allowed on a HIGHER 7. |
| Q4 | **"7 on 7"**: under HIGHER, 7 isn't higher; under LOWER, 7 isn't lower. So when can a 7 go on a 7? | A 7 can't be played on a declared 7 (it's neither higher nor lower). A 2 always can; a 10 only on a HIGHER 7. |
| Q5 | **Ace choice**: when an Ace is played, does the player choose "high Ace" or "burn"? | The player chooses with two buttons, shown only when both options are legal. |
| Q6 | **Who goes first?** | Random player for the prototype. |
| Q7 | **Card swap before play.** Many versions let players swap hand cards with face-up cards before the first turn. It's not in your rules. | No swap phase. |

### Edge cases (likely fine with the default)
| # | Question | Proposed default |
|---|---|---|
| Q8 | After picking up the pile, does the turn pass? | Yes, play passes to the next player. |
| Q9 | Can a player pick up voluntarily when they could play? | No. You must play if you can. |
| Q10 | Can face-up cards be combined with matching hand cards in one turn? | No. The hand must be empty first. |
| Q11 | Stuck on your **face-up** cards (none can be played)? | Decided (your school rule): pick up the pile AND take one face-up card of your choice into your hand. `HouseRules.FaceUpPickUpTakesOne` = false gives pile only. |
| Q12 | Several **Queens** together: does each one reverse? | Yes. Two Queens means no direction change. |
| Q13 | **Queen in a 2-player game**: no effect on turn order? | No effect (play passes to the other player as normal). |
| Q14 | **8 skips** count only players still in the game, and wrap around (possibly back to yourself). | Yes, as in your 3-player example. |
| Q15 | After an 8, the next player must beat the 8 (≥ 8). | Yes. |
| Q16 | Several 10s together: one burn, one extra turn. | Yes. |
| Q17 | Finishing on a 10: the player is out. The next player starts on an empty pile. | Yes. |
| Q18 | Finishing on a 7 / 8 / Queen: the effect still applies to the following players. | Yes. |
| Q19 | The game ends when only one player is left (last place), without them playing out. | Yes. |
| Q20 | **Four of a kind burns the pile.** This is a common rule, but it's not in yours. | Not added. |
| Q21 | Face-down cards are played one at a time. | Yes. |
| Q24 | **8 with only 2 players left**: skipping the only other player means you go again and must beat your own 8, which can trap both players forever. | Decided: with 2 players left an 8 is a normal card (no skip). `HouseRules.EightSkipsWithTwoLeft` switches the old behaviour back on. |

### Non-rule decisions
| # | Question | Proposed default |
|---|---|---|
| Q22 | Local prototype opponents: **computer players** (bots) or pass-and-play on one device? | Simple bots, so 10-player games can be tested alone. |
| Q23 | Online: playing with friends via join code, or public matchmaking with strangers? | Friends via join code first. |

## 2. Architecture

```
Palace.Core   (pure C#: no MonoBehaviours, no UI)
  Card, Deck, PlayerState, GameState
  RulesEngine: Validate(state, command) / Apply(state, command) -> events
  Commands: PlayCards, PickUpPile, PlayFaceDown, DeclareHigherLower, ChooseAceMode
      ^ commands                     | events (CardsPlayed, PileBurned, PlayerSkipped,...)
      |                              v
Palace (Unity presentation)
  GameController  – owns the GameState and feeds commands in (later: through the network)
  TableView / SeatView / CardView / HandView – display only, raise "player tapped X"
```

- **The rules live in one place** (`Palace.Core`). The UI never decides legality. It only asks
  "what is legal?" so it can highlight playable cards and explain why a card can't be played.
- **The rules engine is plain C#**, so it can be unit-tested outside the scene. It can also run on the host,
  or later on a server, without changes.
- **Hidden information**: each player's client only receives *their own* hand and face-down
  identities (as "unknown"). This prevents cheating by inspecting memory or network traffic.
- **One scene, any player count.** `TableLayout` computes seat positions, so no layouts are placed by hand.

## 3. Table layout (2–10 players)

- The table is a **stadium/racetrack shape** (the classic poker-table oval), drawn as one sprite.
- Seats sit on a slightly smaller racetrack outline. **Seat 0 (you) is always at the bottom centre**.
  Everyone else follows clockwise in turn order: bottom → left → top → right.
- Opponents are spread **evenly by distance along the outline**, not by angle, which stops them bunching at the curved ends.
  A stretch at the bottom (`localGap`) is kept for your own seat and hand.
- `TableLayout.FitScale` then finds the **largest seat size at which no two seats overlap**.
  It also keeps seats clear of your hand, the centre piles and the screen edge. This is calculated, so
  "no overlap" is guaranteed rather than eyeballed. Measured result: 2–9 players at 97–100% size, and 10 players at 87%.
- Seats in the top half put their name plate on the outside, so every player's cards face the middle.
  All seats stay upright, so names are never upside down.
- The Canvas Scaler uses **Expand** mode on a 1920×1080 reference. The full table is visible on
  16:9 and 19.5:9 phones and 4:3 tablets. Extra screen space just shows more background.

## 4. Online multiplayer recommendation

**Build the complete local game first (Stages 2–4, with bots), then add networking in Stage 5.**
Networking a rules engine that is still changing doubles the debugging work.
Because the engine is command-based from day one, adding networking later means sending commands over the network instead of directly.

Likely stack for Stage 5, to be confirmed with current pricing before we build:
- **Unity Gaming Services**: Authentication (anonymous sign-in), Lobby (create/join by code,
  ready status) and Relay (connect players without port forwarding).
- **Netcode for GameObjects**, or plain custom messages. A card game sends a few small messages per turn,
  so this stays simple.
- **Host-authoritative**: the host player's device runs the rules engine. This is free to run and fine for friends.
  If cheating by hosts ever matters, the same engine can move to a server later.
- UGS has a free tier that should cover development and small friend groups. I'll check the exact current limits and costs with you before anything is set up.
