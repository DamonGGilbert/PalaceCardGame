# Daily streak reminders – Setup

## What it does
- One friendly notification **the day after you play, at 6pm**. For example: "Keep your streak going! Day 4 pays 40 coins."
- **Off by default.** Players turn it on in **Settings → Sound & speed → Daily reminder**. From the second day of a streak, the daily reward pop-up also offers it once.
- Each time the game opens, the reminder moves to the next day, so it never reminds someone who has already played today.
- At most one reminder is waiting at any time. Nothing is sent from a server, so there is no cost.
- Phones only (Android and iOS). On PC and in the Editor it does nothing.

## One-time setup (about 2 minutes)
1. **Window → Package Manager → Unity Registry**, search for **Mobile Notifications**, then click **Install**.
   The game switches the feature on by itself: it adds `PALACE_NOTIFICATIONS`. If it doesn't, run **Palace → Setup → Refresh Optional Features**.
2. **Android:** nothing else is needed. On Android 13 and later, the phone asks the player for permission when they turn the reminder on.
3. **iOS:** **Edit → Project Settings → Mobile Notifications → iOS**. Leave **Request Authorization on App Launch** *off*: the game asks only when the player turns reminders on.
4. Build to a phone, turn the reminder on, then set the phone's clock forward a day to check it arrives.

## Notes
- Code: `Assets/Palace/Scripts/App/StreakReminder.cs`. The reminder time is `ReminderHour`, and the text is in `Body()` (translated into Polish and Spanish).
- The store listings should mention that the app can send optional notifications.
- The calls to the package couldn't be compiled here because the package isn't installed yet. If Unity reports an error after installing it, the fix will be in that one file.
