# Court card pictures: editable copies

These are the 12 Jack, Queen and King pictures used by the Classic card faces (from Byron Knoll's public-domain
cards, see `Docs/Third-Party-Assets.md`). This folder is outside `Assets`, so Unity ignores it.

- `1-white-background/`: cropped to the picture frame, nothing removed. Start here if you want to make your
  own transparent version.
- `2-transparent-background/`: what the game uses now. The white background is removed, but the figures'
  own white (faces, hands, robes) is kept.

## Putting your edited versions in the game
1. Edit the PNGs in any image editor (Photopea, GIMP, Photoshop...). Keep the size (370 x 595) and the file names.
2. Save as PNG with transparency.
3. Copy them over the files in `Assets/Palace/Resources/Courts/` (keep the same names).
4. Unity picks them up the next time you click into it. Nothing else needs changing.

Tip: if you make the figures' white transparent too, faces and robes take on the deck's colour. On dark
decks they can disappear, so check on a navy or galaxy deck before deciding.
