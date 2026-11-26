# Monetization – Setup Guide

## What's in the game
- **Coins** are earned by finishing games and spent on the Customise screen.
- **Shop tab** (Customise → Shop) sells real-money products:

| Product ID (use exactly this) | Type | What the player gets | Suggested price |
|---|---|---|---|
| `palace.coins.small` | Consumable | 2,000 coins | £0.99 |
| `palace.coins.medium` | Consumable | 8,000 coins | £2.99 |
| `palace.coins.big` | Consumable | 20,000 coins (best value) | £5.99 |
| `palace.removeads` | Non-consumable | Bonus coins with no ad to watch (same daily limit) | £2.99 |

> **Launch is ad-free:** Remove Ads is hidden in the game until an ad network is set up (`ShopCatalog.Offered()`), so
> only create the three coin bags in the stores for now.

Bigger bags are better value per pound (about 2,000 / 2,700 / 3,300 coins per £), which nudges players towards
the bigger bag without making the small one feel like a rip-off.

All product IDs live in one file: `Assets/Palace/Scripts/Shop/ShopCatalog.cs`. Prices are set in the store consoles,
and the game shows each store's local price automatically.

- **Ads: rewarded only.** No forced ads. Players can choose to watch an ad for 500 coins, up to 5 times a day.
  Remove Ads turns that into an instant "Claim" with no ad.

## How it stays safe
- Purchases use Unity IAP 5's two-step flow. Content is granted and saved **before** the purchase is confirmed,
  so a crash can't lose something a player paid for. Unconfirmed purchases are re-delivered on next launch.
- Each order's transaction ID is recorded, so nothing is ever granted twice.
- Non-consumables are restored automatically on a new device or reinstall, and with the **Restore purchases** button
  (Apple requires this button).
- Without the In App Purchasing package, the editor and development builds use a free **test store**.
  Release builds show "shop unavailable" and never give items away.

## Steps for you

### 1. Install In App Purchasing (switches real purchasing on)
Unity: **Window → Package Manager → Unity Registry → In App Purchasing → Install** (version 5.5.1 was tested).
The game detects it and turns on real purchasing automatically (`PALACE_IAP`). Nothing else is needed in code.
In the editor, Unity IAP shows its own fake purchase dialog, so you can test without paying.

### 2. Google Play Console
1. Create the app (package name = Unity **Player Settings → Other Settings → Package Name**, `com.shedhead.game`).
2. **Monetize → Products → In-app products**: create the coin products above (not Remove Ads while the game has no ads) with the same IDs and types.
3. Upload a build to an **internal testing** track and add yourself as a licence tester to test purchases.
4. Optional (recommended later): local receipt checks. **Services → In-App Purchasing → Receipt Validation Obfuscator**
   with your Play licence key.

### 3. App Store Connect
1. Create the app (bundle ID = the same Package Name / Bundle Identifier).
2. Sign the **Paid Applications Agreement** (Business section). Purchases won't work until it's active.
3. **In-App Purchases**: create the coin products (Remove Ads only once ads are added). *Consumable* for the coin packs, *Non-Consumable* for the rest.
4. Test with a **Sandbox** account on a real iPhone (iOS builds need a Mac with Xcode).

### 4. Rewarded ads (next step)
Ads use Unity LevelPlay. It needs a free LevelPlay account, the app added for Android and iOS, and a rewarded ad unit for each.
Send me the app keys and ad unit IDs (they aren't secret), and I'll connect it. Until then, development builds show a 3-second test ad.

## Audience (teens and young adults)
- Choose **13+** as the target age in Google Play's *Target audience and content*. Don't tick under-13 groups,
  or the game falls under Google's stricter Families policy.
- The expected rating is roughly **PEGI 7–12 / Teen**: card game, no gambling, no betting with money.
  "Simulated gambling" doesn't apply because nothing is wagered.
- For under-18s in the UK/EU, ads should be **non-personalised** unless consent is given. LevelPlay setup
  will include a consent prompt and non-personalised ads by default.
- Store listing name idea: **"Palace: The Shedhead Card Game"**.
