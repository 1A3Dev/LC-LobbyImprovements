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
using LobbyImprovements.LANDiscovery;
using Steamworks;
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

        public static ConfigEntry<bool> steamSecureLobby;

        public static ConfigEntry<bool> recentlyPlayedWithOrbit;

        public static ConfigEntry<bool> lobbyNameFilterEnabled;
        public static ConfigEntry<bool> lobbyNameFilterDefaults;
        public static ConfigEntry<string> lobbyNameFilterWhitelist;
        public static ConfigEntry<string> lobbyNameFilterBlacklist;
        public static string[] lobbyNameParsedBlacklist;

        public static ConfigEntry<bool> lanSecureLobby;
        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;

        public static string lobbyPassword = "";

        public static void SetLobbyPassword(string password)
        {
            lobbyPassword = password;
            if (GameNetworkManager.Instance && !GameNetworkManager.Instance.disableSteam)
            {
                if (GameNetworkManager.Instance.isHostingGame && GameNetworkManager.Instance.currentLobby.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(lobbyPassword))
                        GameNetworkManager.Instance.currentLobby.Value.SetData("password", "t");
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

            steamSecureLobby = StaticConfig.Bind("Steam", "Secure Lobby", false, "Should players without a valid steam session token be kicked?");
            steamSecureLobby.SettingChanged += (sender, args) =>
            {
                if (GameNetworkManager.Instance && !GameNetworkManager.Instance.disableSteam)
                {
                    if (GameNetworkManager.Instance.isHostingGame && GameNetworkManager.Instance.currentLobby.HasValue)
                    {
                        if (steamSecureLobby.Value)
                            GameNetworkManager.Instance.currentLobby.Value.SetData("secure", "t");
                        else
                            GameNetworkManager.Instance.currentLobby.Value.DeleteData("secure");
                    }
                }
            };
            recentlyPlayedWithOrbit = StaticConfig.Bind("Steam", "Recent Players In Orbit", true, "Should players be added to the steam recent players list whilst you are in orbit? Disabling this will only add players once the ship has landed.");

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

            lanSecureLobby = StaticConfig.Bind("LAN", "Secure Lobby", false, "Should players without a valid token be kicked? Enabling this will ensure you can ban players.");
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
        private static IEnumerator loadLobbyListAndFilter(IEnumerator result)
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

        // Fix none of the lobby types showing as selected after closing the leaderboard
        [HarmonyPatch(typeof(MenuManager), "EnableLeaderboardDisplay")]
        [HarmonyPostfix]
        private static void MM_EnableLeaderboardDisplay(MenuManager __instance, bool enable)
        {
            if (!enable)
                __instance.HostSetLobbyPublic(__instance.hostSettings_LobbyPublic);
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetConnectionDataBeforeConnecting")]
        [HarmonyPostfix]
        private static void SetConnectionDataBeforeConnecting(GameNetworkManager __instance)
        {
            string origString = Encoding.ASCII.GetString(NetworkManager.Singleton.NetworkConfig.ConnectionData);
            List<string> newData = [origString];

            if (__instance.disableSteam)
            {
                newData.Add($"hwid:{SystemInfo.deviceUniqueIdentifier}");
                string playerName = ES3.Load("PlayerName", "LCGeneralSaveData", "PlayerName").Replace(',', '.');
                newData.Add($"playername:{playerName}");
            }
            else
            {
                SessionTickets_Client.currentTicket = SteamUser.GetAuthSessionTicket();
                newData.Add($"steamticket:{string.Join(';', SessionTickets_Client.currentTicket.Data)}");
            }

            string newString = string.Join(',', newData);
            //PluginLoader.StaticLogger.LogInfo(newString);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(newString);
        }

        // [Host] Added kick reasons
        public const string kickPrefixStr = "<size=12><color=red>Kicked From Lobby:<color=white>\n";
        public const string banPrefixStr = "<size=12><color=red>Banned From Lobby:<color=white>\n";
        public static Dictionary<ulong, string> steamBanReasons = new Dictionary<ulong, string>();
        [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
        [HarmonyPostfix]
        private static void Postfix(GameNetworkManager __instance, ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response)
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
                    else if (PluginLoader.lanSecureLobby.Value)
                    {
                        string kickPrefix = "<size=12><color=red>LobbyImprovements:<color=white>\n";
                        response.Reason = $"{kickPrefix}This lobby is secure which requires you to have the LobbyImprovements mod.";
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
                    if (response.Approved && array.Any(x => x.StartsWith("playername:")))
                    {
                        string value = array.First(x => x.StartsWith("playername:")).Substring(11);
                        lanPlayer.playerName = value;
                    }

                    if (response.Approved)
                        PlayerManager.sv_lanPlayers.Add(lanPlayer);
                }
                else
                {
                    if (response.Approved)
                    {
                        // Session Tickets
                        ulong steamId = (ulong)Convert.ToInt64(array[1]);
                        if (array.Any(x => x.StartsWith("steamticket:")))
                        {
                            response.Pending = true;
                            BeginAuthResult authResponse = BeginAuthResult.InvalidTicket;
                            try
                            {
                                string value = array.First(x => x.StartsWith("steamticket:"));
                                string[] stringArray = value.Substring(12).Split(';');
                                byte[] ticketData = Array.ConvertAll(stringArray, byte.Parse);
                                authResponse = SteamUser.BeginAuthSession(ticketData, steamId);
                            }
                            catch (Exception e)
                            {
                                PluginLoader.StaticLogger.LogError(e);
                            }
                            PluginLoader.StaticLogger.LogInfo($"[Steam] ConnectionApproval ({steamId}): {authResponse}");
                            if (authResponse != BeginAuthResult.OK && PluginLoader.steamSecureLobby.Value)
                            {
                                string kickPrefix = "<size=12><color=red>Invalid Steam Ticket:<color=white>\n";
                                response.Reason = $"{kickPrefix}{response}";
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
                            PluginLoader.StaticLogger.LogInfo($"[Steam] ConnectionApproval ({steamId}): MissingTicket");
                            if (PluginLoader.steamSecureLobby.Value)
                            {
                                string kickPrefix = "<size=12><color=red>Missing Steam Ticket:<color=white>\n";
                                response.Reason = $"{kickPrefix}This lobby has steam authentication enforced which requires you to have the LobbyImprovements mod.";
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

        public static int debugConnectedPlayers = -1;
        [HarmonyPatch(typeof(StartOfRound), "Update")]
        [HarmonyPostfix]
        private static void SOR_Update(StartOfRound __instance)
        {
            if (NetworkManager.Singleton && NetworkManager.Singleton.IsServer && GameNetworkManager.Instance.connectedPlayers != debugConnectedPlayers)
            {
                debugConnectedPlayers = GameNetworkManager.Instance.connectedPlayers;
                PluginLoader.StaticLogger.LogInfo($"[Connected Players] GNM: {debugConnectedPlayers} | SOR: {__instance.connectedPlayersAmount}");
            }
        }
    }
}