using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;

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
    public enum LIMinimalAuthResult
    {
        None = -1,
        OK,
        Invalid
    }

    [Serializable]
    public class SV_SteamPlayer
    {
        public ulong actualClientId;
        public SteamId steamId;
        public LIBeginAuthResult authResult1 = LIBeginAuthResult.None;
        public LIAuthResponse authResult2 = LIAuthResponse.None;
    }

    [Serializable]
    public class CL_SteamPlayer
    {
        public ulong actualClientId;
        public SteamId steamId;
        public LIMinimalAuthResult authResult1 = LIMinimalAuthResult.None;
        public LIMinimalAuthResult authResult2 = LIMinimalAuthResult.None;
    }

    [Serializable]
    public class SV_LANPlayer
    {
        public ulong actualClientId;
        public string playerName = "PlayerName";
        public string hwidToken;
        public bool banned;
        public string banReason;
    }

    [Serializable]
    public class CL_LANPlayer
    {
        public ulong actualClientId;
        public string playerName = "PlayerName";
    }

    [HarmonyPatch]
    public static class PlayerManager
    {
        internal static List<SV_SteamPlayer> sv_steamPlayers = new List<SV_SteamPlayer>();
        internal static List<CL_SteamPlayer> cl_steamPlayers = new List<CL_SteamPlayer>();

        internal static List<SV_LANPlayer> sv_lanPlayers = new List<SV_LANPlayer>();
        internal static List<CL_LANPlayer> cl_lanPlayers = new List<CL_LANPlayer>();
    }
}
