using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
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

        public static ConfigEntry<bool> steamSessionKickEnabled;
        public static ConfigEntry<int> steamSessionKickDelay;

        public static ConfigEntry<bool> recentlyPlayedWithOrbit;

        public static ConfigEntry<bool> lobbyNameFilterEnabled;
        public static ConfigEntry<bool> lobbyNameFilterDefaults;
        public static ConfigEntry<string> lobbyNameFilterWhitelist;
        public static ConfigEntry<string> lobbyNameFilterBlacklist;
        public static string[] lobbyNameParsedBlacklist;

        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;

        public static int GetMaxPlayers()
        {
            if ((StartOfRound.Instance?.allPlayerScripts?.Length ?? 4) > 4)
            {
                return StartOfRound.Instance.allPlayerScripts.Length;
            }
            else if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                try { return Compatibility.MoreCompany.GetMaxPlayers(); } catch { }
            }

            return 4;
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

            steamSessionKickEnabled = StaticConfig.Bind("Secure Lobby", "Enabled", false, "Should players without a valid steam session token be kicked?");
            steamSessionKickDelay = StaticConfig.Bind("Secure Lobby", "Connect Kick Delay", 2, new ConfigDescription("When someone connects, how long should they have to submit a steam session token before being kicked?", new AcceptableValueRange<int>(1, 30)));
            steamSessionKickEnabled.SettingChanged += (sender, args) =>
            {
                if (GameNetworkManager.Instance && !GameNetworkManager.Instance.disableSteam)
                {
                    if (GameNetworkManager.Instance.isHostingGame && GameNetworkManager.Instance.currentLobby.HasValue)
                    {
                        GameNetworkManager.Instance.currentLobby.Value.SetData("secure", steamSessionKickEnabled.Value ? "t" : "f");
                    }
                }
            };

            recentlyPlayedWithOrbit = StaticConfig.Bind("Recent Players", "Enabled In Orbit", true, "Should players be added to the steam recent players list whilst you are in orbit? Disabling this will only add players once the ship has landed.");

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

            StaticLogger.LogInfo("Patches Loaded");

            if (Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"))
            {
                Compatibility.LobbyCompatibility.Init();
            }
        }

        private static void UpdateLobbyNameFilter()
        {
            string[] parsedWhitelist = lobbyNameFilterWhitelist.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            string[] parsedBlacklist = lobbyNameFilterBlacklist.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            lobbyNameParsedBlacklist = LobbyNameFilter.offensiveWords.Union(parsedBlacklist).Where(x => !parsedWhitelist.Contains(x)).Select(x => x.ToLower()).ToArray();

            SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.censorOffensiveLobbyNames = lobbyNameFilterEnabled.Value && lobbyNameParsedBlacklist.Length > 0;
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
                __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter tag or ip...";
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
                        return false;
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

        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter")]
        [HarmonyPostfix]
        private static IEnumerator Postfix(IEnumerator result)
        {
            while (result.MoveNext())
                yield return result.Current;

            LobbySlot[] lobbySlots = Object.FindObjectsByType<LobbySlot>(FindObjectsSortMode.InstanceID);
            foreach (LobbySlot lobbySlot in lobbySlots)
            {
                lobbySlot.playerCount.text = $"{lobbySlot.thisLobby.MemberCount} / {lobbySlot.thisLobby.MaxMembers}";

                // Add the lobby code copy button
                Button JoinButton = lobbySlot.transform.Find("JoinButton")?.GetComponent<Button>();
                if (JoinButton && !lobbySlot.transform.Find("CopyCodeButton"))
                {
                    JoinButton.transform.localPosition = new Vector3(405f, -8.5f, -4.1f);
                    JoinButton.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                    //LobbyCodes.AddButtonToCopyLobbyCode(JoinButton, lobbySlot.lobbyId.Value.ToString(), ["Copy ID", "Copied", "Invalid"]);
                }

                // Move the text for challenge moon lobbies
                if (lobbySlot.thisLobby.GetData("chal") == "t")
                {
                    TextMeshProUGUI origChalModeText = lobbySlot.transform.Find("NumPlayers (1)")?.GetComponent<TextMeshProUGUI>();
                    origChalModeText?.gameObject?.SetActive(false);

                    GameObject chalModeTextObj = GameObject.Instantiate(lobbySlot.playerCount.gameObject, lobbySlot.transform);
                    chalModeTextObj.name = "ChalText";
                    TextMeshProUGUI chalModeText = chalModeTextObj?.GetComponent<TextMeshProUGUI>();
                    chalModeText.transform.localPosition = new Vector3(-25f, -4f, 0f);
                    chalModeText.transform.localScale = new Vector3(1f, 1f, 1f);
                    chalModeText.horizontalAlignment = HorizontalAlignmentOptions.Right;
                    chalModeText.color = Color.magenta;
                    chalModeText.alpha = 0.4f;
                    chalModeText.text = "CHALLENGE MODE";
                }

                if (lobbySlot.thisLobby.GetData("secure") == "t")
                {
                    GameObject secureTextObj = GameObject.Instantiate(lobbySlot.playerCount.gameObject, lobbySlot.transform);
                    secureTextObj.name = "SecureText";
                    TextMeshProUGUI secureText = secureTextObj?.GetComponent<TextMeshProUGUI>();
                    if (secureText != null)
                    {
                        secureText.transform.localPosition = new Vector3(-25f, -15f, 0f);
                        secureText.transform.localScale = new Vector3(1f, 1f, 1f);
                        secureText.horizontalAlignment = HorizontalAlignmentOptions.Right;
                        secureText.color = Color.green;
                        secureText.alpha = 0.4f;
                        secureText.text = "SECURE: \U00002713";
                    }
                }
            }
        }

        // [Host] Notify the player that they were kicked
        public static string kickReason = null;
        [HarmonyPatch(typeof(StartOfRound), "KickPlayer")]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
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

                        CodeInstruction kickReasonInst = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(General_Patches), "kickReason"));
                        newInstructions.Add(kickReasonInst);

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

        // [Host] Added kick reasons
        public const string kickPrefixStr = "<size=12><color=red>Kicked From Lobby:<color=white>\n";
        public const string banPrefixStr = "<size=12><color=red>Banned From Lobby:<color=white>\n";
        public static Dictionary<ulong, string> cachedBanReasons = new Dictionary<ulong, string>();
        [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
        [HarmonyPostfix]
        private static void Postfix(ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
                return;

            if (response.Reason == "You cannot rejoin after being kicked.")
            {
                string @string = Encoding.ASCII.GetString(request.Payload);
                string[] array = @string.Split(",");
                ulong steamId = (ulong)Convert.ToInt64(array[1]);
                if (cachedBanReasons.ContainsKey(steamId))
                {
                    response.Reason = cachedBanReasons[steamId];
                }
            }
        }
    }
}