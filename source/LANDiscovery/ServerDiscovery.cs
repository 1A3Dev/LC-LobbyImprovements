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

namespace LobbyImprovements.LANDiscovery
{
    public class ServerDiscovery : MonoBehaviour
    {
        private UdpClient udpClient;
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

                udpClient = new UdpClient();
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
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, PluginLoader.lanDiscoveryPort.Value);
            udpClient.Send(data, data.Length, ipEndPoint);
        }

        public void StopServerDiscovery()
        {
            if (isServerRunning)
            {
                isServerRunning = false;
                CancelInvoke("BroadcastServer");
                udpClient.Close();
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
