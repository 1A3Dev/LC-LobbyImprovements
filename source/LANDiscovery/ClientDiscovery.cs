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
                LANLobby parsedLobby = JsonUtility.FromJson<LANLobby>(message);
                if (parsedLobby != null && parsedLobby.GameId == LANLobbyManager_LobbyList.DiscoveryKey && parsedLobby.GameVersion == GameNetworkManager.Instance.gameVersionNum.ToString())
                {
                    parsedLobby.IPAddress = ipAddress;

                    LANLobby existingLobby = discoveredLobbies.Find(lobby =>
                        lobby.IPAddress == parsedLobby.IPAddress && lobby.Port == parsedLobby.Port);

                    if (existingLobby != null)
                    {
                        existingLobby = parsedLobby;
                        PluginLoader.StaticLogger.LogDebug($"[LAN Discovery] Updated Lobby: {existingLobby.LobbyName} at {existingLobby.IPAddress}:{existingLobby.Port} with {existingLobby.MemberCount}/{existingLobby.MaxMembers} players.");
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
        }
    }
}
