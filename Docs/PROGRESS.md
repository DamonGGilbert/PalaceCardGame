# Palace – Progress Log

## Stage 1 – Project foundation ✅ code delivered (waiting for you to run it)
- [x] Project structure (`Assets/Palace/...`)
- [x] Landscape orientation + PC window settings (`Palace → Setup → 1`)
- [x] Original art generated from code: cards, suits, card back, felt table, UI shapes (`Palace → Setup → 2`)
- [x] CardTheme / TableTheme ScriptableObjects
- [x] Card prefab (CardView: face/back, select lift, dimming, tap event)
- [x] Seat prefab (name plate, avatar, turn glow, hand-count fan, 3 face-down + 3 face-up)
- [x] Responsive 2–10 player layout with guaranteed no-overlap (TableLayout)
- [x] Game scene with preview harness (`Palace → Setup → 3`)
- [x] Tested on Unity 6000.6.10f1 and 6000.4.3f1: compiles with 0 errors and 0 warnings. Rendered 2–10 players
      at 16:9, 19.5:9 and 4:3 with 0 overlaps and nothing off-screen.
- [ ] **You:** run it in your own project and tick the checklist in `Stage1-Guide.md`

## Waiting on you
- [ ] Answers to the rule questions in `DESIGN.md` (Q1–Q7 are needed for Stage 3)

## Next: Stage 2 – Core card system
- Deck building (1 or 2 decks, see Q1), shuffle, deal 3 face-down / 3 face-up / 3 hand
- PlayerState and GameState in `Palace.Core`
- HandView: your hand along the bottom, fanned/overlapping for big hands after a pick-up
- Rule self-tests runnable from a menu

## Later
- Stage 3: rules engine (2 reset, 7 higher/lower, 8 skip stacking, 10/Ace burn, Queen reverse, multi-card plays, pick up)
- Stage 4: three-stage flow, replenishing, finishing order, results screen
- Stage 5: bots → lobby → online multiplayer (costs checked first)
- Stage 6: animations, audio, settings, menus
- Stage 7: full test pass

## Known limitations / notes
- The local hand area is positioned relative to the table. On 4:3 tablets there is spare space below it.
  It will be anchored to the bottom of the screen when HandView is built in Stage 2.
- `TableLayoutPreview` is a temporary test harness and will be removed once the real game controller exists.
