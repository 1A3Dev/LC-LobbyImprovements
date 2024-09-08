using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LobbyImprovements.LANDiscovery;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [BepInPlugin(modGUID, "LobbyImprovements", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private const string modGUID = "Dev1A3.LobbyImprovements";

        private readonly Harmony harmony = new Harmony(modGUID);

        private const string modVersion = "1.0.0";

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }

        public static ConfigEntry<bool> lobbyNameFilterEnabled;
        public static ConfigEntry<bool> lobbyNameFilterDefaults;
        public static ConfigEntry<string> lobbyNameFilterWhitelist;
        public static ConfigEntry<string> lobbyNameFilterBlacklist;
        public static string[] BlockedTermsRaw;

        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;

        public static int MaxPlayers = 4;
        public static int GetMaxPlayers()
        {
            if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                try { return Compatibility.MoreCompany.GetMaxPlayers(); } catch { }
            }

            return MaxPlayers;
        }

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            StaticLogger = Logger;
            StaticConfig = Config;

            lobbyNameFilterEnabled = StaticConfig.Bind("Lobby Names", "Filter Enabled", true, "Should the lobby name filter be enabled?");
            lobbyNameFilterEnabled.SettingChanged += (sender, args) =>
            {
                SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
                if (lobbyManager != null)
                {
                    UpdateLobbyNameFilter();
                }
            };
            lobbyNameFilterDefaults = StaticConfig.Bind("Lobby Names", "Default Words", true, $"Should Zeekerss' blocked words be filtered? Words: {string.Join(',', LobbyNameFilter.offensiveWords)}");
            lobbyNameFilterDefaults.SettingChanged += (sender, args) =>
            {
                UpdateLobbyNameFilter();
            };
            lobbyNameFilterWhitelist = StaticConfig.Bind("Lobby Names", "Whitelisted Terms", "", "This should be a comma-separated list.");
            lobbyNameFilterWhitelist.SettingChanged += (sender, args) =>
            {
                UpdateLobbyNameFilter();
            };
            lobbyNameFilterBlacklist = StaticConfig.Bind("Lobby Names", "Blacklisted Terms", "", "This should be a comma-separated list.");
            lobbyNameFilterBlacklist.SettingChanged += (sender, args) =>
            {
                UpdateLobbyNameFilter();
            };
            UpdateLobbyNameFilter();

            AcceptableValueRange<int> acceptablePortRange = new AcceptableValueRange<int>(1, 65535);
            lanDefaultPort = StaticConfig.Bind("LAN", "Default Port", 7777, new ConfigDescription("The port used for hosting a lobby", acceptablePortRange));
            lanDiscoveryPort = StaticConfig.Bind("LAN", "Discovery Port", 47777, new ConfigDescription("The port used for lobby discovery", acceptablePortRange));

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);

            if (Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"))
            {
                Compatibility.LobbyCompatibility.Init();
            }
        }

        private static void UpdateLobbyNameFilter()
        {
            string[] parsedWhitelist = lobbyNameFilterWhitelist.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            string[] parsedBlacklist = lobbyNameFilterBlacklist.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            BlockedTermsRaw = LobbyNameFilter.offensiveWords.Union(parsedBlacklist).Where(x => !parsedWhitelist.Contains(x)).Select(x => x.ToLower()).ToArray();

            SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.censorOffensiveLobbyNames = lobbyNameFilterEnabled.Value && BlockedTermsRaw.Length > 0;
            }
        }

        public static bool setInviteOnly = false;
        public static Animator setInviteOnlyButtonAnimator;
    }

    [HarmonyPatch]
    internal class General_Patches
    {
        // Change the placeholder text of the lobby list tag input field
        [HarmonyPatch(typeof(SteamLobbyManager), "OnEnable")]
        [HarmonyPostfix]
        private static void SLM_OnEnable(SteamLobbyManager __instance)
        {
            if (GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam)
            {
                __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter IP Address";
            }
            else
            {
                __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter tag or id...";
            }
        }

        // Adds direct connect support to the lobby list tag input field
        [HarmonyPatch(typeof(SteamLobbyManager), "RefreshServerListButton")]
        [HarmonyPrefix]
        private static bool SteamLobbyManager_RefreshServerListButton(SteamLobbyManager __instance)
        {
            if (__instance.serverTagInputField.text != string.Empty)
            {
                if (GameNetworkManager.Instance.disableSteam)
                {
                    if (LobbyCodes_LAN.TryParseIpAndPort(__instance.serverTagInputField.text, out IPAddress ipAddress, out int port))
                    {
                        LobbyCodes_LAN.JoinLobbyByIP(ipAddress.ToString(), (ushort)port);
                    }
                }
                else if (ulong.TryParse(__instance.serverTagInputField.text, out ulong lobbyId) && lobbyId.ToString().Length >= 15 && lobbyId.ToString().Length <= 20)
                {
                    __instance.StartCoroutine(LobbyCodes_Steam.JoinLobbyByID(__instance, lobbyId));
                    return false;
                }
            }

            return true;
        }

        // [Host] Notify the player that they were kicked
        [HarmonyPatch(typeof(StartOfRound), "KickPlayer")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> StartOfRound_KickPlayer(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundClientId = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundClientId && instruction.opcode == OpCodes.Ldfld && instruction.operand?.ToString() == "System.UInt64 actualClientId")
                    {
                        foundClientId = true;
                        newInstructions.Add(instruction);

                        CodeInstruction kickReason = new CodeInstruction(OpCodes.Ldstr, "You have been kicked.");
                        newInstructions.Add(kickReason);

                        continue;
                    }
                    else if (foundClientId && instruction.opcode == OpCodes.Callvirt && instruction.operand?.ToString() == "Void DisconnectClient(UInt64)")
                    {
                        alreadyReplaced = true;
                        instruction.operand = AccessTools.Method(typeof(NetworkManager), "DisconnectClient", new Type[] { typeof(UInt64), typeof(string) });
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.StaticLogger.LogWarning("KickPlayer failed to add reason");

            return (alreadyReplaced ? newInstructions : instructions).AsEnumerable();
        }

        // Make the LAN warning only show once for each game startup
        private static bool lanWarningShown = false;
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void MenuManager_Start(MenuManager __instance)
        {
            if (!__instance.isInitScene && GameNetworkManager.Instance.disableSteam)
            {
                if (lanWarningShown)
                    __instance.lanWarningContainer.SetActive(false);
                else
                    lanWarningShown = true;
            }
        }
    }
}