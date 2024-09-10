using BepInEx;
using LobbyImprovements.LANDiscovery;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    public class LobbyCodes
    {
        internal static void AddButtonToCopyLobbyCode(Button LobbyJoinBtn, string lobbyCodeStr, string[] textLabels)
        {
            if (LobbyJoinBtn != null)
            {
                var CopyCodeButton = Object.Instantiate(LobbyJoinBtn, LobbyJoinBtn.transform.parent);
                CopyCodeButton.name = "CopyCodeButton";
                RectTransform rectTransform = CopyCodeButton.GetComponent<RectTransform>();
                rectTransform.anchoredPosition -= new Vector2(78f, 0f);
                var LobbyCodeTextMesh = CopyCodeButton.GetComponentInChildren<TextMeshProUGUI>();
                LobbyCodeTextMesh.text = textLabels[0];
                CopyCodeButton.onClick = new Button.ButtonClickedEvent();
                CopyCodeButton.onClick.AddListener(() => CopyLobbyCodeToClipboard(lobbyCodeStr, LobbyCodeTextMesh, textLabels));
            }
        }

        internal static void CopyLobbyCodeToClipboard(string lobbyCode, TextMeshProUGUI textMesh, string[] textLabels)
        {
            if (textMesh.text != textLabels[0]) return;
            GameNetworkManager.Instance.StartCoroutine(LobbySlotCopyCode(lobbyCode, textMesh, textLabels));
        }
        internal static IEnumerator LobbySlotCopyCode(string lobbyCode, TextMeshProUGUI textMesh, string[] textLabels)
        {
            if (!lobbyCode.IsNullOrWhiteSpace())
            {
                GUIUtility.systemCopyBuffer = lobbyCode;
                PluginLoader.StaticLogger.LogInfo("Lobby code copied to clipboard: " + lobbyCode);
                textMesh.text = textLabels[1];
            }
            else
            {
                textMesh.text = textLabels[2];
            }
            yield return new WaitForSeconds(1.2f);
            textMesh.text = textLabels[0];
            yield break;
        }
    }

    public class LobbyCodes_Steam
    {
        internal static IEnumerator JoinLobbyByID(SteamLobbyManager __instance, ulong lobbyId)
        {
            if (GameNetworkManager.Instance.waitingForLobbyDataRefresh)
            {
                yield break;
            }

            PluginLoader.StaticLogger.LogWarning("[JoinLobby] Attempting to find lobby by id: " + lobbyId);
            Task<Lobby?> joinTask = SteamMatchmaking.JoinLobbyAsync(lobbyId);
            yield return new WaitUntil(() => joinTask.IsCompleted);
            if (!joinTask.Result.HasValue)
            {
                PluginLoader.StaticLogger.LogWarning("[JoinLobby] Failed to find lobby by id: " + lobbyId);
                yield break;
            }
            Lobby lobby = joinTask.Result.Value;
            if (lobby.GetData("vers").IsNullOrWhiteSpace())
            {
                PluginLoader.StaticLogger.LogWarning("[JoinLobby] Failed to join lobby by id: " + lobbyId);
                yield break;
            }
            LobbySlot.JoinLobbyAfterVerifying(lobby, lobby.Id);
            PluginLoader.StaticLogger.LogWarning("[JoinLobby] Successfully found lobby by id: " + lobbyId);
            __instance.serverTagInputField.text = "";
        }
    }

    public class LobbyCodes_LAN
    {
        internal static string GetGlobalIPAddress(bool external = false)
        {
            if (!external)
            {
                return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
            }
            
            var ip = "0.0.0.0";
            var url = "https://api.ipify.org/";
            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 2000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                using StreamReader reader = new StreamReader(dataStream);
                ip = reader.ReadToEnd();
                reader.Close();
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogError(ex);
            }
            return ip;
        }

        internal static bool TryParseIpAndPort(string input, out IPAddress ipAddress, out int port)
        {
            ipAddress = null;
            port = 0;

            string[] parts = input.Split(':');
            if (parts.Length > 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0], out ipAddress))
            {
                return false;
            }

            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out port) || port < 0 || port > 65535)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void JoinLobbyByIP(string IP_Address, ushort Port = 0, LANLobby lanLobby = null)
        {
            if (LANLobbyManager_InGame.waitingForLobbyDataRefresh)
                return;

            if (Port == 0)
                Port = (ushort)PluginLoader.lanDefaultPort.Value;

            NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address = IP_Address;
            NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Port = Port;
            PluginLoader.StaticLogger.LogInfo($"Listening to LAN server: {IP_Address}:{Port}");
            LANLobbyManager_InGame.UpdateCurrentLANLobby(lanLobby, startAClient: true);
        }
    }
}
