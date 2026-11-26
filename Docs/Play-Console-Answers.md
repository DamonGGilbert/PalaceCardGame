# Google Play Console: what to upload and how to answer the forms

Written for the **ad-free launch** (no ads, no cloud save, no notifications). If any of those are added
later, update these answers and the privacy policy before releasing that update.

## Store images (Docs/StoreAssets)

| Play Console field | File | Notes |
|---|---|---|
| App icon (512 x 512) | `play-icon-512.png` | Full-bleed square; Google rounds the corners itself |
| Feature graphic (1024 x 500) | `play-feature-graphic-1024x500.png` | Shown at the top of the store page |
| Phone screenshots (2 to 8) | `Screenshots/` | Take them in Unity: press Play, set the Game view to **1920x1080**, press **F12** |

Good screenshots, in this order (the first two matter most):
1. A busy 4-player game mid-turn, with your hand fanned out
2. A 10 burning the pile (the flash and cards flying)
3. The Game mode pop-up (Classic, Big Battle, Strategy, Chaos)
4. The online room with friends and computer players
5. The Customise screen showing card backs and tables
6. The results screen with a celebration

Tip: give your profile a fun name, picture and banner before taking them.

## Main store listing
Text is in `Docs/Store-Listing.md`. Privacy policy:
**https://damonggilbert.github.io/PalaceCardGame/privacy.html** · Support email: **shedheadgame@gmail.com**

Category: **Games → Card**. Tags: Card, Casual, Multiplayer, Offline.

## App content (Policy → App content)

**Privacy policy:** the link above.

**Ads:** No, my app does not contain ads.

**App access:** All functionality is available without special access (no login).

**Content rating (IARC questionnaire)**
- Category: **Game**.
- Violence, fear, sexuality, language, drugs: **No** to all.
- Gambling: **No** real-money gambling and **no** simulated gambling. It's a shedding card game with no
  betting; coins only buy cosmetics.
- Users can interact or exchange content: **Yes**, online games with friends. Players see each other's chosen
  names and pictures and can send preset reactions. There is **no free-text chat** and no user-made content.
- Shares the user's location: **No**.
- Digital purchases: **Yes** (coin bags).
- Expected result: PEGI 3 / Everyone, possibly with "Users Interact" and "In-Game Purchases" notes.

**Target audience:** **13-15, 16-17 and 18+**. Don't tick any under-13 age group. That keeps the game out of
the Designed for Families rules, which matches the privacy policy (13+). When asked whether the game could
unintentionally appeal to children, answer **No**.

**News app:** No. **Government app:** No. **Financial features:** None. **Health:** None.

**Data safety**
- Does the app collect or share user data? **Yes**: online play uses an anonymous ID, and Unity can collect
  crash and diagnostic data.
- Is all data encrypted in transit? **Yes**.
- Can users ask for their data to be deleted? **Yes**, by email (see the privacy policy). The game has no
  accounts, so there's no account deletion link to give.
- Data types to declare:

| Data type | Collected | Shared | Optional | Purpose |
|---|---|---|---|---|
| Personal info → **Name** (the nickname players choose, shown to others online) | Yes | No | Yes (only when playing online) | App functionality |
| Device or other IDs (anonymous Unity player ID for online play) | Yes | No | Yes (online only) | App functionality |
| App info and performance → **Crash logs**, **Diagnostics** (Unity engine) | Yes | No | No | Analytics, App functionality |

- **Don't** declare: location, contacts, photos, messages, financial info, purchase history (Google Play
  handles payments and the game keeps receipts only on the phone), or the advertising ID.
- Check Unity's own guidance on what its engine and services collect, in case it has changed (search
  "Unity Google Play data safety", or start from https://unity.com/legal).

## In-app products (Monetize → Products → In-app products)
Create the three coin bags, using exactly these IDs, all **consumable** in Unity's terms (in Play they're plain
"in-app products"):

| Product ID | Name | Price |
|---|---|---|
| `palace.coins.small` | Small Bag of Coins (2,000 coins) | £0.99 |
| `palace.coins.medium` | Medium Bag of Coins (8,000 coins) | £2.99 |
| `palace.coins.big` | Big Bag of Coins (20,000 coins) | £5.99 |

Don't create `palace.removeads` until the game has ads. You'll also need a payments profile (Setup → Payments
profile) with bank and tax details before you can sell anything.

## Testing tracks
1. New personal accounts must run a **closed test with at least 12 testers for 14 days in a row** before
   applying for production. Add testers by email list or a Google Group.
2. Upload the `.aab`. In Unity: File → Build Profiles → Android → **Build**. Raise **Bundle Version Code** by 1
   for every upload (Player → Other Settings).
3. Add yourself under **Setup → License testing** so test purchases don't charge you.
