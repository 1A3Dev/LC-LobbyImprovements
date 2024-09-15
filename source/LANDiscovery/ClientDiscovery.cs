using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LobbyImprovements.LANDiscovery
{
    [Serializable]
    public class LANLobby
    {
        public string GameId;
        public string GameVersion;
        public string IPAddress;
        public ushort Port;
        public string LobbyName;
        public string LobbyTag = "none";
        public int MemberCount;
        public int MaxMembers;
        public bool IsChallengeMoon;
        public bool IsSecure;
    }

    public class ClientDiscovery : MonoBehaviour
    {
        private UdpClient udpClient;
        public int listenPort = 47777;
        public bool isListening { get; private set; }

        private List<LANLobby> discoveredLobbies = new List<LANLobby>();

        public async Task<List<LANLobby>> DiscoverLobbiesAsync(float discoveryTime)
        {
            discoveredLobbies.Clear();

            udpClient = new UdpClient(listenPort);
            isListening = true;
            PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery listening started");

            LANLobby foundLobby = null;
            Task listenTask = Task.Run(() => StartListening(null, 0, ref foundLobby));
            await Task.Delay(TimeSpan.FromSeconds(discoveryTime));

            isListening = false;
            udpClient.Close();
            PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery listening stopped");

            return discoveredLobbies;
        }

        public async Task<LANLobby> DiscoverSpecificLobbyAsync(string targetLobbyIP, int targetLobbyPort, float discoveryTime)
        {
            discoveredLobbies.Clear();

            udpClient = new UdpClient(listenPort);
            isListening = true;
            PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] Server discovery listening started for specific IP. Target: {targetLobbyIP}");

            LANLobby foundLobby = null;
            Task listenTask = Task.Run(() => StartListening(targetLobbyIP, targetLobbyPort, ref foundLobby));
            float timePassed = 0f;
            float pollInterval = 0.1f; // Check every 100 ms if the lobby is found
            while (timePassed < discoveryTime && foundLobby == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(pollInterval));
                timePassed += pollInterval;
            }

            isListening = false;
            udpClient.Close();
            PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] Server discovery listening stopped for specific IP. Target: {targetLobbyIP} | Found: {foundLobby != null}");

            return foundLobby;
        }

        private void StartListening(string targetLobbyIP, int targetLobbyPort, ref LANLobby foundLobby)
        {
            try
            {
                while (isListening)
                {
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
                    byte[] data = udpClient.Receive(ref ipEndPoint); // Synchronous receive (non-async here)

                    string message = Encoding.UTF8.GetString(data);
                    LANLobby tmpLobby = ParseAndStoreLobby(ipEndPoint.Address.ToString(), message, targetLobbyIP, targetLobbyPort);
                    if (tmpLobby != null)
                        foundLobby = tmpLobby;
                }
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError("[LAN Discovery] Error receiving UDP broadcast: " + ex.Message);
            }
        }

        private LANLobby ParseAndStoreLobby(string ipAddress, string message, string targetLobbyIP, int targetLobbyPort)
        {
            try
            {
                LANLobby parsedLobby = JsonUtility.FromJson<LANLobby>(message);
                if (parsedLobby != null && parsedLobby.GameId == LANLobbyManager_LobbyList.DiscoveryKey && parsedLobby.GameVersion == GameNetworkManager.Instance.gameVersionNum.ToString())
                {
                    parsedLobby.IPAddress = ipAddress;

                    if (targetLobbyIP != null)
                    {
                        return parsedLobby.IPAddress == targetLobbyIP && parsedLobby.Port == targetLobbyPort ? parsedLobby : null;
                    }

                    LANLobby existingLobby = discoveredLobbies.Find(lobby => lobby.IPAddress == parsedLobby.IPAddress && lobby.Port == parsedLobby.Port);
                    if (existingLobby != null)
                    {
                        existingLobby = parsedLobby;
                        //PluginLoader.StaticLogger.LogDebug($"[LAN Discovery] Updated Lobby: {parsedLobby.LobbyName} at {parsedLobby.IPAddress}:{parsedLobby.Port} with {parsedLobby.MemberCount}/{parsedLobby.MaxMembers} players.");
                    }
                    else
                    {
                        discoveredLobbies.Add(parsedLobby);
                        PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] Discovered Lobby: {parsedLobby.LobbyName} at {parsedLobby.IPAddress}:{parsedLobby.Port} with {parsedLobby.MemberCount}/{parsedLobby.MaxMembers} players.");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError(ex);
            }

            return null;
        }
    }
}
