using GameNetcodeStuff;
using HarmonyLib;
using Netcode.Transports.Facepunch;
using Newtonsoft.Json;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class SessionTickets_Hosting
    {
        internal static void BroadcastPlayerInfoToClients(SV_SteamPlayer sv_playerInfo)
        {
            string activePlayerStr = JsonConvert.SerializeObject(new CL_SteamPlayer()
            {
                actualClientId = sv_playerInfo.actualClientId,
                steamId = sv_playerInfo.steamId,
                authResult1 = (int)sv_playerInfo.authResult1 >= 1 ? LIMinimalAuthResult.Invalid : (LIMinimalAuthResult)sv_playerInfo.authResult1,
                authResult2 = (int)sv_playerInfo.authResult2 >= 1 ? LIMinimalAuthResult.Invalid : (LIMinimalAuthResult)sv_playerInfo.authResult2,
            });
            int writeSize = FastBufferWriter.GetWriteSize(activePlayerStr);
            var writer = new FastBufferWriter(writeSize, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(activePlayerStr);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("LI_CL_ReceivePlayerInfo", writer, writer.Capacity > 1300
                    ? NetworkDelivery.ReliableFragmentedSequenced
                    : NetworkDelivery.Reliable);
            }
        }

        internal static void BroadcastPlayerInfoToClients(SV_LANPlayer sv_playerInfo)
        {
            string activePlayerStr = JsonConvert.SerializeObject(new CL_LANPlayer()
            {
                actualClientId = sv_playerInfo.actualClientId,
                playerName = sv_playerInfo.playerName,
            });
            int writeSize = FastBufferWriter.GetWriteSize(activePlayerStr);
            var writer = new FastBufferWriter(writeSize, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(activePlayerStr);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("LI_CL_ReceivePlayerInfo", writer, writer.Capacity > 1300
                    ? NetworkDelivery.ReliableFragmentedSequenced
                    : NetworkDelivery.Reliable);
            }
        }

        internal static void BroadcastAllPlayerInfoToClient(ulong clientId)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                List<CL_LANPlayer> activePlayers = new List<CL_LANPlayer>();
                foreach (SV_LANPlayer sv_playerInfo in PlayerManager.sv_lanPlayers)
                {
                    activePlayers.Add(new CL_LANPlayer()
                    {
                        actualClientId = sv_playerInfo.actualClientId,
                        playerName = sv_playerInfo.playerName,
                    });
                }

                string activePlayerStr = JsonConvert.SerializeObject(activePlayers);
                int writeSize = FastBufferWriter.GetWriteSize(activePlayerStr);
                var writer = new FastBufferWriter(writeSize, Allocator.Temp);
                using (writer)
                {
                    writer.WriteValueSafe(activePlayerStr);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_CL_ReceiveAllPlayerInfo", clientId, writer, writer.Capacity > 1300
                        ? NetworkDelivery.ReliableFragmentedSequenced
                        : NetworkDelivery.Reliable);
                }
            }
            else
            {
                List<CL_SteamPlayer> activePlayers = new List<CL_SteamPlayer>();
                foreach (SV_SteamPlayer sv_playerInfo in PlayerManager.sv_steamPlayers)
                {
                    activePlayers.Add(new CL_SteamPlayer()
                    {
                        actualClientId = sv_playerInfo.actualClientId,
                        steamId = sv_playerInfo.steamId,
                        authResult1 = (int)sv_playerInfo.authResult1 >= 1 ? LIMinimalAuthResult.Invalid : (LIMinimalAuthResult)sv_playerInfo.authResult1,
                        authResult2 = (int)sv_playerInfo.authResult2 >= 1 ? LIMinimalAuthResult.Invalid : (LIMinimalAuthResult)sv_playerInfo.authResult2,
                    });
                }

                string activePlayerStr = JsonConvert.SerializeObject(activePlayers);
                int writeSize = FastBufferWriter.GetWriteSize(activePlayerStr);
                var writer = new FastBufferWriter(writeSize, Allocator.Temp);
                using (writer)
                {
                    writer.WriteValueSafe(activePlayerStr);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_CL_ReceiveAllPlayerInfo", clientId, writer, writer.Capacity > 1300
                        ? NetworkDelivery.ReliableFragmentedSequenced
                        : NetworkDelivery.Reliable);
                }
            }
        }

        public static void SteamUser_OnValidateAuthTicketResponse(SteamId steamId, SteamId steamIdOwner, AuthResponse response)
        {
            if (response != AuthResponse.AuthTicketCanceled)
                PluginLoader.StaticLogger.LogInfo($"[Steam] OnValidateAuthTicketResponse ({steamId}): {response}");

            string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
            foreach (var steamPlayer in PlayerManager.sv_steamPlayers)
            {
                if (steamId == steamPlayer.steamId)
                {
                    steamPlayer.authResult2 = (LIAuthResponse)response;
                    
                    if (response != AuthResponse.OK && PluginLoader.steamSecureLobby && NetworkManager.ServerClientId != steamPlayer.actualClientId)
                    {
                        NetworkManager.Singleton.DisconnectClient(steamPlayer.actualClientId, $"{kickPrefix}{response}");
                    }
                    else
                    {
                        BroadcastPlayerInfoToClients(steamPlayer);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "OnEnable")]
        [HarmonyPostfix]
        private static void GNM_OnEnable(GameNetworkManager __instance)
        {
            if (!__instance.disableSteam)
            {
                SteamUser.OnValidateAuthTicketResponse += SteamUser_OnValidateAuthTicketResponse;
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "OnDisable")]
        [HarmonyPostfix]
        private static void GNM_OnDisable(GameNetworkManager __instance)
        {
            if (!__instance.disableSteam)
            {
                SteamUser.OnValidateAuthTicketResponse -= SteamUser_OnValidateAuthTicketResponse;
            }
        }
    }

    [HarmonyPatch]
    public class SessionTickets_Client
    {
        internal static AuthTicket currentTicket;

        internal static string ParsePlayerName(string playerName, int playerClientId)
        {
            string text = GameNetworkManager.Instance.NoPunctuation(playerName ?? "");
            text = Regex.Replace(text, "[^\\w\\._]", "");
            if (text == string.Empty || text.Length == 0)
            {
                text = $"Player #{playerClientId}";
            }
            else if (text.Length <= 2)
            {
                text += "0";
            }
            else if (text.Length > 32)
            {
                text = text.Substring(0, 32);
            }

            return text;
        }
        
        private static Color profileIconColor = Color.clear;
        internal static void UpdatedPlayerInfo(CL_LANPlayer playerInfo)
        {
            if (!GameNetworkManager.Instance.disableSteam) return;

            if (StartOfRound.Instance.ClientPlayerList.TryGetValue(playerInfo.actualClientId, out int playerClientId))
            {
                // [LAN] Update Username
                if (playerInfo.playerName != StartOfRound.Instance.allPlayerScripts[playerClientId].playerUsername)
                {
                    string text = ParsePlayerName(playerInfo.playerName, playerClientId);
                    StartOfRound.Instance.allPlayerScripts[playerClientId].playerUsername = text;
                    StartOfRound.Instance.allPlayerScripts[playerClientId].usernameBillboardText.text = text;
                    string text2 = text;
                    int numberOfDuplicateNamesInLobby = StartOfRound.Instance.allPlayerScripts[playerClientId].GetNumberOfDuplicateNamesInLobby();
                    if (numberOfDuplicateNamesInLobby > 0)
                    {
                        text2 = string.Format("{0}{1}", text, numberOfDuplicateNamesInLobby);
                    }

                    QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
                    if (quickMenuManager)
                        quickMenuManager.playerListSlots[playerClientId].usernameHeader.text = text2;
                    StartOfRound.Instance.mapScreen.radarTargets[playerClientId].name = text2;
                }
            }
        }
        internal static void UpdatedPlayerInfo(CL_SteamPlayer playerInfo)
        {
            if (GameNetworkManager.Instance.disableSteam) return;

            if (StartOfRound.Instance.ClientPlayerList.TryGetValue(playerInfo.actualClientId, out int playerClientId))
            {
                // [Steam] Update Profile Icon
                QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
                PlayerListSlot playerSlot = quickMenuManager?.playerListSlots[playerClientId];
                if (playerSlot != null)
                {
                    Color targetColor = profileIconColor != Color.clear ? profileIconColor : Color.clear;
                    if (playerInfo.authResult1 == LIMinimalAuthResult.OK && playerInfo.authResult2 == LIMinimalAuthResult.OK)
                    {
                        targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                    }
                    else if (playerInfo.authResult1 == LIMinimalAuthResult.Invalid || playerInfo.authResult2 == LIMinimalAuthResult.Invalid)
                    {
                        targetColor = Color.red;
                    }

                    if (targetColor != Color.clear)
                    {
                        //Image profileIconNameBtn = playerSlot?.slotContainer?.transform?.Find("PlayerNameButton")?.GetComponent<Image>();
                        //if (profileIconNameBtn)
                        //{
                        //    if (profileIconColor == Color.clear)
                        //        profileIconColor = profileIconNameBtn.color;

                        //    profileIconNameBtn.color = targetColor;
                        //}

                        Image profileIconImg = playerSlot?.slotContainer?.transform?.Find("ProfileIcon")?.GetComponent<Image>();
                        if (profileIconImg)
                        {
                            if (profileIconColor == Color.clear)
                                profileIconColor = profileIconImg.color;

                            profileIconImg.color = targetColor;
                        }
                    }
                }
            }
        }

        internal static void CL_ReceivePlayerInfo(ulong senderId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string playerInfoStr);

            if (GameNetworkManager.Instance.disableSteam)
            {
                CL_LANPlayer playerInfo = JsonConvert.DeserializeObject<CL_LANPlayer>(playerInfoStr);
                int origIndex = PlayerManager.cl_lanPlayers.FindIndex(x => x.actualClientId == playerInfo.actualClientId);
                if (origIndex != -1)
                    PlayerManager.cl_lanPlayers[origIndex] = playerInfo;
                else
                    PlayerManager.cl_lanPlayers.Add(playerInfo);

                UpdatedPlayerInfo(playerInfo);
            }
            else
            {
                CL_SteamPlayer playerInfo = JsonConvert.DeserializeObject<CL_SteamPlayer>(playerInfoStr);
                int origIndex = PlayerManager.cl_steamPlayers.FindIndex(x => x.actualClientId == playerInfo.actualClientId);
                if (origIndex != -1)
                    PlayerManager.cl_steamPlayers[origIndex] = playerInfo;
                else
                    PlayerManager.cl_steamPlayers.Add(playerInfo);

                UpdatedPlayerInfo(playerInfo);
            }
        }

        internal static void CL_ReceiveAllPlayerInfo(ulong senderId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string playerInfoStr);

            if (GameNetworkManager.Instance.disableSteam)
            {
                PlayerManager.cl_lanPlayers = JsonConvert.DeserializeObject<List<CL_LANPlayer>>(playerInfoStr);
                foreach (CL_LANPlayer playerInfo in PlayerManager.cl_lanPlayers)
                {
                    UpdatedPlayerInfo(playerInfo);
                }
            }
            else
            {
                PlayerManager.cl_steamPlayers = JsonConvert.DeserializeObject<List<CL_SteamPlayer>>(playerInfoStr);
                foreach (CL_SteamPlayer playerInfo in PlayerManager.cl_steamPlayers)
                {
                    UpdatedPlayerInfo(playerInfo);
                }
            }
        }

        internal static void SV_RequestAllPlayerInfo(ulong senderId, FastBufferReader messagePayload)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                SV_LANPlayer authSession = PlayerManager.sv_lanPlayers.Find(x => x.actualClientId == senderId);
                if (authSession != null)
                    SessionTickets_Hosting.BroadcastPlayerInfoToClients(authSession);
            }
            else
            {
                SV_SteamPlayer authSession = PlayerManager.sv_steamPlayers.Find(x => x.actualClientId == senderId);
                if (authSession != null)
                    SessionTickets_Hosting.BroadcastPlayerInfoToClients(authSession);
            }

            SessionTickets_Hosting.BroadcastAllPlayerInfoToClient(senderId);
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        private static void ConnectClientToPlayerObject_Prefix(PlayerControllerB __instance)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_CL_ReceivePlayerInfo", CL_ReceivePlayerInfo);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_CL_ReceiveAllPlayerInfo", CL_ReceiveAllPlayerInfo);
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_SV_RequestAllPlayerInfo", SV_RequestAllPlayerInfo);
            }
            PluginLoader.StaticLogger.LogInfo("Registered Named Message Handlers");
        }
        
        
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        private static void ConnectClientToPlayerObject_Postfix(PlayerControllerB __instance)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                if (GameNetworkManager.Instance.disableSteam)
                {
                    // Set own name locally
                    string parsedPlayerName = ParsePlayerName(PluginLoader.lanPlayerName, (int)__instance.playerClientId);
                    __instance.playerUsername = parsedPlayerName;
                    __instance.usernameBillboardText.text = parsedPlayerName;
                    string text2 = parsedPlayerName;
                    int numberOfDuplicateNamesInLobby = __instance.GetNumberOfDuplicateNamesInLobby();
                    if (numberOfDuplicateNamesInLobby > 0)
                    {
                        text2 = string.Format("{0}{1}", parsedPlayerName, numberOfDuplicateNamesInLobby);
                    }
                    __instance.quickMenuManager.AddUserToPlayerList(0, text2, (int)__instance.playerClientId);
                    StartOfRound.Instance.mapScreen.radarTargets[(int)__instance.playerClientId].name = text2;
                    
                    // Set own data for syncing to other players
                    if (!PlayerManager.sv_lanPlayers.Any(t => t.actualClientId == __instance.actualClientId))
                    {
                        SV_LANPlayer authSession = new SV_LANPlayer()
                        {
                            actualClientId = __instance.actualClientId,
                            playerName = PluginLoader.lanPlayerName,
                        };
                        PlayerManager.sv_lanPlayers.Add(authSession);
                    }
                }
                else
                {
                    if (!PlayerManager.sv_steamPlayers.Any(t => t.actualClientId == __instance.actualClientId))
                    {
                        SV_SteamPlayer authSession = new SV_SteamPlayer()
                        {
                            actualClientId = __instance.actualClientId,
                            steamId = __instance.playerSteamId,
                        };
                        PlayerManager.sv_steamPlayers.Add(authSession);
                    }
                }
            }
            else
            {
                var writer = new FastBufferWriter(1, Allocator.Temp);
                using (writer)
                {
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_SV_RequestAllPlayerInfo", NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetInstanceValuesBackToDefault")]
        [HarmonyPostfix]
        public static void SetInstanceValuesBackToDefault()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_CL_ReceivePlayerInfo");
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_CL_ReceiveAllPlayerInfo");
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_SV_RequestAllPlayerInfo");
                PluginLoader.StaticLogger.LogInfo("Unregistered Named Message Handlers");
            }
        }
        
        [HarmonyPatch(typeof(QuickMenuManager), "AddUserToPlayerList")]
        [HarmonyPostfix]
        public static void AddUserToPlayerList(ulong steamId, int playerObjectId)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                PluginLoader.StaticLogger.LogInfo("Adding user to player list: " + playerObjectId);
                CL_LANPlayer playerInfo = PlayerManager.cl_lanPlayers.Find(x => x.actualClientId == StartOfRound.Instance.allPlayerScripts[playerObjectId].actualClientId);
                if (playerInfo != null)
                {
                    PluginLoader.StaticLogger.LogInfo("Adding user to player list (UpdatedPlayerInfo): " + playerObjectId);
                    UpdatedPlayerInfo(playerInfo);
                }
            }
            else
            {
                CL_SteamPlayer playerInfo = PlayerManager.cl_steamPlayers.Find(x => x.actualClientId == StartOfRound.Instance.allPlayerScripts[playerObjectId].actualClientId && x.steamId == steamId);
                if (playerInfo != null)
                {
                    UpdatedPlayerInfo(playerInfo);
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        [HarmonyPrefix]
        private static void StartDisconnect(GameNetworkManager __instance)
        {
            if (!__instance.disableSteam)
            {
                currentTicket?.Cancel();

                foreach (SV_SteamPlayer authSession in PlayerManager.sv_steamPlayers)
                {
                    SteamUser.EndAuthSession(authSession.steamId);
                }
            }

            PlayerManager.cl_lanPlayers.Clear();
            PlayerManager.cl_steamPlayers.Clear();
            PlayerManager.sv_lanPlayers.Clear();
            PlayerManager.sv_steamPlayers.Clear();
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPrefix]
        private static void OnPlayerDC(ulong clientId)
        {
            if (!GameNetworkManager.Instance.disableSteam)
            {
                SV_SteamPlayer authSession = PlayerManager.sv_steamPlayers.Find(x => x.actualClientId == clientId);
                if (authSession != null)
                {
                    PluginLoader.StaticLogger.LogInfo($"[SteamUser.EndAuthSession] {authSession.steamId}");
                    SteamUser.EndAuthSession(authSession.steamId);
                    //PlayerManager.sv_steamPlayers.Remove(authSession);
                }
            }
            else
            {
                SV_LANPlayer authSession = PlayerManager.sv_lanPlayers.Find(x => x.actualClientId == clientId);
                if (authSession != null)
                {
                    //PlayerManager.sv_lanPlayers.Remove(authSession);
                }
            }
        }
    }
}
