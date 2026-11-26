# Shedhead icon and logo

The app icon is a little picture of a Shedhead table, drawn from the game's own card art: the draw deck and
discard pile at the top, and a player's table below (three face-up cards sitting on three face-down ones).
The Queen and King pictures are Byron Knoll's public-domain court cards (see `Docs/Third-Party-Assets.md`).

- `tool/`: the program that draws it. It writes the app icon, the Android adaptive layers, the Play Store
  512 icon, the in-game logo (the same picture as a rounded tile) and preview images. To change the cards or
  layout, edit `Cards()` in `TableIcon.cs`, then run
  `dotnet run -- <path to PalaceCardGame> <output folder>` from that folder and copy the files over:
  - `shedhead_icon.png`, `shedhead_icon_android_fg.png`, `shedhead_icon_android_bg.png` to `Assets/Palace/Art/Icons/`
  - `shedhead_logo.png` to `Assets/Palace/Resources/Brand/`
  - `play-icon-512.png` to `Docs/StoreAssets/`
- `old-wordmark-logo/`: the previous "Shedhead" lettering logo and icons. To switch back, copy them over the
  same files. The Play Store feature graphic (`Docs/StoreAssets/play-feature-graphic-1024x500.png`) still uses
  the lettering logo.
