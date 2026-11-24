# Cloud Save – Setup and Notes

## What it does
- Backs up **coins, unlocked cosmetics, what's equipped and real-money purchases** to Unity Cloud Save.
- **Without signing in**, each install gets a hidden guest account. Progress is backed up, but only that device can restore it.
- **Signing in with a free Unity account** (Settings → Cloud save → *Sign in*) links that progress to the account.
  On a new phone or PC, sign in with the same account and the progress is merged in.
- **Merging never loses anything:** unlocks and purchases from both copies are kept. Coins and equipped items come
  from whichever copy changed most recently.
- Changes upload a few seconds after they happen, and a sync runs once each time the game starts.
  With no internet, everything is still saved on the device and syncs later.

## One-time setup (about 10 minutes)
The game builds and runs fine without any of this; the Cloud save tab then says it isn't included yet.

1. **Link the project to Unity Cloud.** This is the same step as online multiplayer, so skip it if you've done that:
   **Edit → Project Settings → Services → Link to Unity Cloud project**.
2. **Install the package.** Open **Window → Package Manager → Unity Registry**, search for **Cloud Save** and click **Install**.
   The game switches the feature on by itself (it adds `PALACE_CLOUDSAVE`; run **Palace → Setup → Refresh Optional Features** if it doesn't).
3. **Turn on Cloud Save** at <https://cloud.unity.com>: open the **PalaceCardGame** project and go to **Cloud Save**.
   There is a free tier; check Unity's current pricing page for the limits.
4. **Turn on Unity Player Accounts** for the *Sign in* button:
   - In the dashboard: **Player Authentication → Identity providers → Add → Unity Player Accounts**.
   - In the Editor: **Edit → Project Settings → Services → Authentication** and select the same provider.
     Unity creates the Player Accounts settings asset for you.
5. Build onto two devices, sign in with the same Unity account on both, and check that coins and unlocks follow you.

## Notes
- Progress is stored under the Cloud Save key `palace_progress` as a small JSON document. You can see it per player
  in the dashboard under **Cloud Save → Player data**.
- Purchases are still owned by the App Store or Google Play account; **Restore purchases** in the Shop also brings them back.
- Code: `Assets/Palace/Scripts/App/CloudProgress.cs`.
