# Multiplayer – Setup and Notes

## How it works
- **Same Wi-Fi:** one player taps **Host a game** and everyone else taps **Join a game**. Nearby games appear automatically.
  If one doesn't show, type the host's address. The host's room screen shows it, e.g. `192.168.1.20`.
- **Online:** the host taps **Host a game** under *Online* and reads out the join code. Friends tap
  **Join with a code**. This uses Unity Lobby and Relay, so nobody needs to change router settings.
- **Who's in charge:** the host's device runs the rules. Other devices send their moves and only ever
  receive their own cards, plus what everyone can see (face-up cards, the pile and hand sizes).
  Hidden cards are never sent to the wrong device.
- The host can add computer players to fill seats. If someone leaves mid-game, a computer player takes over their cards.
- **Cross-platform:** Windows, Android and iPhone can all play together, on Wi-Fi or online, as long as everyone
  has the same version of the game.

## One-time setup for online play (about 5 minutes)
Wi-Fi play works without any of this. Online play needs the project linked to Unity Cloud:

1. In the Unity Editor, open **Edit → Project Settings → Services** and click **Link to Unity Cloud project**
   (or use the cloud icon in the toolbar). You're already signed in. Choose **Create project** and name it `PalaceCardGame`.
2. Go to <https://cloud.unity.com>, open the **PalaceCardGame** project, and turn on **Lobby** and **Relay**
   (under *Multiplayer*). Accept the terms if asked. Both have a free tier; check the current limits on
   Unity's pricing page before inviting lots of players.
3. Build the game onto two devices, or run one copy in the Editor and one as a Windows build, and try Host / Join with a code.

Until step 1 is done, "Host a game (Online)" shows a message saying the game isn't linked to Unity Cloud yet.

**Status (26 Nov 2026):** done. The project is linked and Lobby + Relay are on: two copies played a full game
through a join code. To repeat that test on one PC, give each copy its own sign-in profile:
```
Palace.exe -palace-net online-host -palace-profile a -palace-codefile code.txt -palace-autoplay 1
Palace.exe -palace-net online-join -palace-profile b -palace-codefile code.txt -palace-autoplay 1 -palace-idle-turn
```
(`-palace-idle-turn` sits on one go until the turn timer runs out.)

## Testing on one computer
The automated test runs two copies against each other on this PC:
```
Palace.exe -palace-net host -palace-players 4 -palace-autoplay 1
Palace.exe -palace-net join -palace-autoplay 1
```

## Known limits (to address before release)
- **iPhone Wi-Fi discovery:** Apple requires a special "multicast" permission for automatic discovery.
  Until that's granted, iPhone players type the host's address or use online play. iOS will also ask for
  "Local Network" permission the first time.
- **Some Android phones** filter Wi-Fi broadcasts. Typing the address always works.
- **Turn timer:** in Wi-Fi and online games each person has 20 seconds for their go. A ring round the picture of
  whoever is playing counts down for everyone, and your own last five seconds tick. If it runs out, that player
  picks up the pile (with an empty pile, or a 7 / Ace call waiting, the computer makes the move for them).
  Computer players and single-player games are not timed.
- **Dropped connections:** if a player loses signal or the app is closed, a computer player plays for them and
  their device keeps trying to get back in for 60 seconds ("Reconnecting..."). Once back, they get their seat and
  the table as it is now. Phones stay awake during multiplayer games, since a locked screen drops the connection.
- **Versions:** the host turns away a copy of the game on a different version, saying who needs to update. Raise
  `NetProtocol.ProtocolVersion` whenever a change means old and new copies can't play together.
- **iPhone:** the build adds the "local network" permission sentence iOS needs for Wi-Fi games.
- If the host leaves or loses connection, the game ends for everyone. Host handover is possible later.
