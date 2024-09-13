using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyImprovements
{
    public enum LIBeginAuthResult
    {
        None = -1,
        OK,
        InvalidTicket,
        DuplicateRequest,
        InvalidVersion,
        GameMismatch,
        ExpiredTicket
    }
    public enum LIAuthResponse
    {
        None = -1,
        OK,
        UserNotConnectedToSteam,
        NoLicenseOrExpired,
        VACBanned,
        LoggedInElseWhere,
        VACCheckTimedOut,
        AuthTicketCanceled,
        AuthTicketInvalidAlreadyUsed,
        AuthTicketInvalid,
        PublisherIssuedBan
    }

    public class LISessionTicket {
        public ulong actualClientId;
        public SteamId steamId;
        public string steamName;
        public LIBeginAuthResult beginAuthResult = LIBeginAuthResult.None;
        public LIAuthResponse authResponse = LIAuthResponse.None;
    }

    [HarmonyPatch]
    public class SessionTickets_Hosting
    {
        internal static List<LISessionTicket> activeTickets = new List<LISessionTicket>();

        internal static void SV_BeginAuthSession(ulong senderId, FastBufferReader messagePayload)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                messagePayload.ReadValueSafe(out int dataLength);
                byte[] ticketData = new byte[dataLength];
                messagePayload.ReadBytesSafe(ref ticketData, dataLength);
                messagePayload.ReadValueSafe(out ulong steamId);

                BeginAuthResult response = BeginAuthResult.DuplicateRequest;
                if (!activeTickets.Any(t => t.steamId == steamId))
                {
                    //Friend friend = new Friend(steamId);
                    //string text = GameNetworkManager.Instance.NoPunctuation(friend.Name);
                    //text = Regex.Replace(text, "[^\\w\\._]", "");
                    //if (text == string.Empty || text.Length == 0)
                    //{
                    //    text = "Nameless";
                    //}
                    //else if (text.Length <= 2)
                    //{
                    //    text += "0";
                    //}
                    activeTickets.Add(new LISessionTicket()
                    {
                        actualClientId = senderId,
                        steamId = steamId,
                        //steamName = text,
                    });

                    response = SteamUser.BeginAuthSession(ticketData, steamId);
                    PluginLoader.StaticLogger.LogInfo($"[Auth_BeginSession]: {response}");

                    int foundIndex = activeTickets.FindIndex(t => t.steamId == steamId);
                    if (foundIndex != -1)
                        activeTickets[foundIndex].beginAuthResult = (LIBeginAuthResult)(int)response;
                }
                else
                {
                    PluginLoader.StaticLogger.LogInfo($"[Auth_BeginSession]: Steam ID already has a token");
                }

                PlayerControllerB[] playerControllers = StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == senderId).ToArray();
                if (playerControllers.Length > 0)
                {
                    QuickMenuManager quickMenuManager = UnityEngine.Object.FindFirstObjectByType<QuickMenuManager>();
                    UpdateProfileIconColour(quickMenuManager?.playerListSlots[playerControllers[0].playerClientId]);
                }

                if (response != BeginAuthResult.OK && PluginLoader.steamSessionTokenKick.Value)
                {
                    string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                    NetworkManager.Singleton.DisconnectClient(senderId, $"{kickPrefix}{response}");
                }
            }
        }

        public static void SteamUser_OnValidateAuthTicketResponse(SteamId steamId, SteamId steamIdOwner, AuthResponse response)
        {
            int foundIndex = activeTickets.FindIndex(t => t.steamId == steamId);
            if (foundIndex != -1)
            {
                activeTickets[foundIndex].authResponse = (LIAuthResponse)(int)response;

                PlayerControllerB[] playerControllers = StartOfRound.Instance.allPlayerScripts.Where(x => x.playerSteamId == steamId).ToArray();
                if (playerControllers.Length > 0)
                {
                    QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
                    UpdateProfileIconColour(quickMenuManager?.playerListSlots[playerControllers[0].playerClientId]);
                }

                if (response != AuthResponse.AuthTicketCanceled)
                    PluginLoader.StaticLogger.LogInfo($"[Auth_ValidateTicket] {steamId}: {response}");

                if (response != AuthResponse.OK && PluginLoader.steamSessionTokenKick.Value)
                {
                    string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                    NetworkManager.Singleton.DisconnectClient(activeTickets[foundIndex].actualClientId, $"{kickPrefix}{response}");
                    activeTickets.RemoveAt(foundIndex);
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

        private static Color profileIconColor = Color.clear;
        internal static void UpdateProfileIconColour(PlayerListSlot playerSlot)
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            if (GameNetworkManager.Instance.disableSteam) return;

            Color targetColor = Color.clear;
            if (SessionTickets_Hosting.activeTickets.Any(t => t.steamId == playerSlot.playerSteamId))
            {
                LISessionTicket sessionTicket = SessionTickets_Hosting.activeTickets.First(t => t.steamId == playerSlot.playerSteamId);
                if (sessionTicket.authResponse == LIAuthResponse.None)
                {
                    if (sessionTicket.beginAuthResult == LIBeginAuthResult.None)
                    {
                        targetColor = Color.cyan;
                        //targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                    }
                    else if (sessionTicket.beginAuthResult == LIBeginAuthResult.OK)
                    {
                        targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                    }
                    else
                    {
                        targetColor = Color.red;
                        //targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                    }
                }
                else if (sessionTicket.authResponse == LIAuthResponse.OK)
                {
                    targetColor = new Color(0f, 0.5f, 0.3f, 1f);
                }
                else
                {
                    targetColor = Color.red;
                }
            }
            else
            {
                targetColor = profileIconColor;
            }

            if (targetColor != Color.clear)
            {
                Image profileIconNameBtn = playerSlot?.slotContainer?.transform?.Find("PlayerNameButton")?.GetComponent<Image>();
                if (profileIconNameBtn)
                {
                    if (profileIconColor == Color.clear)
                        profileIconColor = profileIconNameBtn.color;

                    profileIconNameBtn.color = targetColor;
                }

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

    [HarmonyPatch]
    public class SessionTickets_Client
    {
        internal static AuthTicket currentTicket;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        private static void ConnectClientToPlayerObject()
        {
            if (!GameNetworkManager.Instance.disableSteam)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LI_SV_BeginAuthSession", SessionTickets_Hosting.SV_BeginAuthSession);
                    PluginLoader.StaticLogger.LogInfo("[Auth] Registered Named Message Handlers");
                }
                else
                {
                    currentTicket = SteamUser.GetAuthSessionTicket();
                    PluginLoader.StaticLogger.LogInfo($"[Auth_GetAuthSessionTicket]: {currentTicket.Handle}");
                    int writeSize = FastBufferWriter.GetWriteSize(currentTicket.Data.Length) + FastBufferWriter.GetWriteSize(currentTicket.Data) + FastBufferWriter.GetWriteSize(SteamClient.SteamId.Value);
                    var writer = new FastBufferWriter(writeSize, Allocator.Temp);
                    using (writer)
                    {
                        writer.WriteValueSafe(currentTicket.Data.Length);
                        writer.WriteBytesSafe(currentTicket.Data);
                        writer.WriteValueSafe(SteamClient.SteamId.Value);
                        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LI_SV_BeginAuthSession", NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
                    }
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
                PluginLoader.StaticLogger.LogInfo("[Auth] Unregistered Named Message Handlers");
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        [HarmonyPrefix]
        private static void StartDisconnect(GameNetworkManager __instance)
        {
            if (!__instance.disableSteam)
            {
                if (__instance.isHostingGame)
                {
                    foreach (LISessionTicket authSession in SessionTickets_Hosting.activeTickets)
                    {
                        SteamUser.EndAuthSession(authSession.steamId);
                    }
                    SessionTickets_Hosting.activeTickets.Clear();
                }
                else
                {
                    currentTicket?.Cancel();
                }
            }
        }
    }
}
