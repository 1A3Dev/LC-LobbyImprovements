using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GameNetcodeStuff;
using HarmonyLib;
using LethalModDataLib.Features;
using LethalModDataLib.Helpers;
using Newtonsoft.Json;

namespace LobbyImprovements
{
    public enum AuditLogAction
    {
        PlayerJoined,
        PlayerLeft,
        PlayerDied,
        ItemStored,
        RoundStart,
        RoundFinish,
        RoundCancel,
        EnemySpawned,
        EnemyKilled,
    }
    
    [Serializable]
    public class AuditLog
    {
        public DateTime timestamp = DateTime.UtcNow;
        public string saveFileId;
        public AuditLogAction action;
        // Enemy
        public string enemyType;
        public int enemyId;
        // Player
        public ulong targetSteamId;
        // Round
        public List<ulong> steamIds;
        public int seed;
        public string moon;
    }
    
    [HarmonyPatch]
    public class StatTracker
    {
        public static List<List<AuditLog>> oldAuditLogs = new List<List<AuditLog>>();
        public static List<AuditLog> auditLogs = new List<AuditLog>();

        public static void AddAuditLog(AuditLog auditLog)
        {
            auditLog.saveFileId = PluginLoader.saveFileStatId;
            auditLogs.Add(auditLog);
            PluginLoader.StaticLogger.LogInfo("Added to audit log: " + JsonConvert.SerializeObject(auditLog));
        }
        
        public static async Task SendAuditLogsAsync()
        {
            string json = JsonConvert.SerializeObject(auditLogs);
            auditLogs.Clear();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = "http://127.0.0.1:5002/api/games/lethal_company/stats";
                    HttpResponseMessage response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                    response.EnsureSuccessStatusCode();

                    if (oldAuditLogs.Count > 0)
                    {
                        foreach (var auditLog in oldAuditLogs.ToArray())
                        {
                            await client.PostAsync(url, new StringContent(JsonConvert.SerializeObject(auditLog), Encoding.UTF8, "application/json"));
                            oldAuditLogs.Remove(auditLog);
                        }
                    }
                }
            }
            catch (HttpRequestException e)
            {
                PluginLoader.StaticLogger.LogError(e);
                oldAuditLogs.Add(JsonConvert.DeserializeObject<List<AuditLog>>(json));
            }
        }
        
        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        private static void StartOfRound_Awake(StartOfRound __instance)
        {
            if (PluginLoader.saveFileStatId == null)
            {
                PluginLoader.saveFileStatId = Guid.NewGuid().ToString();
                SaveLoadHandler.SaveData(ModDataHelper.GetModDataKey(typeof(PluginLoader), nameof(PluginLoader.saveFileStatId)));
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "OnShipLandedMiscEvents")]
        [HarmonyPostfix]
        private static void StartOfRound_OnShipLandedMiscEvents()
        {
            AddAuditLog(new AuditLog()
            {
                action = AuditLogAction.RoundStart,
                seed = StartOfRound.Instance.randomMapSeed,
                moon = StartOfRound.Instance.currentLevel.PlanetName,
                steamIds = StartOfRound.Instance.allPlayerScripts.Select(x => x.playerSteamId).Where(x => x != 0).OrderBy(x => x).ToList()
            });
        }

        [HarmonyPatch(typeof(StartOfRound), "AutoSaveShipData")]
        [HarmonyPostfix]
        private static void StartOfRound_AutoSaveShipData()
        {
            AddAuditLog(new AuditLog()
            {
                action = AuditLogAction.RoundFinish,
                steamIds = StartOfRound.Instance.allPlayerScripts.Select(x => x.playerSteamId).Where(x => x != 0).OrderBy(x => x).ToList()
            });
            
            if (auditLogs.Count > 0)
                SendAuditLogsAsync();
        }
        
        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void StartOfRound_OnDestroy()
        {
            if (TimeOfDay.Instance != null && TimeOfDay.Instance.currentDayTimeStarted)
            {
                AddAuditLog(new AuditLog() { action = AuditLogAction.RoundCancel });
            }

            if (auditLogs.Count > 0)
                SendAuditLogsAsync();
        }
        
        [HarmonyPatch(typeof(StartOfRound), "OnPlayerConnectedClientRpc")]
        [HarmonyPostfix]
        private static void StartOfRound_OnPlayerConnectedClientRpc(StartOfRound __instance, int assignedPlayerObjectId)
        {
            AddAuditLog(new AuditLog()
            {
                action = AuditLogAction.PlayerJoined,
                targetSteamId = __instance.allPlayerScripts[assignedPlayerObjectId].playerSteamId
            });
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPostfix]
        private static void StartOfRound_OnPlayerDC(StartOfRound __instance, ref int playerObjectNumber)
        {
            PlayerControllerB component = __instance.allPlayerObjects[playerObjectNumber].GetComponent<PlayerControllerB>();
            if (component)
            {
                AddAuditLog(new AuditLog()
                {
                    action = AuditLogAction.PlayerLeft,
                    targetSteamId = component.playerSteamId
                });
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPostfix]
        private static void PlayerControllerB_KillPlayer(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
            {
                AddAuditLog(new AuditLog()
                {
                    action = AuditLogAction.PlayerDied,
                    targetSteamId = __instance.playerSteamId
                });
            }
        }
        
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        private static void EnemyAI_Start(EnemyAI __instance)
        {
            AddAuditLog(new AuditLog()
            {
                action = AuditLogAction.EnemySpawned,
                enemyType = __instance.enemyType.enemyName,
                enemyId = __instance.GetInstanceID()
            });
        }
        
        [HarmonyPatch(typeof(EnemyAI), "KillEnemy")]
        [HarmonyPostfix]
        private static void EnemyAI_KillEnemy(EnemyAI __instance)
        {
            AddAuditLog(new AuditLog()
            {
                action = AuditLogAction.EnemyKilled,
                enemyType = __instance.enemyType.enemyName,
                enemyId = __instance.GetInstanceID()
            });
        }
    }
}
