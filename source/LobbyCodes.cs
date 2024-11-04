using BepInEx;
using LobbyImprovements.LANDiscovery;
using Steamworks.Data;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

            Lobby lobby = new Lobby(lobbyId);
            PluginLoader.StaticLogger.LogWarning("Attempting to join lobby by id: " + lobby.Id);
            LobbySlot.JoinLobbyAfterVerifying(lobby, lobby.Id);
            __instance.serverTagInputField.text = "";
        }
    }

    public class LobbyCodes_LAN
    {
        internal static string GetGlobalIPAddress(bool IPv6 = false, bool external = false)
        {
            if (!external && !IPv6)
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var ipInfo in networkInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (ipInfo.Address.AddressFamily == (IPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork) && !IPAddress.IsLoopback(ipInfo.Address))
                            {
                                return ipInfo.Address.ToString();
                            }
                        }
                    }
                }
                return IPv6 ? "::1" : "127.0.0.1";
            }
            
            var ip = "0.0.0.0";
            var url = IPv6 ? "https://api6.ipify.org/" : "https://api.ipify.org/";
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

            // Remove zone index if present
            int percentIndex = input.IndexOf('%');
            if (percentIndex != -1)
            {
                input = input.Substring(0, percentIndex);
            }

            // Check for IPv6 format with brackets (e.g., "[IPv6 Address]:Port")
            if (input.StartsWith("[") && input.Contains("]"))
            {
                int closingBracketIndex = input.IndexOf(']');
                if (closingBracketIndex == -1)
                    return false;

                string ipPart = input.Substring(1, closingBracketIndex - 1); // IPv6 without brackets
                string portPart = input.Substring(closingBracketIndex + 1); // Possible port part

                if (portPart.StartsWith(":"))
                {
                    portPart = portPart.Substring(1);
                    if (!int.TryParse(portPart, out port) || port < 0 || port > 65535)
                    {
                        return false;
                    }
                }

                return IPAddress.TryParse(ipPart, out ipAddress);
            }
            else
            {
                int lastColonIndex = input.LastIndexOf(':');
                if (lastColonIndex == -1)
                {
                    // No colon, try parsing as an IP without port
                    return IPAddress.TryParse(input, out ipAddress);
                }

                string ipPart = input.Substring(0, lastColonIndex);
                string portPart = input.Substring(lastColonIndex + 1);

                // Try parsing as IP:Port if port part is a valid integer
                if (!int.TryParse(portPart, out port) || port < 0 || port > 65535)
                {
                    return false;
                }

                return IPAddress.TryParse(ipPart, out ipAddress);
            }
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
