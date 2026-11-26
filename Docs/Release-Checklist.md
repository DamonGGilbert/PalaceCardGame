# Shedhead – release checklist

Work top to bottom. Items marked **(blocker)** must be done before the game can go live. Costs are as of late 2026;
check the current prices on each site.

## 1. Accounts and identity (do these first: some take days)
- [ ] Make a dedicated email, e.g. `shedheadgame@gmail.com`, for every account below and as the public support email
- [ ] Reserve "Shedhead" on TikTok, Instagram, YouTube and X with that email
- [ ] **(blocker)** Google Play developer account – **$25 one-off**. New *personal* accounts must run a **closed
      test with at least 12 testers for 14 days in a row** before they can publish, so start this early and line up
      12 friends with Android phones. (Organisation accounts skip this but need a D-U-N-S number.)
- [ ] **(blocker for iPhone)** Apple Developer Program – **$99 a year**. iPhone builds need a Mac with Xcode
      (a borrowed Mac or a cloud Mac service works)
- [ ] Optional: a domain such as shedhead.com (a few pounds a year) for a homepage and the privacy policy

## 2. Unity setup (on your PC)
- [ ] **(blocker)** Unity Hub → Installs → 6000.6.10f1 → Add modules → **Android Build Support** (tick OpenJDK and
      Android SDK & NDK) and **iOS Build Support**
- [ ] **(blocker)** Project Settings → Player: set **Company Name** and the app id, e.g. `com.yourname.shedhead`
      (it's still the template's `com.DefaultCompany.2D-URP`). **Never change the app id after launch** – players'
      saves are tied to it
- [ ] Run Palace → Setup → **Apply Shedhead Branding** (adds the Android adaptive icon once the Android module is in)
- [ ] Let Unity paint the art when it asks (premium range, name fonts, table organising)
- [ ] Version: Player → Version `1.0.0`, Android Bundle Version Code `1` (raise both for every update)
- Already set correctly: 64-bit ARM64 + IL2CPP for Android, Android 8.0+, iOS 15+, landscape only

## 3. Money (the game earns nothing until these are done)
- [ ] **(blocker for purchases)** Install **In App Purchasing** (Window → Package Manager → Unity Registry). Without
      it, release builds show "shop unavailable"
- [ ] Create the 4 products in both stores with the exact ids in `Docs/Monetization-Setup.md`
      (`palace.coins.small`, `palace.coins.medium`, `palace.coins.big`, `palace.removeads`)
- [ ] Set up payments/tax/bank details in Google Play Console and App Store Connect (needed to sell anything)
- [ ] **Rewarded ads:** create a Unity LevelPlay account, add the Android and iOS apps, then install the
      **Ads Mediation** package and give me the app keys and ad unit ids to connect it. Until then, release builds
      have no ads and "Watch an ad" doesn't pay out
- [ ] **(blocker if ads are on, UK/EU)** an ad consent prompt (Google's UMP or LevelPlay's), plus a "privacy
      choices" button in Settings – the privacy policy promises this, so it must exist before ads go live

## 4. Online services
- [ ] Link the project to Unity Cloud (Edit → Project Settings → Services) and turn on **Lobby** and **Relay** in the
      Unity dashboard – needed for online games (Same Wi-Fi works without it). Free tier first; check Unity's pricing
- [ ] Test an online game between two phones on different networks (one on mobile data)
- [ ] Optional: Cloud Save (`Docs/Cloud-Save-Setup.md`). If you turn on **Unity account sign-in**, Google Play
      requires a way to **delete the account** (a web page or in-game button) – ask me to add one
- [ ] Optional: Mobile Notifications package for streak reminders (`Docs/Notifications-Setup.md`)

## 5. Privacy and ratings
- [ ] **(blocker)** Host `Docs/privacy-policy.html` publicly after filling in the [brackets]. Easiest: GitHub
      Pages, or any website. Put the link in both stores and in the game's Credits if you like
- [ ] Google Play **Data safety** form – answer from the policy: device identifiers (ads), app activity (game progress if
      cloud save), purchase history, no data sold
- [ ] Apple **App Privacy** labels – same answers
- [ ] Content rating questionnaires (IARC on Google, age rating on Apple) – see `Docs/Store-Listing.md`
- [ ] Target audience: **13+** only

## 6. Testing on real devices (most bugs show up here)
- [ ] Android: one recent phone and one cheap/older phone. Check: touch and drag, text size, notch/rounded corners,
      speed during big burns, sound, vibration, rotating the phone, pausing the app and coming back
- [ ] iPhone: same checks, plus a small-screen iPhone if possible
- [ ] A full game at every table size (2, 6 and 10 players), the guided game, Swap mode, and an online game
- [ ] Buy each product with the stores' test accounts; check Restore purchases on a reinstall
- [ ] A native speaker checks the Polish and Spanish text
- [ ] Google Play **closed test**: 12+ testers for 14 days (see section 1)

## 7. Store listing
- [ ] Text: paste from `Docs/Store-Listing.md`
- [ ] Icon: done (Art/Icons/shedhead_icon.png, 1024x1024)
- [ ] Screenshots (list in `Docs/Store-Listing.md`) and the Google Play feature graphic (1024x500)
- [ ] Support email and privacy policy link

## 8. Launch
- [ ] Google Play: closed test → production (staged rollout at 20% first is a good idea)
- [ ] App Store: TestFlight with friends → submit for review (usually 1–3 days)
- [ ] After launch: watch crash reports (Unity Cloud Diagnostics is free) and reviews; ship a fix update within the
      first week if needed

## Rough costs to launch
| Item | Cost |
|---|---|
| Google Play account | $25 once |
| Apple Developer | $99 / year |
| Unity (Personal) | free under Unity's revenue threshold |
| Unity Lobby / Relay / Cloud Save | free tier to start |
| Domain (optional) | ~£10 / year |
| Mac for iPhone builds | borrow one, or cloud Mac ~£20–30 / month while needed |
