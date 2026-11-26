# Shedhead icon and logo

The app icon is a real screenshot of the game: a bot game captured with the names, buttons and labels hidden,
then cropped to the middle of the table (the top player's cards, the deck and pile, your table cards and your hand).

- `screenshot-source.png`: the full 1920x1080 capture the icon is cropped from.
- `tool/ScreenshotIcon.cs`: turns a capture into the app icon, the Android adaptive layers, the Play Store 512
  icon, the in-game logo (the same picture as a rounded tile with a gold rim) and preview images. It also paints
  over the empty info plate beside the pile. Run it from the `tool` folder:
  `dotnet run -- <screenshot> <output folder> 430 20 1060` (the last three numbers are the crop: left, top, size).
  Then copy the files over:
  - `shedhead_icon.png`, `shedhead_icon_android_fg.png`, `shedhead_icon_android_bg.png` to `Assets/Palace/Art/Icons/`
  - `shedhead_logo.png` to `Assets/Palace/Resources/Brand/`
  - `play-icon-512.png` to `Docs/StoreAssets/`

## Taking a new screenshot
Build the game for Windows, then run it with:
`Palace.exe -palace-autoplay 1 -palace-players 4 -palace-shots <folder> -palace-clean -screen-fullscreen 0 -screen-width 1920 -screen-height 1080`
It plays one bot game, saves `clean_0.png` to `clean_6.png` at different turns, and quits. If the info plate
has moved, change the numbers in `Patch(...)` in the tool.

## Play Store feature graphic
`Docs/StoreAssets/play-feature-graphic-1024x500.png` is made from the same screenshot: the table fills the
banner, with SHEDHEAD and "THE CLASSIC CARD GAME" (in the game's Alegreya font) on a dark fade down the left.
To remake it, run from the `tool-feature` folder:
`dotnet run -- <screenshot> ../../../Assets/Palace/Fonts/Alegreya.ttf <output png>`

## The old logo
`old-wordmark-logo/` has the previous "Shedhead" lettering logo, icons and feature graphic. To switch back,
copy them over the same files.
