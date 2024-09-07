using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LobbyImprovements.LANDiscovery
{
    public class LANLobby
    {
        public string IPAddress;
        public ushort Port;
        public int MemberCount;
        public int MaxMembers;
        public Dictionary<string, string> Data;
        public string GetData(string key)
        {
            return Data.ContainsKey(key) ? Data[key] : null;
        }
        public void SetData(string key, string value)
        {
            if (Data.ContainsKey(key))
            {
                Data[key] = value;
            }
            else
            {
                Data.Add(key, value);
            }
        }
    }

    public class ClientDiscovery : MonoBehaviour
    {
        private UdpClient udpClient;
        public int listenPort = 47777;
        internal bool isListening = false;

        private List<LANLobby> discoveredLobbies = new List<LANLobby>();

        public async Task<List<LANLobby>> DiscoverLobbiesAsync(float discoveryTime)
        {
            discoveredLobbies.Clear();

            udpClient = new UdpClient(listenPort);
            isListening = true;
            PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery listening started");

            Task listenTask = Task.Run(() => StartListening());

            await Task.Delay(TimeSpan.FromSeconds(discoveryTime));

            isListening = false;
            udpClient.Close();
            PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery listening stopped");

            return discoveredLobbies;
        }

        private void StartListening()
        {
            try
            {
                while (isListening)
                {
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
                    byte[] data = udpClient.Receive(ref ipEndPoint); // Synchronous receive (non-async here)

                    string message = Encoding.UTF8.GetString(data);
                    ParseAndStoreLobby(ipEndPoint.Address.ToString(), message);
                }
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError("[LAN Discovery] Error receiving UDP broadcast: " + ex.Message);
            }
        }

        private void ParseAndStoreLobby(string ipAddress, string message)
        {
            try
            {
                string[] parts = message.Split(';');
                if (parts.Length == 5 && parts[0] == LANLobbyManager_LobbyList.DiscoveryKey)
                {
                    ushort lobbyPort = ushort.Parse(parts[1]);
                    int currentPlayers = int.Parse(parts[2]);
                    int maxPlayers = int.Parse(parts[3]);
                    string lobbyName = parts[4];

                    LANLobby existingLobby = discoveredLobbies.Find(lobby =>
                        lobby.IPAddress == ipAddress && lobby.Port == lobbyPort);

                    if (existingLobby != null)
                    {
                        existingLobby.SetData("name", lobbyName);
                        existingLobby.MemberCount = currentPlayers;
                        existingLobby.MaxMembers = maxPlayers;
                        PluginLoader.StaticLogger.LogDebug($"[LAN Discovery] Updated Lobby: {existingLobby.GetData("name")} at {existingLobby.IPAddress}:{existingLobby.Port} with {existingLobby.MemberCount}/{existingLobby.MaxMembers} players.");
                    }
                    else
                    {
                        LANLobby lobby = new LANLobby
                        {
                            IPAddress = ipAddress,
                            Port = lobbyPort,
                            Data = new Dictionary<string, string>() {
                                { "name", lobbyName }
                            },
                            MemberCount = currentPlayers,
                            MaxMembers = maxPlayers
                        };

                        discoveredLobbies.Add(lobby);
                        PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] Discovered Lobby: {lobby.GetData("name")} at {lobby.IPAddress}:{lobby.Port} with {lobby.MemberCount}/{lobby.MaxMembers} players.");
                    }
                }
                else
                {
                    PluginLoader.StaticLogger.LogWarning("[LAN Discovery] Invalid broadcast format received.");
                }
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError(ex);
            }
        }
    }
}
