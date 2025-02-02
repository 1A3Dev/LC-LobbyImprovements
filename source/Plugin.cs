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
using LethalModDataLib.Attributes;
using LethalModDataLib.Enums;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }

        public static ConfigEntry<bool> lobbySortPlayerCount;
        
        public static ConfigEntry<bool> recentlyPlayedWithOrbit;

        public static ConfigEntry<bool> lobbyNameFilterEnabled;
        public static ConfigEntry<bool> lobbyNameFilterDefaults;
        public static ConfigEntry<string> lobbyNameFilterWhitelist;
        public static ConfigEntry<string> lobbyNameFilterBlacklist;
        public static string[] lobbyNameParsedBlacklist;

        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;
        public static ConfigEntry<bool> lanIPv6Enabled;

        [ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        public static string lanPlayerName { get; set; } = "PlayerName";
        
        [ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        public static bool steamSecureLobby { get; set; } = false;

        //[ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        //public static bool steamLobbyType_Vanilla { get; set; } = true;
        //[ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        //public static bool steamLobbyType_MoreCompany { get; set; } = true;
        //[ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        //public static bool steamLobbyType_Password { get; set; } = true;

        [ModData(SaveWhen.Manual, LoadWhen.OnRegister, SaveLocation.GeneralSave)]
        public static bool lanSecureLobby { get; set; } = false;

        public static string lobbyPassword = null;

        public static void SetLobbyPassword(string password)
        {
            lobbyPassword = string.IsNullOrWhiteSpace(password) ? null : password;
            if (GameNetworkManager.Instance && !GameNetworkManager.Instance.disableSteam)
            {
                if (GameNetworkManager.Instance.isHostingGame && GameNetworkManager.Instance.currentLobby.HasValue)
                {
                    if (lobbyPassword != null)
                        GameNetworkManager.Instance.currentLobby.Value.SetData("password", "1");
                    else
                        GameNetworkManager.Instance.currentLobby.Value.DeleteData("password");
                }
            }
        }

        public static int GetMaxPlayers()
        {
            if ((StartOfRound.Instance?.allPlayerScripts?.Length ?? 4) > 4)
            {
                return StartOfRound.Instance.allPlayerScripts.Length;
            }
            else if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                try { return Compatibility.MoreCompany_Compat.GetMaxPlayers(); } catch { }
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
            
            lobbySortPlayerCount = StaticConfig.Bind("Lobby List", "Sort By Player Count", true, "Should lobbies be sorted by player count?");

            recentlyPlayedWithOrbit = StaticConfig.Bind("Steam", "Recent Players In Orbit", true, "Should players be added to the steam recent players list whilst you are in orbit? Disabling this will only add players once the ship has landed.");

            lobbyNameFilterEnabled = StaticConfig.Bind("Lobby Names", "Filter Enabled", true, "Should the lobby name filter be enabled?");
            lobbyNameFilterEnabled.SettingChanged += (_, _) =>
            {
                SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
                if (lobbyManager != null)
                {
                    UpdateLobbyNameFilter();
                }
            };
            lobbyNameFilterDefaults = StaticConfig.Bind("Lobby Names", "Default Words", true, $"Should Zeekerss' blocked words be filtered? Words: {string.Join(',', LobbyNameFilter.offensiveWords)}");
            lobbyNameFilterDefaults.SettingChanged += (_, _) =>
            {
                UpdateLobbyNameFilter();
            };
            lobbyNameFilterWhitelist = StaticConfig.Bind("Lobby Names", "Whitelisted Terms", "", "This should be a comma-separated list.");
            lobbyNameFilterWhitelist.SettingChanged += (_, _) =>
            {
                UpdateLobbyNameFilter();
            };
            lobbyNameFilterBlacklist = StaticConfig.Bind("Lobby Names", "Blacklisted Terms", "", "This should be a comma-separated list.");
            lobbyNameFilterBlacklist.SettingChanged += (_, _) =>
            {
                UpdateLobbyNameFilter();
            };
            UpdateLobbyNameFilter();

            AcceptableValueRange<int> acceptablePortRange = new AcceptableValueRange<int>(1, 65535);
            lanDefaultPort = StaticConfig.Bind("LAN", "Default Port", 7777, new ConfigDescription("The port used for hosting a server", acceptablePortRange));
            lanDiscoveryPort = StaticConfig.Bind("LAN", "Discovery Port", 47777, new ConfigDescription("The port used for lobby discovery", acceptablePortRange));
            lanIPv6Enabled = StaticConfig.Bind("LAN", "IPv6", false, "Should the server listen for IPv6 connections instead of IPv4?");

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);

            StaticLogger.LogInfo("Patches Loaded");

            if (Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"))
            {
                Compatibility.LobbyCompatibility_Compat.Init(harmony);
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
    }

    [HarmonyPatch]
    internal class General_Patches
    {
        // Change the placeholder text of the lobby list tag input field
        [HarmonyPatch(typeof(SteamLobbyManager), "OnEnable")]
        [HarmonyPostfix]
        private static void SLM_OnEnable(SteamLobbyManager __instance)
        {
            if (GameNetworkManager.Instance)
            {
                GameNetworkManager.Instance.waitingForLobbyDataRefresh = false;
                if (GameNetworkManager.Instance.disableSteam)
                {
                    __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter tag or ip...";
                    if (__instance.serverTagInputField.characterLimit < 55)
                        __instance.serverTagInputField.characterLimit = 55;
                }
                else
                {
                    __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter tag or id...";
                }
            }

            if (__instance.levelListContainer && !__instance.levelListContainer.gameObject.GetComponentInChildren<ContentSizeFitter>())
            {
                ContentSizeFitter val = __instance.levelListContainer.gameObject.AddComponent<ContentSizeFitter>();
                val.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                val.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                VerticalLayoutGroup val2 = __instance.levelListContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                val2.spacing = 6f;
                val2.childAlignment = 0;
                val2.reverseArrangement = false;
                val2.childControlHeight = false;
                val2.childControlWidth = false;
                val2.childForceExpandHeight = true;
                val2.childForceExpandWidth = true;
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

        //public static async void LoadServerList_Steam(SteamLobbyManager __instance)
        //{
        //    if (GameNetworkManager.Instance.waitingForLobbyDataRefresh) return;

        //    __instance.refreshServerListTimer = 0f;
        //    __instance.serverListBlankText.text = "Loading server list...";
        //    __instance.serverListBlankText.gameObject.SetActive(true);
        //    __instance.currentLobbyList = null;
        //    GameNetworkManager.Instance.waitingForLobbyDataRefresh = true;
        //    LobbySlot[] array = Object.FindObjectsByType<LobbySlot>(FindObjectsSortMode.InstanceID);
        //    for (int i = 0; i < array.Length; i++)
        //    {
        //        Object.Destroy(array[i].gameObject);
        //    }

        //    List<Lobby> combinedLobbies = new List<Lobby>();
        //    bool hasMoreCompany = Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany");

        //    if (PluginLoader.steamLobbyType_Vanilla)
        //    {
        //        LobbyQuery lobbyQuery = SteamMatchmaking.LobbyList
        //            .WithMaxResults(50)
        //            .WithSlotsAvailable(1)
        //            .WithKeyValue("vers", GameNetworkManager.Instance.gameVersionNum.ToString())
        //            .WithEqual("password", 0)
        //            .WithLower("maxplayers", 5);
        //        if (!__instance.sortWithChallengeMoons)
        //            lobbyQuery = lobbyQuery.WithKeyValue("chal", "f");
        //        if (__instance.serverTagInputField.text != string.Empty)
        //            lobbyQuery = lobbyQuery.WithKeyValue("tag", __instance.serverTagInputField.text);
        //        Lobby[] tempLobbies = await lobbyQuery.RequestAsync();
        //        if (tempLobbies != null && tempLobbies.Length > 0)
        //            combinedLobbies.AddRange(tempLobbies);
        //        PluginLoader.StaticLogger.LogInfo($"[Lobby Count] Vanilla: {tempLobbies?.Length ?? 0}");
        //    }

        //    if (hasMoreCompany && PluginLoader.steamLobbyType_MoreCompany)
        //    {
        //        LobbyQuery lobbyQuery = SteamMatchmaking.LobbyList
        //            .WithMaxResults(50)
        //            .WithSlotsAvailable(1)
        //            .WithKeyValue("vers", GameNetworkManager.Instance.gameVersionNum.ToString())
        //            .WithEqual("password", 0)
        //            .WithHigher("maxplayers", 4);
        //        if (!__instance.sortWithChallengeMoons)
        //            lobbyQuery = lobbyQuery.WithKeyValue("chal", "f");
        //        if (__instance.serverTagInputField.text != string.Empty)
        //            lobbyQuery = lobbyQuery.WithKeyValue("tag", __instance.serverTagInputField.text);
        //        Lobby[] tempLobbies = await lobbyQuery.RequestAsync();
        //        if (tempLobbies != null && tempLobbies.Length > 0)
        //            combinedLobbies.AddRange(tempLobbies);
        //        PluginLoader.StaticLogger.LogInfo($"[Lobby Count] MoreCompany: {tempLobbies?.Length ?? 0}");
        //    }

        //    if (PluginLoader.steamLobbyType_Password)
        //    {
        //        LobbyQuery lobbyQuery = SteamMatchmaking.LobbyList
        //            .WithMaxResults(50)
        //            .WithSlotsAvailable(1)
        //            .WithKeyValue("vers", GameNetworkManager.Instance.gameVersionNum.ToString())
        //            .WithEqual("password", 1);
        //        if (!__instance.sortWithChallengeMoons)
        //            lobbyQuery = lobbyQuery.WithKeyValue("chal", "f");
        //        if (__instance.serverTagInputField.text != string.Empty)
        //            lobbyQuery = lobbyQuery.WithKeyValue("tag", __instance.serverTagInputField.text);
        //        if (!hasMoreCompany)
        //            lobbyQuery = lobbyQuery.WithLower("maxplayers", 5);
        //        Lobby[] tempLobbies = await lobbyQuery.RequestAsync();
        //        if (tempLobbies != null && tempLobbies.Length > 0)
        //            combinedLobbies.AddRange(tempLobbies);
        //        PluginLoader.StaticLogger.LogInfo($"[Lobby Count] Password: {tempLobbies?.Length ?? 0}");
        //    }

        //    __instance.currentLobbyList = combinedLobbies.GroupBy(x => x.Id).Select(x => x.First()).ToArray();

        //    if (__instance.currentLobbyList != null && __instance.currentLobbyList.Length != 0)
        //    {
        //        __instance.serverListBlankText.text = "";
        //        __instance.lobbySlotPositionOffset = 0f;
        //        __instance.loadLobbyListCoroutine = __instance.StartCoroutine(__instance.loadLobbyListAndFilter(__instance.currentLobbyList));
        //    }
        //    else
        //    {
        //        __instance.serverListBlankText.text = "No available servers to join.";
        //        GameNetworkManager.Instance.waitingForLobbyDataRefresh = false;
        //    }
        //}

        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter")]
        [HarmonyPrefix]
        private static void loadLobbyListAndFilter_Prefix(ref Lobby[] lobbyList)
        {
            if (PluginLoader.lobbySortPlayerCount.Value)
            {
                Array.Sort(lobbyList, (x, y) =>
                {
                    if (x.MemberCount == y.MemberCount)
                    {
                        if (x.MaxMembers == y.MaxMembers)
                        {
                            return x.GetData("name").CompareTo(y.GetData("name"));
                        }
                        else
                        {
                            return y.MaxMembers - x.MaxMembers;
                        }
                    }
                    else
                    {
                        return y.MemberCount - x.MemberCount;
                    }
                });
            }
        }
        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter")]
        [HarmonyPostfix]
        private static IEnumerator loadLobbyListAndFilter(IEnumerator result)
        {
            SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
            lobbyManager?.serverListBlankText?.gameObject?.SetActive(false);

            while (result.MoveNext())
                yield return result.Current;

            lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
            lobbyManager?.serverListBlankText?.gameObject?.SetActive(lobbyManager.serverListBlankText.text != string.Empty);
            GameNetworkManager.Instance.waitingForLobbyDataRefresh = false;

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

                List<string> tags = new List<string>();
                if (lobbySlot.thisLobby.GetData("chal") == "t")
                {
                    TextMeshProUGUI origChalModeText = lobbySlot.transform.Find("NumPlayers (1)")?.GetComponent<TextMeshProUGUI>();
                    origChalModeText?.gameObject?.SetActive(false);

                    tags.Add("<color=purple>CHALLENGE</color>");
                }

                if (lobbySlot.thisLobby.GetData("password") == "1")
                    tags.Add("<color=yellow>PASSWORD</color>");
                
                if (lobbySlot.thisLobby.GetData("li_secure") == "1")
                    tags.Add("<color=green>SECURE</color>");
                
                GameObject lobbyTagsObj = GameObject.Instantiate(lobbySlot.playerCount.gameObject, lobbySlot.transform);
                lobbyTagsObj.name = "TagsText";
                TextMeshProUGUI lobbyTagsText = lobbyTagsObj?.GetComponent<TextMeshProUGUI>();
                if (lobbyTagsText != null)
                {
                    lobbyTagsText.transform.localPosition = new Vector3(-25f, -15f, 0f);
                    lobbyTagsText.transform.localScale = new Vector3(1f, 1f, 1f);
                    lobbyTagsText.horizontalAlignment = HorizontalAlignmentOptions.Right;
                    lobbyTagsText.alpha = 0.4f;
                    lobbyTagsText.text = string.Join(", ", tags);
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

        // Fix none of the lobby types showing as selected after closing the leaderboard
        [HarmonyPatch(typeof(MenuManager), "EnableLeaderboardDisplay")]
        [HarmonyPostfix]
        private static void MM_EnableLeaderboardDisplay(MenuManager __instance, bool enable)
        {
            if (!enable)
                __instance.HostSetLobbyPublic(__instance.hostSettings_LobbyPublic);
        }

        private static string parseConnectionData(string[] array)
        {
            return string.Join(',', array.Select(x => 
                x.StartsWith("ticket:") ? $"ticket:{x.Substring(7).Length}" :
                x.StartsWith("hwid:") ? $"hwid:{x.Substring(5).Length}" :
                x.StartsWith("password:") ? $"password:{x.Substring(9).Length}" : x
            ));
        }
        
        [HarmonyPatch(typeof(GameNetworkManager), "SetConnectionDataBeforeConnecting")]
        [HarmonyPostfix]
        private static void SetConnectionDataBeforeConnecting(GameNetworkManager __instance)
        {
            string origString = Encoding.ASCII.GetString(NetworkManager.Singleton.NetworkConfig.ConnectionData);
            List<string> newData = [origString];

            if (!string.IsNullOrWhiteSpace(HostingUI.protectedLobbyPassword))
            {
                newData.Add($"password:{HostingUI.protectedLobbyPassword}");
                HostingUI.protectedLobbyPassword = null;
            }
            else
            {
                newData.Add("password:");
            }

            if (__instance.disableSteam)
            {
                newData.Add($"playername:{PluginLoader.lanPlayerName.Replace(',', '.')}");
                newData.Add($"hwid:{SystemInfo.deviceUniqueIdentifier}");
            }
            else
            {
                SessionTickets_Client.currentTicket = SteamUser.GetAuthSessionTicket();
                newData.Add($"ticket:{string.Join(';', SessionTickets_Client.currentTicket.Data)}");
            }

            PluginLoader.StaticLogger.LogInfo("SetConnectionDataBeforeConnecting: " + parseConnectionData(newData.ToArray()));
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(string.Join(',', newData));
        }

        // [Host] Remove the vanilla "Connection approval callback! Game version of client request:" debug log
        [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ConnectionApproval_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (instruction.opcode == OpCodes.Ldstr && instruction.operand?.ToString() == "Joining client id: {0}; Local/host client id: {1}")
                    {
                        alreadyReplaced = true;
                    }
                }
                
                if (alreadyReplaced)
                    newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.StaticLogger.LogWarning("ConnectionApproval failed to replace log");

            return alreadyReplaced ? newInstructions.AsEnumerable() : instructions;
        }
        // [Host] Send the "Connection approval callback! Game version of client request:" debug log but with certain data redacted
        [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
        [HarmonyPrefix]
        private static void ConnectionApproval_Prefix(ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response)
        {
            string @string = Encoding.ASCII.GetString(request.Payload);
            string[] array = @string.Split(",");
            Debug.Log("Connection approval callback! Game version of client request: " + parseConnectionData(array));
        }

        // [Host] Added kick reasons
        public const string kickPrefixStr = "<size=12><color=red>Kicked From Lobby:<color=white>\n";
        public const string banPrefixStr = "<size=12><color=red>Banned From Lobby:<color=white>\n";
        public static Dictionary<ulong, string> steamBanReasons = new Dictionary<ulong, string>();
        [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
        [HarmonyPostfix]
        private static void ConnectionApproval_Postfix(GameNetworkManager __instance, ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
                return;

            string @string = Encoding.ASCII.GetString(request.Payload);
            string[] array = @string.Split(",");

            string vanillaKickReason = "You cannot rejoin after being kicked.";
            if (!response.Approved && response.Reason == vanillaKickReason)
            {
                ulong steamId = (ulong)Convert.ToInt64(array[1]);
                if (steamBanReasons.ContainsKey(steamId))
                    response.Reason = steamBanReasons[steamId];
            }
            else
            {
                SV_LANPlayer lanPlayer = new SV_LANPlayer() { actualClientId = request.ClientNetworkId };
                if (response.Approved && __instance.disableSteam)
                {
                    if (array.Any(x => x.StartsWith("hwid:")))
                    {
                        lanPlayer.hwidToken = array.First(x => x.StartsWith("hwid:")).Substring(5);
                        if (PlayerManager.sv_lanPlayers.Any(x => x.hwidToken == lanPlayer.hwidToken && x.banned))
                        {
                            SV_LANPlayer lanPlayerOrig = PlayerManager.sv_lanPlayers.FindLast(x => x.hwidToken == lanPlayer.hwidToken && x.banned);
                            if (!string.IsNullOrWhiteSpace(lanPlayerOrig.banReason))
                                response.Reason = lanPlayerOrig.banReason;
                            else
                                response.Reason = vanillaKickReason;

                            response.Approved = false;
                        }
                    }
                    else if (PluginLoader.lanSecureLobby)
                    {
                        string kickPrefix = "<size=12><color=red>LobbyImprovements:<color=white>\n";
                        response.Reason = $"{kickPrefix}This lobby requires you to have the LobbyImprovements mod.";
                        response.Approved = false;
                    }
                }

                if (response.Approved && !string.IsNullOrWhiteSpace(PluginLoader.lobbyPassword))
                {
                    // Password Protected
                    string kickPrefix = "<size=12><color=red>Password Protection:<color=white>\n";
                    if (array.Any(x => x.StartsWith("password:")))
                    {
                        string value = array.First(x => x.StartsWith("password:")).Substring(9);
                        if (PluginLoader.lobbyPassword != value)
                        {
                            response.Reason = $"{kickPrefix}You have entered an incorrect password.";
                            response.Approved = false;
                        }
                    }
                    else
                    {
                        response.Reason = $"{kickPrefix}This lobby is password protected which requires you to have the LobbyImprovements mod.";
                        response.Approved = false;
                    }
                }

                if (__instance.disableSteam)
                {
                    if (response.Approved)
                    {
                        if (array.Any(x => x.StartsWith("playername:")))
                        {
                            string value = array.First(x => x.StartsWith("playername:")).Substring(11);
                            lanPlayer.playerName = value;
                        }
                        PlayerManager.sv_lanPlayers.Add(lanPlayer);
                    }
                }
                else
                {
                    if (response.Approved)
                    {
                        // Session Tickets
                        ulong steamId = (ulong)Convert.ToInt64(array[1]);
                        if (array.Any(x => x.StartsWith("ticket:")) && PluginLoader.steamSecureLobby) //TODO: steamSecureLobby here blocks checking if the option is disabled, this is to allow testing it first
                        {
                            response.Pending = true;
                            BeginAuthResult authResponse = BeginAuthResult.InvalidTicket;
                            try
                            {
                                string value = array.First(x => x.StartsWith("ticket:"));
                                string[] stringArray = value.Substring(7).Split(';');
                                byte[] ticketData = Array.ConvertAll(stringArray, byte.Parse);
                                authResponse = SteamUser.BeginAuthSession(ticketData, steamId);
                            }
                            catch (Exception e)
                            {
                                PluginLoader.StaticLogger.LogError(e);
                            }
                            PluginLoader.StaticLogger.LogInfo($"[Steam] ConnectionApproval ({steamId}): {authResponse}");
                            if (authResponse != BeginAuthResult.OK && PluginLoader.steamSecureLobby)
                            {
                                string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                                response.Reason = $"{kickPrefix}{authResponse}";
                                response.Approved = false;
                            }
                            else
                            {
                                ulong clientId = request.ClientNetworkId;
                                if (!PlayerManager.sv_steamPlayers.Any(t => t.actualClientId == clientId))
                                {
                                    PlayerManager.sv_steamPlayers.Add(new SV_SteamPlayer()
                                    {
                                        actualClientId = clientId,
                                        steamId = steamId,
                                        authResult1 = (LIBeginAuthResult)authResponse,
                                    });
                                }
                            }

                            response.Pending = false;
                        }
                        else
                        {
                            if (PluginLoader.steamSecureLobby)
                            {
                                PluginLoader.StaticLogger.LogInfo($"[Steam] ConnectionApproval ({steamId}): MissingTicket");
                                string kickPrefix = "<size=12><color=red>Missing Steam Ticket:<color=white>\n";
                                response.Reason = $"{kickPrefix}This lobby requires you to have the LobbyImprovements mod.";
                                response.Approved = false;
                            }
                            else
                            {
                                ulong clientId = request.ClientNetworkId;
                                if (!PlayerManager.sv_steamPlayers.Any(t => t.actualClientId == clientId))
                                {
                                    PlayerManager.sv_steamPlayers.Add(new SV_SteamPlayer()
                                    {
                                        actualClientId = clientId,
                                        steamId = steamId,
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    //[HarmonyPatch]
    //public class LoadServerListTranspiler
    //{
    //    static MethodBase TargetMethod()
    //    {
    //        var stateMachineType = AccessTools.TypeByName("SteamLobbyManager+<LoadServerList>d__19");
    //        return AccessTools.Method(stateMachineType, "MoveNext");
    //    }

    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        int foundAssignment = 0;
    //        foreach (var instruction in instructions)
    //        {
    //            PluginLoader.StaticLogger.LogInfo($"[{instruction.opcode}] {instruction.operand?.ToString()} [{string.Join(',', instruction.labels)}]");

    //            if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo fieldInfo && fieldInfo.Name == "currentLobbyList")
    //            {
    //                foundAssignment++;

    //                yield return instruction;

    //                //if (foundAssignment == 3)
    //                //{
    //                //    CodeInstruction instruction1 = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LoadServerListTranspiler), nameof(LocalAsyncMethod)));
    //                //    CodeInstruction instruction2 = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Task), nameof(Task.GetAwaiter)));
    //                //    CodeInstruction instruction3 = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(System.Runtime.CompilerServices.TaskAwaiter), nameof(System.Runtime.CompilerServices.TaskAwaiter.GetResult)));
    //                //    CodeInstruction instruction4 = new CodeInstruction(OpCodes.Pop);
    //                //    PluginLoader.StaticLogger.LogWarning($"[{instruction1.opcode}] {instruction1.operand?.ToString()} [{string.Join(',', instruction1.labels)}]");
    //                //    PluginLoader.StaticLogger.LogWarning($"[{instruction2.opcode}] {instruction2.operand?.ToString()} [{string.Join(',', instruction2.labels)}]");
    //                //    PluginLoader.StaticLogger.LogWarning($"[{instruction3.opcode}] {instruction3.operand?.ToString()} [{string.Join(',', instruction3.labels)}]");
    //                //    PluginLoader.StaticLogger.LogWarning($"[{instruction4.opcode}] {instruction4.operand?.ToString()} [{string.Join(',', instruction4.labels)}]");
    //                //    yield return instruction1;
    //                //    yield return instruction2;
    //                //    yield return new CodeInstruction(OpCodes.Stloc_S, 4);
    //                //    yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
    //                //    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(System.Runtime.CompilerServices.TaskAwaiter), nameof(System.Runtime.CompilerServices.TaskAwaiter.IsCompleted)));
    //                //    yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
    //                //    yield return instruction3;
    //                //    yield return instruction4;
    //                //    yield return new CodeInstruction(OpCodes.Ldloc_0);
    //                //}
    //            }
    //            else
    //            {
    //                yield return instruction;
    //            }
    //        }
    //    }

    //    public static async Task LocalAsyncMethod()
    //    {
    //        PluginLoader.StaticLogger.LogInfo("[TEST1] Async operation started.");
    //        await Task.Delay(10000);
    //        PluginLoader.StaticLogger.LogInfo("[TEST1] Async operation completed.");
    //    }
    //}
}