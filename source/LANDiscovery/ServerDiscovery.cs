using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using HarmonyLib;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;

namespace LobbyImprovements.LANDiscovery
{
    public class ServerDiscovery : MonoBehaviour
    {
        private UdpClient udpClient;
        internal bool isServerRunning = false;
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
                    IsChallengeMoon = GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile"
                };
                LANLobbyManager_InGame.UpdateCurrentLANLobby(currentLobby);

                udpClient = new UdpClient();
                isServerRunning = true;
                InvokeRepeating("BroadcastServer", 0, 1.0f); // Broadcast every second
                PluginLoader.StaticLogger.LogInfo("[LAN Discovery] Server discovery broadcasting started");
            }
        }

        void BroadcastServer()
        {
            if (!isServerRunning)
                return;

            currentLobby.MemberCount = GameNetworkManager.Instance.connectedPlayers;

            if (currentLobby.MemberCount >= PluginLoader.GetMaxPlayers())
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
        public static bool LobbyPublic = false;

        [HarmonyPatch(typeof(MenuManager), "StartHosting")]
        [HarmonyPostfix]
        private static void MenuManager_StartHosting(MenuManager __instance)
        {
            LobbyPublic = __instance.hostSettings_LobbyPublic;
        }

        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        private static void OnStartServer()
        {
            if (LobbyPublic && GameNetworkManager.Instance.disableSteam)
            {
                if (!serverDiscovery)
                {
                    GameObject val = new GameObject("ServerDiscovery");
                    serverDiscovery = val.AddComponent<ServerDiscovery>();
                    val.hideFlags = (HideFlags)61;
                    Object.DontDestroyOnLoad(val);
                }

                serverDiscovery?.StartServerDiscovery();
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        private static void OnStopServer(MenuManager __instance)
        {
            if (!__instance.isInitScene && serverDiscovery && serverDiscovery.isServerRunning)
            {
                serverDiscovery.StopServerDiscovery();
            }
        }
    }
}
