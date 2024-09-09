using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;

namespace LobbyImprovements.LANDiscovery
{
    public class ServerDiscovery : MonoBehaviour
    {
        private UdpClient udpClient;
        internal bool isServerRunning = false;

        public void StartServerDiscovery()
        {
            if (!isServerRunning)
            {
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

            if (GameNetworkManager.Instance.connectedPlayers >= PluginLoader.GetMaxPlayers())
                return;

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, PluginLoader.lanDiscoveryPort.Value);

            string dataStr = JsonUtility.ToJson(new LANLobby() {
                GameId = LANLobbyManager_LobbyList.DiscoveryKey,
                GameVersion = GameNetworkManager.Instance.gameVersionNum.ToString(),
                Port = NetworkManager.Singleton?.GetComponent<UnityTransport>()?.ConnectionData.Port ?? 7777,
                LobbyName = GameNetworkManager.Instance.lobbyHostSettings?.lobbyName,
                LobbyTag = GameNetworkManager.Instance.lobbyHostSettings?.serverTag ?? "none",
                MemberCount = GameNetworkManager.Instance.connectedPlayers,
                MaxMembers = PluginLoader.GetMaxPlayers(),
                IsChallengeMoon = GameNetworkManager.Instance.currentSaveFileName == "LCChallengeFile"
            });

            byte[] data = Encoding.UTF8.GetBytes(dataStr);
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

        [HarmonyPatch(typeof(MenuManager), "StartHosting")]
        [HarmonyPostfix]
        private static void OnStartServer(MenuManager __instance)
        {
            if (__instance.hostSettings_LobbyPublic && GameNetworkManager.Instance.disableSteam)
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
