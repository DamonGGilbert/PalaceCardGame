using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Palace.Core;
using Palace.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Multiplayer screens: choose Wi-Fi or online, host or join, then the game room
    /// (players, ready status, computer players, Start).
    /// </summary>
    public class MultiplayerMenu : MonoBehaviour
    {
        private RectTransform root;
        private Action backToMain;
        private GameObject home, joinWifi, joinOnline, room;
        private TextMeshProUGUI homeMessage, joinWifiStatus, joinOnlineStatus, roomInfo, roomStatus;
        private RectTransform foundList, seatList;
        private TMP_InputField addressInput, codeInput;
        private Button readyButton, startButton, addBotButton, removeBotButton;
        private GameObject modeButton;
        private TextMeshProUGUI modeText;
        private bool localReady;
        private bool lookingForGames;
        private float nextListRefresh;

        private NetSession Net => NetSession.GetOrCreate();

        public void Build(RectTransform menuRoot, Action onBack)
        {
            root = menuRoot;
            backToMain = onBack;
            BuildHome();
            BuildJoinWifi();
            BuildJoinOnline();
            BuildRoom();
            HideAll();
        }

        public void Open(string message = null)
        {
            ShowOnly(home);
            homeMessage.text = Loc.T(message ?? "");
        }

        public void HideAll() => ShowOnly(null);

        private void ShowOnly(GameObject screen)
        {
            foreach (var s in new[] { home, joinWifi, joinOnline, room }) s.SetActive(s == screen);
            var session = NetSession.Instance; // don't create the network object just for browsing menus
            if (screen != joinWifi && session != null && !session.IsHost) session.Discovery.Stop();
        }

        // ------------------------------------------------------------------ screens

        private void BuildHome()
        {
            Transform p = Panel("Multiplayer", new Vector2(1100f, 760f), out home);

            UIKit.Label(p, "Your name", 28f, new Vector2(260f, 50f), new Vector2(-300f, 230f), TextAlignmentOptions.Left, UIKit.Muted, true);
            UIKit.Input(p, new Vector2(420f, 64f), new Vector2(110f, 230f), GameSettings.PlayerName, "Enter your name", 14,
                v => GameSettings.PlayerName = v);

            UIKit.Label(p, "Same Wi-Fi", 30f, new Vector2(460f, 50f), new Vector2(-240f, 140f), TextAlignmentOptions.Center, UIKit.Gold, true);
            UIKit.Label(p, "Online", 30f, new Vector2(460f, 50f), new Vector2(240f, 140f), TextAlignmentOptions.Center, UIKit.Gold, true);
            UIKit.Button(p, "Host a game", new Vector2(420f, 84f), new Vector2(-240f, 60f), HostWifi, true);
            UIKit.Button(p, "Join a game", new Vector2(420f, 84f), new Vector2(-240f, -40f), OpenJoinWifi);
            UIKit.Button(p, "Host a game", new Vector2(420f, 84f), new Vector2(240f, 60f), HostOnline, true);
            UIKit.Button(p, "Join with a code", new Vector2(420f, 84f), new Vector2(240f, -40f), () =>
            {
                ShowOnly(joinOnline);
                joinOnlineStatus.text = "";
            });

            homeMessage = UIKit.Label(p, "", 24f, new Vector2(1000f, 80f), new Vector2(0f, -150f), TextAlignmentOptions.Center, new Color(1f, 0.65f, 0.55f));
            homeMessage.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -300f), () =>
            {
                HideAll();
                backToMain();
            });
        }

        private void BuildJoinWifi()
        {
            Transform p = Panel("Join on Wi-Fi", new Vector2(1000f, 820f), out joinWifi);
            UIKit.Label(p, "Games on this Wi-Fi:", 26f, new Vector2(900f, 40f), new Vector2(0f, 270f), TextAlignmentOptions.Center, UIKit.Muted);
            foundList = UIBuild.Rect("Found", p, new Vector2(900f, 300f), new Vector2(0f, 100f));
            joinWifiStatus = UIKit.Label(p, "", 24f, new Vector2(900f, 40f), new Vector2(0f, -80f), TextAlignmentOptions.Center, UIKit.Muted);

            UIKit.Label(p, "Or type the host's address:", 24f, new Vector2(900f, 40f), new Vector2(0f, -140f), TextAlignmentOptions.Center, UIKit.Muted);
            addressInput = UIKit.Input(p, new Vector2(420f, 64f), new Vector2(-110f, -205f), "", "e.g. 192.168.1.20", 40, null);
            UIKit.Button(p, "Join", new Vector2(200f, 64f), new Vector2(230f, -205f), () => JoinWifi(addressInput.text), true);
            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -340f), () => Open());
        }

        private void BuildJoinOnline()
        {
            Transform p = Panel("Join with a code", new Vector2(900f, 560f), out joinOnline);
            UIKit.Label(p, "Ask the host for their join code", 26f, new Vector2(800f, 40f), new Vector2(0f, 130f), TextAlignmentOptions.Center, UIKit.Muted);
            codeInput = UIKit.Input(p, new Vector2(420f, 76f), new Vector2(0f, 50f), "", "Code", 12, null);
            UIKit.Button(p, "Join", new Vector2(260f, 76f), new Vector2(0f, -50f), JoinOnline, true);
            joinOnlineStatus = UIKit.Label(p, "", 24f, new Vector2(820f, 60f), new Vector2(0f, -120f), TextAlignmentOptions.Center, UIKit.Muted);
            joinOnlineStatus.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -210f), () => Open());
        }

        private void BuildRoom()
        {
            Transform p = Panel("Game room", new Vector2(1200f, 960f), out room);
            roomInfo = UIKit.Label(p, "", 40f, new Vector2(1100f, 60f), new Vector2(0f, 330f), TextAlignmentOptions.Center, UIKit.Text, true);
            seatList = UIBuild.Rect("Seats", p, new Vector2(1000f, 520f), new Vector2(0f, 20f));
            roomStatus = UIKit.Label(p, "", 24f, new Vector2(1100f, 40f), new Vector2(0f, -275f), TextAlignmentOptions.Center, UIKit.Muted);

            addBotButton = UIKit.Button(p, "+ Computer player", new Vector2(300f, 70f), new Vector2(-330f, -350f), () => Net.AddBot());
            removeBotButton = UIKit.Button(p, "- Computer player", new Vector2(300f, 70f), new Vector2(-10f, -350f), () => Net.RemoveBot());
            startButton = UIKit.Button(p, "Start game", new Vector2(280f, 70f), new Vector2(320f, -350f), () => Net.StartGame(), true);
            // The room's game mode: everyone sees it, only the host can change it.
            modeText = UIKit.Label(p, "", 26f, new Vector2(620f, 56f), new Vector2(60f, -420f), TextAlignmentOptions.Center, UIKit.Gold, true);
            modeButton = UIKit.Button(p, "Game mode", new Vector2(220f, 66f), new Vector2(480f, -420f),
                () => GameModePanel.Open(p, () => Mathf.Max(2, Net.Seats.Count), () => { Net.ModeChanged(); RefreshRoom(); }), false, 24f).gameObject;

            readyButton = UIKit.Button(p, "I'm ready", new Vector2(300f, 70f), new Vector2(160f, -350f), () =>
            {
                localReady = !localReady;
                Net.SetReady(localReady);
                RefreshRoom();
            }, true);
            UIKit.Button(p, "Leave", new Vector2(200f, 70f), new Vector2(-480f, -420f), () =>
            {
                Net.Leave();
                Open();
            });
        }

        // ------------------------------------------------------------------ actions

        private void HostWifi()
        {
            if (Net.HostLan(GameSettings.PlayerName)) EnterRoom();
            else Open(Net.LastError);
        }

        private void OpenJoinWifi()
        {
            ShowOnly(joinWifi);
            joinWifiStatus.text = Loc.T("Looking for games...");
            lookingForGames = true;
            Net.Discovery.StartListening();
            RefreshFound();
        }

        private void JoinWifi(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            lookingForGames = false;
            joinWifiStatus.text = Loc.F("Connecting to {0}...", address);
            if (!Net.JoinLan(address.Trim(), GameSettings.PlayerName))
            {
                joinWifiStatus.text = Loc.T(Net.LastError);
                return;
            }
            StartCoroutine(WaitForRoom(joinWifiStatus));
        }

        private async void HostOnline()
        {
            homeMessage.text = Loc.T("Creating an online game...");
            bool ok = await Net.HostOnline(GameSettings.PlayerName);
            if (this == null) return;
            if (ok) EnterRoom();
            else homeMessage.text = Loc.T(Net.LastError);
        }

        private async void JoinOnline()
        {
            if (string.IsNullOrWhiteSpace(codeInput.text)) return;
            joinOnlineStatus.text = Loc.T("Joining...");
            bool ok = await Net.JoinOnline(codeInput.text, GameSettings.PlayerName);
            if (this == null) return;
            if (!ok)
            {
                joinOnlineStatus.text = Loc.T(Net.LastError);
                return;
            }
            StartCoroutine(WaitForRoom(joinOnlineStatus));
        }

        private IEnumerator WaitForRoom(TextMeshProUGUI status)
        {
            float until = Time.unscaledTime + 12f;
            while (!Net.Connected && Time.unscaledTime < until && Net.CurrentMode != NetSession.Mode.None) yield return null;
            if (Net.Connected) EnterRoom();
            else
            {
                Net.Leave(false);
                status.text = Loc.T("Couldn't reach that game. Are you on the same Wi-Fi?");
            }
        }

        private void EnterRoom()
        {
            localReady = false;
            ShowOnly(room);
            Net.LobbyChanged -= RefreshRoom;
            Net.LobbyChanged += RefreshRoom;
            Net.Disconnected -= OnDisconnected;
            Net.Disconnected += OnDisconnected;
            RefreshRoom();
        }

        private void OnDisconnected(string reason)
        {
            if (this == null) return;
            Open(reason);
        }

        private void OnDestroy()
        {
            if (NetSession.Instance == null) return;
            NetSession.Instance.LobbyChanged -= RefreshRoom;
            NetSession.Instance.Disconnected -= OnDisconnected;
        }

        // ------------------------------------------------------------------ refresh

        private void Update()
        {
            if (joinWifi.activeSelf && Time.unscaledTime >= nextListRefresh)
            {
                nextListRefresh = Time.unscaledTime + 0.5f;
                RefreshFound();
            }
        }

        private void RefreshFound()
        {
            foreach (Transform child in foundList) Destroy(child.gameObject);
            var games = Net.Discovery.Games.ToList();
            for (int i = 0; i < games.Count && i < 4; i++)
            {
                var g = games[i];
                string label = g.Players == 1 ? Loc.F("{0}'s game   (1 player)", g.HostName) : Loc.F("{0}'s game   ({1} players)", g.HostName, g.Players);
                UIKit.Button(foundList, label, new Vector2(760f, 64f), new Vector2(0f, 110f - i * 74f), () => JoinWifi(g.Address), true, 26f);
            }
            if (games.Count == 0 && lookingForGames)
                joinWifiStatus.text = Loc.T("Looking for games... (the host must be on the same Wi-Fi)");
        }

        private void RefreshRoom()
        {
            if (this == null || room == null || !room.activeSelf) return;
            var net = Net;
            roomInfo.text = net.CurrentMode == NetSession.Mode.Online
                ? Loc.T("Join code:") + "  <color=#F2C24D>" + net.JoinCode + "</color>"
                : (net.IsHost ? Loc.T("On Wi-Fi at") + " <color=#F2C24D>" + net.HostAddress + "</color>" : Loc.T("Connected on Wi-Fi"));

            foreach (Transform child in seatList) Destroy(child.gameObject);
            for (int i = 0; i < net.Seats.Count; i++)
            {
                LobbySeat s = net.Seats[i];
                float y = 230f - i * 50f;
                bool me = i == net.LocalPlayerIndex;
                string tag = i == 0 ? "  " + Loc.T("(host)") : "";
                UIKit.Label(seatList, (i + 1) + ".  " + s.Name + tag + (me ? "  - " + Loc.T("you") : ""), 28f, new Vector2(700f, 46f),
                    new Vector2(-130f, y), TextAlignmentOptions.Left, me ? UIKit.Gold : UIKit.Text, me);
                UIKit.Label(seatList, s.IsBot ? "computer" : (s.Ready ? "ready" : "not ready"), 26f, new Vector2(240f, 46f),
                    new Vector2(370f, y), TextAlignmentOptions.Right, s.Ready ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.7f, 0.5f));
            }

            bool host = net.IsHost;
            addBotButton.gameObject.SetActive(host);
            modeButton.SetActive(host);
            HouseRules mode = host ? GameModes.BuildRules() : net.HostRules ?? new HouseRules();
            modeText.text = GameModes.Summary(mode, Mathf.Max(2, net.Seats.Count));
            removeBotButton.gameObject.SetActive(host);
            startButton.gameObject.SetActive(host);
            readyButton.gameObject.SetActive(!host);
            startButton.interactable = net.CanStart;
            UIKit.SetButtonLabel(readyButton, localReady ? "Not ready" : "I'm ready");
            UIKit.SetButtonStyle(readyButton, !localReady);

            int humans = net.Seats.Count(s => !s.IsBot);
            roomStatus.text = host
                ? (net.Seats.Count < 2 ? Loc.T("Waiting for players to join (or add computer players)")
                    : net.CanStart ? Loc.F("{0} players. Everyone is ready!", net.Seats.Count) : Loc.T("Waiting for everyone to be ready"))
                : humans == 1 ? Loc.T("Waiting for the host to start (1 person here)") : Loc.F("Waiting for the host to start ({0} people here)", humans);
        }

        private Transform Panel(string title, Vector2 size, out GameObject screen)
        {
            screen = UIBuild.Stretch(title, root).gameObject;
            var panel = UIKit.Panel(screen.transform, size, Vector2.zero);
            UIKit.Label(panel.transform, title, 54f, new Vector2(size.x - 60f, 80f), new Vector2(0f, size.y * 0.5f - 60f),
                TextAlignmentOptions.Center, UIKit.Gold, true);
            return panel.transform;
        }

        // ------------------------------------------------------------------ testing only

        /// <summary>
        /// Automated network test: "-palace-net host" hosts on this computer and starts once
        /// someone joins; "-palace-net join" finds that game and joins it.
        /// </summary>
        public IEnumerator RunNetTest(string role, int players)
        {
            LanDiscovery.LoopbackOnly = true;
            yield return new WaitForSecondsRealtime(0.5f);
            if (role == "host")
            {
                if (!Net.HostLan("Host")) yield break;
                EnterRoom();
                while (Net.Seats.Count(s => !s.IsBot) < 2) yield return null;
                while (Net.Seats.Count < players) Net.AddBot();
                while (!Net.CanStart) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log("[NetTest] host starting with " + Net.Seats.Count + " players");
                Net.StartGame();
            }
            else
            {
                OpenJoinWifi();
                while (!Net.Discovery.Games.Any()) yield return null;
                var g = Net.Discovery.Games.First();
                Debug.Log("[NetTest] found " + g.HostName + "'s game at " + g.Address);
                JoinWifi(g.Address);
                while (!room.activeSelf) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                localReady = true;
                Net.SetReady(true);
                Debug.Log("[NetTest] joined and ready");
            }
        }
    }
}
