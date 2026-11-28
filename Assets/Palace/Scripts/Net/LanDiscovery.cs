using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Palace.Net
{
    /// <summary>
    /// Finds Palace games on the same Wi-Fi. A host shouts "I'm hosting" once a second
    /// (UDP broadcast); joiners listen and list what they hear. No internet needed.
    ///
    /// Note: some phones limit broadcasts (iOS needs a special Apple permission), so the
    /// join screen also lets players type the host's address directly.
    /// </summary>
    public class LanDiscovery : IDisposable
    {
        public const int DiscoveryPort = 47778;
        private const string Prefix = "PALACE1";

        /// <summary>Testing only: keep all traffic on this computer (avoids firewall prompts).</summary>
        public static bool LoopbackOnly;

        public sealed class FoundGame
        {
            public string Address;
            public ushort Port;
            public string HostName;
            public int Players;
            public float LastSeen;
        }

        private UdpClient socket;
        private float nextBroadcast;
        private string announcement;
        private readonly Dictionary<string, FoundGame> found = new Dictionary<string, FoundGame>();

        public IEnumerable<FoundGame> Games => found.Values;

        // ------------------------------------------------------------------ hosting

        public void StartAnnouncing(string hostName, ushort port, int players)
        {
            Stop();
            socket = new UdpClient { EnableBroadcast = true };
            UpdateAnnouncement(hostName, port, players);
        }

        public void UpdateAnnouncement(string hostName, ushort port, int players)
        {
            announcement = string.Join("|", Prefix, port, players, (hostName ?? "Host").Replace("|", " "));
        }

        // ------------------------------------------------------------------ joining

        public void StartListening()
        {
            Stop();
            found.Clear();
            socket = new UdpClient();
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(LoopbackOnly ? IPAddress.Loopback : IPAddress.Any, DiscoveryPort));
        }

        /// <summary>Call every frame.</summary>
        public void Tick()
        {
            if (socket == null) return;
            try
            {
                if (announcement != null)
                {
                    if (Time.unscaledTime < nextBroadcast) return;
                    nextBroadcast = Time.unscaledTime + 1f;
                    byte[] data = Encoding.UTF8.GetBytes(announcement);
                    var target = LoopbackOnly ? IPAddress.Loopback : IPAddress.Broadcast;
                    socket.Send(data, data.Length, new IPEndPoint(target, DiscoveryPort));
                    return;
                }

                while (socket.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    string text = Encoding.UTF8.GetString(socket.Receive(ref from));
                    string[] parts = text.Split('|');
                    if (parts.Length < 4 || parts[0] != Prefix) continue;
                    string address = from.Address.ToString();
                    if (!found.ContainsKey(address) && found.Count >= 24) continue; // ignore a flood of fake announcements
                    found[address] = new FoundGame
                    {
                        Address = address,
                        Port = ushort.TryParse(parts[1], out var port) ? port : NetSession.LanPort,
                        Players = int.TryParse(parts[2], out var players) ? players : 1,
                        HostName = PlayerNames.Clean(parts[3], "Game"),
                        LastSeen = Time.unscaledTime
                    };
                }

                // Forget games that stopped announcing.
                var stale = new List<string>();
                foreach (var kv in found) if (Time.unscaledTime - kv.Value.LastSeen > 4f) stale.Add(kv.Key);
                foreach (var key in stale) found.Remove(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Wi-Fi discovery: " + e.Message);
                Stop();
            }
        }

        public void Stop()
        {
            announcement = null;
            socket?.Close();
            socket = null;
        }

        public void Dispose() => Stop();

        /// <summary>This device's address on the local network, shown to the host so others can type it in.</summary>
        public static string LocalAddress()
        {
            if (LoopbackOnly) return "127.0.0.1";
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("10.255.255.255", 1); // no packet is sent; this just picks the Wi-Fi interface
                    return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
                }
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
