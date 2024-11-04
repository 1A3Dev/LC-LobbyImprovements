using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        public bool IsPasswordProtected;
    }

    public class ClientDiscovery
    {
        private UdpClient udpClient;
        private CancellationTokenSource cancellationTokenSource;
        private int listenPort;
        public bool isListening { get; private set; }
        private List<LANLobby> discoveredLobbies = new List<LANLobby>();

        
        private async Task<LANLobby> StartListening(string targetLobbyIP, int targetLobbyPort, float maxDiscoveryTime, CancellationToken cancellationToken)
        {
            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(maxDiscoveryTime), cancellationToken);
                while (!cancellationToken.IsCancellationRequested)
                {
                    Task<UdpReceiveResult> task = udpClient.ReceiveAsync();
                    if (await Task.WhenAny(task, timeoutTask) == task)
                    {
                        UdpReceiveResult result = await task;
                        byte[] data = result.Buffer;
                        string message = Encoding.UTF8.GetString(data);
                        LANLobby tmpLobby = ParseAndStoreLobby(result.RemoteEndPoint.Address.ToString(), message, targetLobbyIP, targetLobbyPort);
                        if (tmpLobby != null)
                            return tmpLobby;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError("[LAN Discovery] StartListening Error: " + ex.Message);
            }
            return null;
        }

        public async Task<LANLobby> DiscoverSpecificLobbyAsync(string targetLobbyIP, int targetLobbyPort, float discoveryTime)
        {
            if (isListening)
            {
                cancellationTokenSource.Cancel();
                await Task.Delay(100); // give the task a chance to cancel
            }

            if (udpClient == null || listenPort != PluginLoader.lanDiscoveryPort.Value)
            {
                listenPort = PluginLoader.lanDiscoveryPort.Value;
                udpClient = new UdpClient(listenPort);
            }

            cancellationTokenSource = new CancellationTokenSource();
            isListening = true;
            PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] DiscoverSpecificLobbyAsync Started (Target: {targetLobbyIP}:{targetLobbyPort})");

            try
            {
                return await StartListening(targetLobbyIP, targetLobbyPort, discoveryTime, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError($"[LAN Discovery] DiscoverSpecificLobbyAsync Error: {ex}");
            }
            finally
            {
                udpClient.Close();
                isListening = false;
                PluginLoader.StaticLogger.LogInfo($"[LAN Discovery] DiscoverSpecificLobbyAsync Stopped (Target: {targetLobbyIP}:{targetLobbyPort})");
            }

            return null;
        }
        
        public async Task<List<LANLobby>> DiscoverLobbiesAsync(float discoveryTime)
        {
            if (isListening)
            {
                cancellationTokenSource.Cancel();
                await Task.Delay(100); // give the task a chance to cancel
            }

            if (udpClient == null || listenPort != PluginLoader.lanDiscoveryPort.Value)
            {
                listenPort = PluginLoader.lanDiscoveryPort.Value;
                udpClient = new UdpClient(listenPort);
            }

            discoveredLobbies.Clear();
            cancellationTokenSource = new CancellationTokenSource();
            isListening = true;
            PluginLoader.StaticLogger.LogInfo("[LAN Discovery] DiscoverLobbiesAsync Started");

            try
            {
                await StartListening(null, 0, discoveryTime, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError("[LAN Discovery] DiscoverLobbiesAsync Error: " + ex);
            }
            finally
            {
                isListening = false;
                PluginLoader.StaticLogger.LogInfo("[LAN Discovery] DiscoverLobbiesAsync Stopped");
            }

            return discoveredLobbies;
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
                        existingLobby.MemberCount = parsedLobby.MemberCount;
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
