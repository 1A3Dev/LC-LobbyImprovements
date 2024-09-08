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

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, PluginLoader.lanDiscoveryPort.Value);
            List<string> lobbyData = new List<string>()
            {
                LANLobbyManager_LobbyList.DiscoveryKey,
                NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Port.ToString(),
                GameNetworkManager.Instance.connectedPlayers.ToString(),
                PluginLoader.GetMaxPlayers().ToString(),
                GameNetworkManager.Instance.lobbyHostSettings.lobbyName?.Replace(";", ":"),
            };

            byte[] data = Encoding.UTF8.GetBytes(string.Join(';', lobbyData));
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
        public static void OnStartServer(MenuManager __instance)
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
        public static void OnStopServer()
        {
            if (serverDiscovery && serverDiscovery.isServerRunning)
            {
                serverDiscovery.StopServerDiscovery();
            }
        }
    }
}
