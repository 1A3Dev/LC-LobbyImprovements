using GameNetcodeStuff;
using HarmonyLib;
using Netcode.Transports.Facepunch;
using Newtonsoft.Json;
using Steamworks;
using System.Collections;
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
    //public enum LIBeginAuthResult
    //{
    //    None = -1,
    //    OK,
    //    InvalidTicket,
    //    DuplicateRequest,
    //    InvalidVersion,
    //    GameMismatch,
    //    ExpiredTicket
    //}
    //public enum LIAuthResponse
    //{
    //    None = -1,
    //    OK,
    //    UserNotConnectedToSteam,
    //    NoLicenseOrExpired,
    //    VACBanned,
    //    LoggedInElseWhere,
    //    VACCheckTimedOut,
    //    AuthTicketCanceled,
    //    AuthTicketInvalidAlreadyUsed,
    //    AuthTicketInvalid,
    //    PublisherIssuedBan
    //}
    public class LISession {
        public ulong actualClientId;
        public SteamId steamId;
        //public LIBeginAuthResult beginAuthResult = LIBeginAuthResult.None;
        //public LIAuthResponse authResponse = LIAuthResponse.None;
    }

    public enum LIMinimalAuthResult
    {
        None = -1,
        OK,
        Invalid
    }
    public class LIPlayerInfo
    {
        public ulong actualClientId;
        public int playerClientId = -1;
        public string playerName;
        public LIMinimalAuthResult authResult = LIMinimalAuthResult.None;
    }

    [HarmonyPatch]
    public class SessionTickets_Hosting
    {
        internal static List<LISession> activeSessions = new List<LISession>();

        internal static void BroadcastPlayerInfoToClients(LIPlayerInfo playerInfo)
        {
            string activePlayerStr = JsonConvert.SerializeObject(playerInfo);
            int writeSize = FastBufferWriter.GetWriteSize(activePlayerStr);
            var writer = new FastBufferWriter(writeSize, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(activePlayerStr);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("LI_CL_ReceivePlayerInfo", writer, NetworkDelivery.Reliable);
            }
        }

        internal static void SV_BeginAuthSession(ulong senderId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out bool disableSteam);
            if (disableSteam != GameNetworkManager.Instance.disableSteam) return;

            // Sync player info to the client
            if (senderId != NetworkManager.ServerClientId)
            {
                string activePlayersStr = JsonConvert.SerializeObject(SessionTickets_Client.playerInfoList);
                int writeSizeAll = FastBufferWriter.GetWriteSize(activePlayersStr);
                var writerAll = new FastBufferWriter(writeSizeAll, Allocator.Temp);
                using (writerAll)
                {
                    writerAll.WriteValueSafe(activePlayersStr);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_CL_ReceiveAllPlayerInfo", senderId, writerAll, NetworkDelivery.Reliable);
                }
            }

            int senderObjectId = -1;
            if (StartOfRound.Instance.ClientPlayerList.TryGetValue(senderId, out int num))
                senderObjectId = num;

            if (disableSteam)
            {
                messagePayload.ReadValueSafe(out int playerClientId);
                messagePayload.ReadValueSafe(out string playerName);
                if (playerName == "PlayerName") playerName = null;

                LIPlayerInfo newPlayerInfo = new LIPlayerInfo()
                {
                    actualClientId = senderId,
                    playerClientId = senderObjectId,
                    playerName = playerName,
                };
                int activePlayerIndex = SessionTickets_Client.playerInfoList.FindIndex(x => x.actualClientId == senderId);
                if (activePlayerIndex != -1)
                    SessionTickets_Client.playerInfoList[activePlayerIndex] = newPlayerInfo;
                else
                    SessionTickets_Client.playerInfoList.Add(newPlayerInfo);

                BroadcastPlayerInfoToClients(newPlayerInfo);
            }
            else
            {
                messagePayload.ReadValueSafe(out int dataLength);
                byte[] ticketData = new byte[dataLength];
                messagePayload.ReadBytesSafe(ref ticketData, dataLength);
                messagePayload.ReadValueSafe(out ulong steamId);

                BeginAuthResult response = BeginAuthResult.DuplicateRequest;
                if (!activeSessions.Any(t => t.actualClientId == senderId))
                {
                    activeSessions.Add(new LISession()
                    {
                        actualClientId = senderId,
                        steamId = steamId,
                    });

                    response = SteamUser.BeginAuthSession(ticketData, steamId);
                    PluginLoader.StaticLogger.LogInfo($"[SteamUser.BeginAuthSession] {steamId}: {response}");

                    //int foundIndex = activeSessions.FindIndex(t => t.actualClientId == steamId);
                    //if (foundIndex != -1)
                    //    activeSessions[foundIndex].beginAuthResult = (LIBeginAuthResult)(int)response;
                }

                if (response != BeginAuthResult.OK && PluginLoader.steamSessionKickEnabled.Value && NetworkManager.ServerClientId != senderId)
                {
                    string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                    NetworkManager.Singleton.DisconnectClient(senderId, $"{kickPrefix}{response}");
                }
                else
                {
                    LIPlayerInfo newPlayerInfo = new LIPlayerInfo()
                    {
                        actualClientId = senderId,
                        playerClientId = senderObjectId,
                        authResult = (response == BeginAuthResult.OK ? LIMinimalAuthResult.OK : LIMinimalAuthResult.Invalid),
                    };
                    int activePlayerIndex = SessionTickets_Client.playerInfoList.FindIndex(x => x.actualClientId == senderId);
                    if (activePlayerIndex != -1)
                        SessionTickets_Client.playerInfoList[activePlayerIndex] = newPlayerInfo;
                    else
                        SessionTickets_Client.playerInfoList.Add(newPlayerInfo);

                    BroadcastPlayerInfoToClients(newPlayerInfo);
                }
            }
        }

        public static void SteamUser_OnValidateAuthTicketResponse(SteamId steamId, SteamId steamIdOwner, AuthResponse response)
        {
            int foundIndex = activeSessions.FindLastIndex(t => t.steamId == steamId);
            if (foundIndex != -1)
            {
                //activeSessions[foundIndex].authResponse = (LIAuthResponse)(int)response;

                if (response != AuthResponse.AuthTicketCanceled)
                    PluginLoader.StaticLogger.LogInfo($"[SteamUser.OnValidateAuthTicketResponse] {steamId}: {response}");

                if (response != AuthResponse.OK && PluginLoader.steamSessionKickEnabled.Value && NetworkManager.ServerClientId != activeSessions[foundIndex].actualClientId)
                {
                    string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                    NetworkManager.Singleton.DisconnectClient(activeSessions[foundIndex].actualClientId, $"{kickPrefix}{response}");
                    activeSessions.RemoveAt(foundIndex);
                }
                else
                {
                    int playerInfoIndex = SessionTickets_Client.playerInfoList.FindIndex(x => x.actualClientId == activeSessions[foundIndex].actualClientId);
                    if (playerInfoIndex != -1)
                    {
                        SessionTickets_Client.playerInfoList[playerInfoIndex].authResult = (response == AuthResponse.OK) ? LIMinimalAuthResult.OK : LIMinimalAuthResult.Invalid;
                        BroadcastPlayerInfoToClients(SessionTickets_Client.playerInfoList[playerInfoIndex]);
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

        [HarmonyPatch(typeof(GameNetworkManager), "SteamMatchmaking_OnLobbyCreated")]
        [HarmonyPostfix]
        private static void GNM_OnLobbyCreated(ref Steamworks.Data.Lobby lobby)
        {
            lobby.SetData("secure", PluginLoader.steamSessionKickEnabled.Value ? "t" : "f");
        }

        private static IEnumerator waitForAuthRequestOrKick(ulong clientId, float waitForDuration)
        {
            float startTime = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => Time.realtimeSinceStartup - startTime > waitForDuration || activeSessions.FindIndex(x => x.actualClientId == clientId) != -1);
            if (PluginLoader.steamSessionKickEnabled.Value && activeSessions.FindIndex(x => x.actualClientId == clientId) == -1)
            {
                string kickPrefix = "<size=12><color=red>Missing Steam Ticket:<color=white>\n";
                NetworkManager.Singleton.DisconnectClient(clientId, $"{kickPrefix}Please ensure the LobbyImprovements mod is enabled. If it is then the host may need to increase the 'Connect Kick Delay'.");
            }
        }
        [HarmonyPatch(typeof(StartOfRound), "OnClientConnect")]
        [HarmonyPostfix]
        private static void SOR_OnClientConnect(StartOfRound __instance, ulong clientId)
        {
            if (__instance.IsServer && !GameNetworkManager.Instance.disableSteam && PluginLoader.steamSessionKickEnabled.Value)
            {
                __instance.StartCoroutine(waitForAuthRequestOrKick(clientId, PluginLoader.steamSessionKickDelay.Value));
            }
        }
    }

    [HarmonyPatch]
    public class SessionTickets_Client
    {
        internal static AuthTicket currentTicket;
        internal static List<LIPlayerInfo> playerInfoList = new List<LIPlayerInfo>();

        private static Color profileIconColor = Color.clear;
        internal static void UpdatedPlayerInfo(LIPlayerInfo playerInfo)
        {
            if (StartOfRound.Instance.allPlayerScripts[playerInfo.playerClientId].actualClientId == playerInfo.actualClientId)
            {
                if (GameNetworkManager.Instance.disableSteam)
                {
                    // [LAN] Update Username
                    string text = GameNetworkManager.Instance.NoPunctuation(playerInfo.playerName ?? "");
                    text = Regex.Replace(text, "[^\\w\\._]", "");
                    if (text == string.Empty || text.Length == 0)
                    {
                        text = $"Player #{playerInfo.playerClientId}";
                    }
                    else if (text.Length <= 2)
                    {
                        text += "0";
                    }
                    else if (text.Length > 32)
                    {
                        text = text.Substring(0, 32);
                    }
                    StartOfRound.Instance.allPlayerScripts[playerInfo.playerClientId].playerUsername = text;
                    StartOfRound.Instance.allPlayerScripts[playerInfo.playerClientId].usernameBillboardText.text = text;
                    string text2 = text;
                    int numberOfDuplicateNamesInLobby = StartOfRound.Instance.allPlayerScripts[playerInfo.playerClientId].GetNumberOfDuplicateNamesInLobby();
                    if (numberOfDuplicateNamesInLobby > 0)
                    {
                        text2 = string.Format("{0}{1}", text, numberOfDuplicateNamesInLobby);
                    }
                    StartOfRound.Instance.allPlayerScripts[playerInfo.playerClientId].quickMenuManager.AddUserToPlayerList(0, text2, playerInfo.playerClientId);
                    StartOfRound.Instance.mapScreen.radarTargets[playerInfo.playerClientId].name = text2;
                }
                else
                {
                    // [Steam] Update Profile Icon
                    QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
                    PlayerListSlot playerSlot = quickMenuManager?.playerListSlots[playerInfo.playerClientId];
                    if (playerSlot != null)
                    {
                        Color targetColor = profileIconColor != Color.clear ? profileIconColor : Color.clear;
                        if (playerInfo.authResult == LIMinimalAuthResult.OK)
                        {
                            targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                        }
                        else if (playerInfo.authResult == LIMinimalAuthResult.Invalid)
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
        }

        internal static void CL_ReceivePlayerInfo(ulong senderId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string playerInfoStr);
            LIPlayerInfo playerInfo = JsonConvert.DeserializeObject<LIPlayerInfo>(playerInfoStr);

            int origIndex = playerInfoList.FindIndex(x => x.actualClientId == playerInfo.actualClientId);
            if (NetworkManager.Singleton.IsServer)
            {
                if (origIndex != -1)
                    playerInfo = playerInfoList[origIndex];
                else
                    playerInfo = null;
            }
            else
            {
                if (origIndex != -1)
                    playerInfoList[origIndex] = playerInfo;
                else
                    playerInfoList.Add(playerInfo);
            }

            if (playerInfo == null) return;

            UpdatedPlayerInfo(playerInfo);
        }
        internal static void CL_ReceiveAllPlayerInfo(ulong senderId, FastBufferReader messagePayload)
        {
            messagePayload.ReadValueSafe(out string playerInfoStr);
            try
            {
                playerInfoList = JsonConvert.DeserializeObject<List<LIPlayerInfo>>(playerInfoStr);
                foreach (LIPlayerInfo playerInfo in playerInfoList)
                {
                    UpdatedPlayerInfo(playerInfo);
                }
            }
            catch
            {
                playerInfoList.Clear();
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        private static void ConnectClientToPlayerObject(PlayerControllerB __instance)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_CL_ReceivePlayerInfo", CL_ReceivePlayerInfo);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_CL_ReceiveAllPlayerInfo", CL_ReceiveAllPlayerInfo);
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_SV_BeginAuthSession", SessionTickets_Hosting.SV_BeginAuthSession);
            }
            PluginLoader.StaticLogger.LogInfo("Registered Named Message Handlers");

            if (!GameNetworkManager.Instance.disableSteam)
            {
                currentTicket = SteamUser.GetAuthSessionTicket();
                PluginLoader.StaticLogger.LogInfo($"[SteamUser.GetAuthSessionTicket] {SteamClient.SteamId.Value}: {currentTicket.Handle}");
                int writeSize = FastBufferWriter.GetWriteSize(false) +
                    FastBufferWriter.GetWriteSize(currentTicket.Data.Length) +
                    FastBufferWriter.GetWriteSize(currentTicket.Data) +
                    FastBufferWriter.GetWriteSize(SteamClient.SteamId.Value);
                var writer = new FastBufferWriter(writeSize, Allocator.Temp);
                using (writer)
                {
                    writer.WriteValueSafe(false);
                    writer.WriteValueSafe(currentTicket.Data.Length);
                    writer.WriteBytesSafe(currentTicket.Data);
                    writer.WriteValueSafe(SteamClient.SteamId.Value);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_SV_BeginAuthSession", NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
                }
            }
            else
            {
                int writeSize = FastBufferWriter.GetWriteSize(true) +
                    FastBufferWriter.GetWriteSize((int)__instance.playerClientId) +
                    FastBufferWriter.GetWriteSize(GameNetworkManager.Instance.username);
                var writer = new FastBufferWriter(writeSize, Allocator.Temp);
                using (writer)
                {
                    writer.WriteValueSafe(true);
                    writer.WriteValueSafe((int)__instance.playerClientId);
                    writer.WriteValueSafe(GameNetworkManager.Instance.username);
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_SV_BeginAuthSession", NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetInstanceValuesBackToDefault")]
        [HarmonyPostfix]
        public static void SetInstanceValuesBackToDefault()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_SV_BeginAuthSession");
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_CL_ReceivePlayerInfo");
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LI_CL_ReceiveAllPlayerInfo");
                PluginLoader.StaticLogger.LogInfo("Unregistered Named Message Handlers");
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        [HarmonyPrefix]
        private static void StartDisconnect(GameNetworkManager __instance)
        {
            playerInfoList.Clear();

            if (!__instance.disableSteam)
            {
                if (!__instance.isHostingGame)
                {
                    currentTicket?.Cancel();
                }
                else
                {
                    foreach (LISession authSession in SessionTickets_Hosting.activeSessions)
                    {
                        SteamUser.EndAuthSession(authSession.steamId);
                    }
                    SessionTickets_Hosting.activeSessions.Clear();
                }
            }
        }
        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPrefix]
        private static void OnPlayerDC(ulong clientId)
        {
            if (!GameNetworkManager.Instance.disableSteam)
            {
                LISession authSession = SessionTickets_Hosting.activeSessions.Find(x => x.actualClientId == clientId);
                if (authSession != null)
                {
                    PluginLoader.StaticLogger.LogInfo($"[SteamUser.EndAuthSession] {authSession.steamId}");
                    SteamUser.EndAuthSession(authSession.steamId);
                    SessionTickets_Hosting.activeSessions.Remove(authSession);
                }
            }
        }
    }
}
