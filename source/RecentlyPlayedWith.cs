using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class RecentlyPlayedWith
    {
        internal static HashSet<ulong> PlayerList = new HashSet<ulong>();
        internal static void SetPlayedWith(ulong[] playerSteamIds, string debugType)
        {
            playerSteamIds = playerSteamIds.Where(x => x != 0f && x != SteamClient.SteamId && (debugType == "generateLevel" || !PlayerList.Contains(x))).ToArray();
            if (playerSteamIds.Length > 0)
            {
                foreach (ulong playerSteamId in playerSteamIds)
                {
                    if (!PlayerList.Contains(playerSteamId))
                        PlayerList.Add(playerSteamId);
                    SteamFriends.SetPlayedWith(playerSteamId);
                }
                PluginLoader.StaticLogger.LogInfo($"Set recently played with ({debugType}) for {playerSteamIds.Length} players.");
                PluginLoader.StaticLogger.LogDebug($"Set recently played with ({debugType}): {string.Join(", ", playerSteamIds)}");
            }
        }

        internal static bool initialJoin = true;

        [HarmonyPatch(typeof(RoundManager), "GenerateNewLevelClientRpc")]
        [HarmonyPostfix]
        private static void GenerateNewLevelClientRpc(ref RoundManager __instance)
        {
            SetPlayedWith(__instance.playersManager.allPlayerScripts.Where(x => x.isPlayerControlled || x.isPlayerDead).Select(x => x.playerSteamId).ToArray(), "generateLevel");
        }

        [HarmonyPatch(typeof(PlayerControllerB), "SendNewPlayerValuesClientRpc")]
        [HarmonyPostfix]
        private static void PlayerControllerB_SendNewPlayerValuesClientRpc(ref ulong[] playerSteamIds)
        {
            if (StartOfRound.Instance != null && (!StartOfRound.Instance.inShipPhase || PluginLoader.recentlyPlayedWithOrbit.Value))
            {
                string debugType = "otherJoined";
                if (initialJoin)
                {
                    initialJoin = false;
                    debugType = "selfJoined";
                }

                SetPlayedWith(playerSteamIds.ToArray(), debugType);
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPostfix]
        private static void StartOfRound_OnPlayerDC(ref StartOfRound __instance, ref int playerObjectNumber, ulong clientId)
        {
            ulong steamId = __instance.allPlayerScripts[playerObjectNumber].playerSteamId;
            PlayerList.Remove(steamId);
            PluginLoader.StaticLogger.LogInfo($"Removing {steamId} from recently played with.");
        }

        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void StartOfRound_OnDestroy()
        {
            initialJoin = true;
            if (PlayerList.Count > 0)
            {
                PlayerList.Clear();
                PluginLoader.StaticLogger.LogInfo($"Cleared recently played with (OnDestroy)");
            }
        }
    }
}
