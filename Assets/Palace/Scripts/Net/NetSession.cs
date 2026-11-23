using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Palace.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Palace.Net
{
    /// <summary>
    /// One multiplayer game, from lobby to the last card. Survives scene changes.
    ///
    ///  - Wi-Fi: the host listens on the local network and announces itself (LanDiscovery).
    ///  - Online: Unity Lobby + Relay give the host a short join code; nothing to set up on routers.
    ///
    /// The HOST runs the rules engine. Everyone else only sends moves and receives their own
    /// private view of the table, so the rules can't be broken from a player's device.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const ushort LanPort = 7777;
        public const int MaxPlayers = 10;
        private const string Channel = "palace";

        private static readonly string[] BotNames =
            { "Alex", "Jordan", "Sam", "Charlie", "Riley", "Taylor", "Morgan", "Casey", "Jamie" };

        public enum Mode { None, Lan, Online }

        public static NetSession Instance { get; private set; }

        /// <summary>True while connected to (or hosting) a multiplayer game.</summary>
        public static bool Active => Instance != null && Instance.Connected;

        public Mode CurrentMode { get; private set; }
        public bool IsHost { get; private set; }
        public bool Connected { get; private set; }
        public bool InGame { get; private set; }
        public string JoinCode { get; private set; }
        public string HostAddress { get; private set; }
        public int LocalPlayerIndex { get; private set; } = -1;
        public string LastError { get; private set; }
        public readonly List<LobbySeat> Seats = new List<LobbySeat>();
        public readonly LanDiscovery Discovery = new LanDiscovery();

        public event Action LobbyChanged;
        public event Action<string> Disconnected;
        public event Action<ulong, NetCommand> CommandReceived;   // host
        public event Action<ulong> ClientInGame;                   // host
        public event Action<ulong> ClientLeft;                     // host
        public event Action<string> ErrorReceived;                 // client

        private NetworkManager net;
        private UnityTransport transport;
        private ISession session;
        private string playerName;
        private readonly Queue<(List<GameEvent> events, GameState state)> updates = new Queue<(List<GameEvent>, GameState)>();

        public ulong LocalClientId => net != null ? net.LocalClientId : 0;

        public static NetSession GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetSession>();
            Instance.Init();
            return Instance;
        }

        private void Init()
        {
            transport = gameObject.AddComponent<UnityTransport>();
            net = gameObject.AddComponent<NetworkManager>();
            net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,   // each device loads its own scenes
                ConnectionApproval = false
            };
            net.OnServerStarted += RegisterHandler;
            net.OnClientStarted += RegisterHandler;
            net.OnClientConnectedCallback += OnClientConnected;
            net.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private string pendingClose;   // shut down outside network callbacks

        private void Update()
        {
            Discovery.Tick();
            if (pendingClose != null)
            {
                string reason = pendingClose;
                pendingClose = null;
                Leave(false);
                Disconnected?.Invoke(reason);
            }
        }

        private void OnDestroy() => Discovery.Stop();

        // ==================================================================
        // Hosting and joining
        // ==================================================================

        public bool HostLan(string name)
        {
            Reset();
            playerName = name;
            CurrentMode = Mode.Lan;
            IsHost = true;
            string listen = LanDiscovery.LoopbackOnly ? "127.0.0.1" : "0.0.0.0";
            transport.SetConnectionData(listen, LanPort, listen);
            if (!net.StartHost())
            {
                LastError = "Couldn't start hosting on this network.";
                return false;
            }
            BecomeHost(name);
            HostAddress = LanDiscovery.LocalAddress();
            Discovery.StartAnnouncing(name, LanPort, Seats.Count);
            return true;
        }

        public bool JoinLan(string address, string name)
        {
            Reset();
            playerName = name;
            CurrentMode = Mode.Lan;
            IsHost = false;
            HostAddress = address;
            transport.SetConnectionData(address.Trim(), LanPort);
            if (!net.StartClient())
            {
                LastError = "Couldn't connect to " + address;
                return false;
            }
            return true; // Connected becomes true when the host accepts us
        }

        public async Task<bool> HostOnline(string name)
        {
            Reset();
            playerName = name;
            CurrentMode = Mode.Online;
            IsHost = true;
            try
            {
                await SignIn();
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                JoinCode = session.Code;
                BecomeHost(name);
                return true;
            }
            catch (Exception e)
            {
                LastError = "Couldn't create an online game. " + Friendly(e);
                Debug.LogWarning("[Palace] " + e);
                Leave(false);
                return false;
            }
        }

        public async Task<bool> JoinOnline(string code, string name)
        {
            Reset();
            playerName = name;
            CurrentMode = Mode.Online;
            IsHost = false;
            try
            {
                await SignIn();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                JoinCode = session.Code;
                return true;
            }
            catch (Exception e)
            {
                LastError = "Couldn't join that game. Check the code. " + Friendly(e);
                Debug.LogWarning("[Palace] " + e);
                Leave(false);
                return false;
            }
        }

        private static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string Friendly(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("project") || m.Contains("Project") || m.Contains("cloud"))
                return "(The game isn't linked to Unity Cloud yet - see Docs/Multiplayer-Setup.md.)";
            return "(" + m + ")";
        }

        private void BecomeHost(string name)
        {
            Connected = true;
            Seats.Clear();
            Seats.Add(new LobbySeat { ClientId = net.LocalClientId, Name = name, Ready = true });
            LocalPlayerIndex = 0;
            BroadcastLobby();
        }

        /// <summary>Leave the game (and tell everyone, if hosting).</summary>
        public void Leave(bool notify = true)
        {
            if (IsHost && Connected && notify && net.IsListening)
                foreach (var seat in Seats)
                    if (!seat.IsBot && seat.ClientId != net.LocalClientId) SendTo(seat.ClientId, NetProtocol.Closing("The host left the game."));

            Discovery.Stop();
            if (net != null && net.IsListening) net.Shutdown();
            var s = session;
            session = null;
            if (s != null) _ = s.LeaveAsync();
            Reset();
        }

        private void Reset()
        {
            Connected = false;
            InGame = false;
            IsHost = false;
            JoinCode = null;
            LocalPlayerIndex = -1;
            LastError = null;
            Seats.Clear();
            updates.Clear();
            CurrentMode = Mode.None;
        }

        // ==================================================================
        // Lobby
        // ==================================================================

        public bool CanStart
        {
            get
            {
                if (!IsHost || Seats.Count < 2) return false;
                foreach (var s in Seats) if (!s.Ready) return false;
                return true;
            }
        }

        public void AddBot()
        {
            if (!IsHost || InGame || Seats.Count >= MaxPlayers) return;
            int bots = 0;
            foreach (var s in Seats) if (s.IsBot) bots++;
            Seats.Add(new LobbySeat { Name = BotNames[bots % BotNames.Length], IsBot = true, Ready = true }); // the lobby labels it "computer"
            BroadcastLobby();
        }

        public void RemoveBot()
        {
            if (!IsHost || InGame) return;
            for (int i = Seats.Count - 1; i >= 0; i--)
                if (Seats[i].IsBot)
                {
                    Seats.RemoveAt(i);
                    BroadcastLobby();
                    return;
                }
        }

        public void SetReady(bool ready)
        {
            if (IsHost || !Connected) return;
            SendTo(NetworkManager.ServerClientId, NetProtocol.Ready(ready));
        }

        /// <summary>Host: everyone loads the table. Player numbers follow the lobby order (host = 0).</summary>
        public void StartGame()
        {
            if (!CanStart) return;
            InGame = true;
            Discovery.Stop();
            for (int i = 0; i < Seats.Count; i++)
                if (!Seats[i].IsBot && Seats[i].ClientId != net.LocalClientId) SendTo(Seats[i].ClientId, NetProtocol.StartGame(i));
            LocalPlayerIndex = 0;
            SceneManager.LoadScene(GameLaunch.GameScene);
        }

        /// <summary>Host: the lobby seat (= player index) of a connected device, or -1.</summary>
        public int PlayerIndexOf(ulong clientId)
        {
            for (int i = 0; i < Seats.Count; i++) if (!Seats[i].IsBot && Seats[i].ClientId == clientId) return i;
            return -1;
        }

        private void BroadcastLobby()
        {
            if (!IsHost) return;
            byte[] data = NetProtocol.Lobby(Seats);
            foreach (var s in Seats)
                if (!s.IsBot && s.ClientId != net.LocalClientId) SendTo(s.ClientId, data);
            if (CurrentMode == Mode.Lan) Discovery.UpdateAnnouncement(playerName, LanPort, Seats.Count);
            LobbyChanged?.Invoke();
        }

        // ==================================================================
        // In game
        // ==================================================================

        /// <summary>Host: send one player their private view of the table plus what just happened.</summary>
        public void SendUpdate(ulong clientId, GameState state, IReadOnlyList<GameEvent> events, int viewer)
        {
            SendTo(clientId, NetProtocol.Update(state, events, viewer));
        }

        public void SendError(ulong clientId, string error) => SendTo(clientId, NetProtocol.Error(error));

        /// <summary>Client: send a move to the host.</summary>
        public void SendCommand(NetCommand command) => SendTo(NetworkManager.ServerClientId, NetProtocol.Command(command));

        /// <summary>Client: tell the host the table is ready to receive the game.</summary>
        public void ReportInGame() => SendTo(NetworkManager.ServerClientId, NetProtocol.InGame());

        /// <summary>Client: next update from the host, if one has arrived.</summary>
        public bool TryTakeUpdate(out List<GameEvent> events, out GameState state)
        {
            if (updates.Count > 0)
            {
                (events, state) = updates.Dequeue();
                return true;
            }
            events = null;
            state = null;
            return false;
        }

        // ==================================================================
        // Messages
        // ==================================================================

        private void RegisterHandler()
        {
            net.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnMessage);
        }

        private void SendTo(ulong clientId, byte[] data)
        {
            if (net == null || !net.IsListening || net.CustomMessagingManager == null) return;
            using (var writer = new FastBufferWriter(data.Length + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data);
                net.CustomMessagingManager.SendNamedMessage(Channel, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            try
            {
                reader.ReadValueSafe(out int length);
                if (length <= 0 || length > 1 << 20) return;
                var data = new byte[length];
                reader.ReadBytesSafe(ref data, length);
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    var type = (MsgType)r.ReadByte();
                    if (IsHost) HandleAsHost(sender, type, r);
                    else HandleAsClient(type, r);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Bad network message: " + e.Message);
            }
        }

        private void HandleAsHost(ulong sender, MsgType type, BinaryReader r)
        {
            switch (type)
            {
                case MsgType.Hello:
                {
                    string name = r.ReadString();
                    if (InGame || Seats.Count >= MaxPlayers)
                    {
                        SendTo(sender, NetProtocol.Closing(InGame ? "That game has already started." : "That game is full."));
                        return;
                    }
                    if (PlayerIndexOf(sender) < 0)
                    {
                        name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
                        if (name.Length > 14) name = name.Substring(0, 14);
                        Seats.Add(new LobbySeat { ClientId = sender, Name = name, Ready = false });
                    }
                    BroadcastLobby();
                    break;
                }
                case MsgType.Ready:
                {
                    int i = PlayerIndexOf(sender);
                    if (i >= 0 && !InGame)
                    {
                        Seats[i].Ready = r.ReadBoolean();
                        BroadcastLobby();
                    }
                    break;
                }
                case MsgType.InGame:
                    ClientInGame?.Invoke(sender);
                    break;
                case MsgType.Command:
                    CommandReceived?.Invoke(sender, NetProtocol.ReadCommand(r));
                    break;
            }
        }

        private void HandleAsClient(MsgType type, BinaryReader r)
        {
            switch (type)
            {
                case MsgType.Lobby:
                    Seats.Clear();
                    Seats.AddRange(NetProtocol.ReadLobby(r));
                    LocalPlayerIndex = PlayerIndexOf(net.LocalClientId);
                    Connected = true;
                    LobbyChanged?.Invoke();
                    break;
                case MsgType.StartGame:
                    LocalPlayerIndex = r.ReadInt32();
                    InGame = true;
                    updates.Clear();
                    SceneManager.LoadScene(GameLaunch.GameScene);
                    break;
                case MsgType.Update:
                {
                    var (viewer, events, state) = NetProtocol.ReadUpdate(r);
                    LocalPlayerIndex = viewer;
                    updates.Enqueue((events, state));
                    break;
                }
                case MsgType.Error:
                    ErrorReceived?.Invoke(r.ReadString());
                    break;
                case MsgType.Closing:
                    pendingClose = r.ReadString();
                    break;
            }
        }

        // ==================================================================
        // Connections
        // ==================================================================

        private void OnClientConnected(ulong clientId)
        {
            if (!IsHost && clientId == net.LocalClientId)
                SendTo(NetworkManager.ServerClientId, NetProtocol.Hello(playerName)); // ask for a seat
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsHost)
            {
                int i = PlayerIndexOf(clientId);
                if (i < 0) return;
                if (!InGame)
                {
                    Seats.RemoveAt(i);
                    BroadcastLobby();
                }
                else
                {
                    Seats[i].IsBot = true;   // a computer player takes over their cards
                    ClientLeft?.Invoke(clientId);
                }
                return;
            }

            if ((clientId == net.LocalClientId || clientId == NetworkManager.ServerClientId) && pendingClose == null)
                pendingClose = Connected || InGame ? "Lost connection to the host." : "Couldn't reach that game.";
        }
    }
}
