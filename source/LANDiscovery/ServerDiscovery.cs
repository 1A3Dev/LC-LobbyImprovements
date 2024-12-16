using System.Net;
using System.Net.Sockets;
using System.Text;
using BepInEx.Bootstrap;
using UnityEngine;
using HarmonyLib;
using LobbyImprovements.Compatibility;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using static Unity.Netcode.Transports.UTP.UnityTransport;
using ProtocolType = System.Net.Sockets.ProtocolType;

namespace LobbyImprovements.LANDiscovery
{
    public class ServerDiscovery : MonoBehaviour
    {
        private Socket socket;
        public bool isServerRunning { get; private set; }
        private static LANLobby currentLobby = new LANLobby();

        public void StartServerDiscovery()
        {
            if (!isServerRunning)
            {
                currentLobby = new LANLobby() {
                    GameId = LANLobbyManager_LobbyList.DiscoveryKey,
                    GameVersion = GameNetworkManager.Instance.gameVersionNum.ToString(),
                    Port = NetworkManager.Singleton?.GetComponent<UnityTransport>()?.ConnectionData.Port ?? 7777,
                    LobbyName = GameNetworkManager.Instance.lobbyHostSettings?.lobbyName,
                    LobbyTag = GameNetworkManager.Instance.lobbyHostSettings?.serverTag ?? "none",
                    MemberCount = GameNetworkManager.Instance.connectedPlayers,
                    MaxMembers = PluginLoader.GetMaxPlayers(),
                    IsChallengeMoon = GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile",
                    IsSecure = PluginLoader.lanSecureLobby,
                    IsPasswordProtected = !string.IsNullOrWhiteSpace(PluginLoader.lobbyPassword),
                };
                LANLobbyManager_InGame.UpdateCurrentLANLobby(currentLobby);

                if (PluginLoader.lanIPv6Enabled.Value)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.IPv6Any, PluginLoader.lanDiscoveryPort.Value));
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(IPAddress.Parse("ff02::1")));
                }
                else
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Any, PluginLoader.lanDiscoveryPort.Value));
                }
                isServerRunning = true;
                InvokeRepeating("BroadcastServer", 0, 1.0f); // Broadcast every second
                PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery broadcasting started");

                if (Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"))
                {
                    LobbyCompatibility_Compat.SetLANLobbyModData(currentLobby);
                }
            }
        }

        void BroadcastServer()
        {
            if (!isServerRunning)
                return;

            currentLobby.LobbyName = GameNetworkManager.Instance.lobbyHostSettings?.lobbyName;
            currentLobby.LobbyTag = GameNetworkManager.Instance.lobbyHostSettings?.serverTag ?? "none";
            currentLobby.MemberCount = GameNetworkManager.Instance.connectedPlayers;
            if (currentLobby.MemberCount <= 1)
                currentLobby.IsSecure = PluginLoader.lanSecureLobby;

            if (!StartOfRound.Instance || !StartOfRound.Instance.inShipPhase || currentLobby.MemberCount >= PluginLoader.GetMaxPlayers())
                return;

            string dataStr = JsonUtility.ToJson(currentLobby);
            byte[] data = Encoding.UTF8.GetBytes(dataStr);
            EndPoint endPoint = new IPEndPoint(PluginLoader.lanIPv6Enabled.Value ? IPAddress.Parse("ff02::1") : IPAddress.Broadcast, PluginLoader.lanDiscoveryPort.Value);
            socket.SendTo(data, data.Length, SocketFlags.None, endPoint);
        }

        public void StopServerDiscovery()
        {
            if (isServerRunning)
            {
                isServerRunning = false;
                CancelInvoke("BroadcastServer");
                socket.Close();
                PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery broadcasting stopped");
            }
        }

        void OnDestroy()
        {
            StopServerDiscovery();
        }
    }

    [HarmonyPatch]
    public static class ServerDiscoveryPatches
    {
        public static ServerDiscovery serverDiscovery;

        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        [HarmonyPostfix]
        private static void Patch_StopServer()
        {
            GameNetworkManager.Instance.SetLobbyJoinable(false);
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetLobbyJoinable")]
        [HarmonyPrefix]
        private static bool GNM_SetLobbyJoinable_Prefix()
        {
            return !GameNetworkManager.Instance.disableSteam;
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetLobbyJoinable")]
        [HarmonyPostfix]
        private static void GNM_SetLobbyJoinable_Postfix(bool joinable)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                bool enableDiscovery = false;
                if (joinable && NetworkManager.Singleton)
                {
                    if (NetworkManager.Singleton.IsServer)
                    {
                        string serverListenAddress = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.ServerListenAddress;
                        if (PluginLoader.setInviteOnly)
                        {
                            PluginLoader.setInviteOnly = false;
                        }
                        // else if (serverListenAddress != "127.0.0.1" && serverListenAddress != "::1")
                        else if (serverListenAddress == "0.0.0.0")
                        {
                            if (!serverDiscovery)
                            {
                                GameObject val = new GameObject("ServerDiscovery");
                                serverDiscovery = val.AddComponent<ServerDiscovery>();
                                val.hideFlags = (HideFlags)61;
                                Object.DontDestroyOnLoad(val);
                            }

                            if (!serverDiscovery.isServerRunning)
                                serverDiscovery?.StartServerDiscovery();

                            enableDiscovery = true;
                        }
                    }
                }

                if (!enableDiscovery && serverDiscovery && serverDiscovery.isServerRunning)
                {
                    serverDiscovery.StopServerDiscovery();
                }
            }
        }
    }
}
