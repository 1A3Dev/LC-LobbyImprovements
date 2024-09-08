using System.Collections;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using HarmonyLib;
using UnityEngine.UI;
using System.Linq;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace LobbyImprovements.LANDiscovery
{
    [HarmonyPatch]
    public static class LANLobbyManager_LobbyList
    {
        public static string DiscoveryKey = "1966720_LobbyImprovements";

        internal static LANLobby[] currentLobbyList;

        internal static ClientDiscovery clientDiscovery;

        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void MenuManager_Start(MenuManager __instance)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                __instance.lanButtonContainer?.SetActive(false);
                __instance.joinCrewButtonContainer?.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "LoadServerList")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool SteamLobbyManager_LoadServerList(SteamLobbyManager __instance)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                GameObject.Find("LobbyList/ListPanel/SortPlayerCountButton")?.SetActive(false);
                TMP_Dropdown sortDropdown = GameObject.Find("LobbyList/ListPanel/Dropdown")?.GetComponent<TMP_Dropdown>();
                if (sortDropdown != null && sortDropdown.options[0].text != "ASC: Name")
                {
                    sortDropdown.ClearOptions();
                    sortDropdown.AddOptions(new List<TMP_Dropdown.OptionData>()
                    {
                        new("ASC: Name"),
                        new("DESC: Name"),
                        new("ASC: Players"),
                        new("DESC: Players"),
                    });
                    __instance.sortByDistanceSetting = sortDropdown.value;
                }
                LoadServerList_LAN(__instance);
                return false;
            }

            return true;
        }

        private static async void LoadServerList_LAN(SteamLobbyManager __instance)
        {
            if (GameNetworkManager.Instance.waitingForLobbyDataRefresh) return;

            if (!clientDiscovery)
                clientDiscovery = new ClientDiscovery();
            if (clientDiscovery.isListening) return;

            if (__instance.loadLobbyListCoroutine != null)
            {
                GameNetworkManager.Instance.StopCoroutine(__instance.loadLobbyListCoroutine);
            }

            __instance.refreshServerListTimer = 0f;
            __instance.serverListBlankText.text = "Loading server list...";
            currentLobbyList = null;
            LANLobbySlot[] array = Object.FindObjectsByType<LANLobbySlot>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < array.Length; i++)
            {
                Object.Destroy(array[i].gameObject);
            }
            GameNetworkManager.Instance.waitingForLobbyDataRefresh = true;
            clientDiscovery.listenPort = PluginLoader.lanDiscoveryPort.Value;
            LANLobby[] lobbiesArr = (await clientDiscovery.DiscoverLobbiesAsync(2f)).ToArray();
            currentLobbyList = lobbiesArr;
            GameNetworkManager.Instance.waitingForLobbyDataRefresh = false;
            if (currentLobbyList != null)
            {
                if (currentLobbyList.Length == 0)
                {
                    __instance.serverListBlankText.text = "No available servers to join.";
                }
                else
                {
                    __instance.serverListBlankText.text = "";
                }
                __instance.lobbySlotPositionOffset = 0f;
                __instance.loadLobbyListCoroutine = GameNetworkManager.Instance.StartCoroutine(loadLobbyListAndFilter(currentLobbyList, __instance));
            }
            else
            {
                PluginLoader.StaticLogger.LogInfo("Lobby list is null after request.");
                __instance.serverListBlankText.text = "No available servers to join.";
            }
        }
        private static IEnumerator loadLobbyListAndFilter(LANLobby[] lobbyList, SteamLobbyManager __instance)
        {
            bool anyResults = false;

            TMP_Dropdown sortDropdown = GameObject.Find("LobbyList/ListPanel/Dropdown")?.GetComponent<TMP_Dropdown>();
            if (sortDropdown != null)
            {
                switch (__instance.sortByDistanceSetting)
                {
                    case 0: // ASC: Name
                        lobbyList = lobbyList.OrderBy(x => x.LobbyName).ToArray();
                        break;
                    case 1: // DESC: Name
                        lobbyList = lobbyList.OrderByDescending(x => x.LobbyName).ToArray();
                        break;
                    case 2: // ASC: Players
                        lobbyList = lobbyList.OrderBy(x => x.MemberCount).ToArray();
                        break;
                    case 3: // DESC: Players
                        lobbyList = lobbyList.OrderByDescending(x => x.MemberCount).ToArray();
                        break;
                }
            }

            for (int i = 0; i < lobbyList.Length; i++)
            {
                if (!__instance.sortWithChallengeMoons && lobbyList[i].IsChallengeMoon)
                {
                    continue;
                }

                if (__instance.serverTagInputField.text != string.Empty && __instance.serverTagInputField.text != lobbyList[i].LobbyTag)
                {
                    continue;
                }

                string lobbyName = lobbyList[i].LobbyName;
                if (lobbyName.Length == 0)
                {
                    continue;
                }

                string lobbyNameNoCapitals = lobbyName.ToLower();
                if (__instance.censorOffensiveLobbyNames && PluginLoader.lobbyNameParsedBlacklist.Any(x => lobbyNameNoCapitals.Contains(x)))
                {
                    PluginLoader.StaticLogger.LogInfo("Lobby name is offensive: " + lobbyNameNoCapitals + "; skipping");
                    continue;
                }

                anyResults = true;

                GameObject original = !lobbyList[i].IsChallengeMoon ? __instance.LobbySlotPrefab : __instance.LobbySlotPrefabChallenge;
                GameObject obj = Object.Instantiate(original, __instance.levelListContainer);
                obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f + __instance.lobbySlotPositionOffset);
                __instance.lobbySlotPositionOffset -= 42f;
                LobbySlot originalSlot = obj.GetComponentInChildren<LobbySlot>();

                // NEW CODE
                LANLobbySlot componentInChildren = originalSlot.gameObject.AddComponent<LANLobbySlot>();
                Object.Destroy(originalSlot);

                if (lobbyList[i].IsChallengeMoon)
                {
                    componentInChildren.LobbyName = componentInChildren.transform.Find("ServerName (1)")?.GetComponent<TextMeshProUGUI>();
                    componentInChildren.playerCount = componentInChildren.transform.Find("NumPlayers (2)")?.GetComponent<TextMeshProUGUI>();
                    TextMeshProUGUI chalModeText = componentInChildren.transform.Find("NumPlayers (1)")?.GetComponent<TextMeshProUGUI>();
                    if (chalModeText != null)
                        chalModeText.transform.localPosition = new Vector3(120f, -11f, -7f);
                }
                else
                {
                    componentInChildren.LobbyName = componentInChildren.transform.Find("ServerName")?.GetComponent<TextMeshProUGUI>();
                    componentInChildren.playerCount = componentInChildren.transform.Find("NumPlayers")?.GetComponent<TextMeshProUGUI>();
                }

                if (componentInChildren.LobbyName)
                    componentInChildren.LobbyName.text = lobbyName.Substring(0, Mathf.Min(lobbyName.Length, 40));

                if (componentInChildren.playerCount)
                    componentInChildren.playerCount.text = $"{lobbyList[i].MemberCount} / {lobbyList[i].MaxMembers}";

                componentInChildren.HostName = componentInChildren.transform.Find("HostName")?.GetComponent<TextMeshProUGUI>();
                if (componentInChildren.HostName)
                {
                    componentInChildren.HostName.transform.localPosition = new Vector3(62f, -18.2f, -4.2f);
                    componentInChildren.HostName.GetComponent<TextMeshProUGUI>().text = $"Host: {lobbyList[i].IPAddress}:{lobbyList[i].Port}";
                    //componentInChildren.HostName.gameObject.SetActive(true);
                }

                Button JoinButton = componentInChildren.transform.Find("JoinButton")?.GetComponent<Button>();
                if (JoinButton && !componentInChildren.transform.Find("CopyCodeButton"))
                {
                    JoinButton.onClick = new Button.ButtonClickedEvent();
                    JoinButton.onClick.AddListener(componentInChildren.JoinButton);
                    LobbyCodes.AddButtonToCopyLobbyCode(JoinButton, $"{lobbyList[i].IPAddress}:{lobbyList[i].Port}", ["Copy IP", "Copied", "Invalid"]);
                }

                componentInChildren.thisLobby = lobbyList[i];

                yield return null;
            }

            if (!anyResults)
                __instance.serverListBlankText.text = "No available servers to join.";
        }
    }

    [HarmonyPatch]
    public static class LANLobbyManager_Hosting
    {
        [HarmonyPatch(typeof(MenuManager), "LAN_HostSetAllowRemoteConnections")]
        [HarmonyPostfix]
        private static void LAN_HostSetAllowRemoteConnections(MenuManager __instance)
        {
            __instance.hostSettings_LobbyPublic = true;
            __instance.lobbyTagInputField.gameObject.SetActive(__instance.hostSettings_LobbyPublic);
            __instance.privatePublicDescription.text = "PUBLIC means your game will be joinable by anyone on your network.";
        }

        [HarmonyPatch(typeof(MenuManager), "LAN_HostSetLocal")]
        [HarmonyPostfix]
        private static void LAN_HostSetLocal(MenuManager __instance)
        {
            __instance.hostSettings_LobbyPublic = false;
            __instance.lobbyTagInputField.gameObject.SetActive(__instance.hostSettings_LobbyPublic);
            __instance.privatePublicDescription.text = "PRIVATE means your game will only be joinable from your local machine.";
        }

        [HarmonyPatch(typeof(MenuManager), "HostSetLobbyPublic")]
        [HarmonyPostfix]
        private static void HostSetLobbyPublic(MenuManager __instance, bool setPublic)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                __instance.hostSettings_LobbyPublic = setPublic;
                __instance.lanSetLocalButtonAnimator.SetBool("isPressed", !setPublic);
                __instance.lanSetAllowRemoteButtonAnimator.SetBool("isPressed", setPublic);
                if (setPublic)
                {
                    __instance.LAN_HostSetAllowRemoteConnections();
                }
                else
                {
                    __instance.LAN_HostSetLocal();
                }
            }
            __instance.lobbyTagInputField.gameObject.SetActive(__instance.hostSettings_LobbyPublic);
        }

        [HarmonyPatch(typeof(MenuManager), "ClickHostButton")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void MenuManager_ClickHostButton(MenuManager __instance)
        {
            Transform lobbyHostOptions = __instance.HostSettingsScreen.transform.Find("HostSettingsContainer/LobbyHostOptions");
            if (lobbyHostOptions != null && lobbyHostOptions.transform.Find("LANOptions/AllowRemote") && GameNetworkManager.Instance.disableSteam)
            {
                Object.Destroy(lobbyHostOptions.transform.Find("LANOptions").gameObject);
                GameObject OptionsNormal = lobbyHostOptions.transform.Find("OptionsNormal").gameObject;
                GameObject menu = Object.Instantiate(OptionsNormal, OptionsNormal.transform.position, OptionsNormal.transform.rotation, OptionsNormal.transform.parent);
                __instance.HostSettingsOptionsLAN = menu;
                __instance.HostSettingsOptionsLAN.name = "LANOptions";

                Transform accessRemoteBtnParent = __instance.HostSettingsOptionsLAN.transform.Find("Public");
                __instance.lanSetAllowRemoteButtonAnimator = accessRemoteBtnParent.GetComponent<Animator>();
                Button accessRemoteBtn = accessRemoteBtnParent.GetComponent<Button>();
                accessRemoteBtn.onClick = new Button.ButtonClickedEvent();
                accessRemoteBtn.onClick.AddListener(__instance.LAN_HostSetAllowRemoteConnections);
                accessRemoteBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Public";

                Transform accessLocalBtnParent = __instance.HostSettingsOptionsLAN.transform.Find("Private");
                __instance.lanSetLocalButtonAnimator = accessLocalBtnParent.GetComponent<Animator>();
                Button accessLocalBtn = accessLocalBtnParent.GetComponent<Button>();
                accessLocalBtn.onClick = new Button.ButtonClickedEvent();
                accessLocalBtn.onClick.AddListener(__instance.LAN_HostSetLocal);
                accessLocalBtnParent.GetComponentInChildren<TextMeshProUGUI>().text = "Private";

                __instance.lobbyNameInputField = __instance.HostSettingsOptionsLAN.transform.Find("ServerNameField").GetComponent<TMP_InputField>();
                __instance.lobbyTagInputField = __instance.HostSettingsOptionsLAN.transform.Find("ServerTagInputField").GetComponent<TMP_InputField>();
            }
        }

        [HarmonyPatch(typeof(MenuManager), "EnableLeaderboardDisplay")]
        [HarmonyPostfix]
        private static void EnableLeaderboardDisplay(MenuManager __instance, bool enable)
        {
            if (enable && !__instance.requestingLeaderboard && GameNetworkManager.Instance.disableSteam)
            {
                __instance.requestingLeaderboard = true;

                int weekNumber = GameNetworkManager.Instance.GetWeekNumber();
                __instance.leaderboardHeaderText.text = "Challenge Moon " + GameNetworkManager.Instance.GetNameForWeekNumber(weekNumber) + " Results";

                __instance.ClearLeaderboard();
                __instance.leaderboardLoadingText.text = "No entries to display!";

                int entryDetails = -1;
                GameObject obj = Object.Instantiate(__instance.leaderboardSlotPrefab, __instance.leaderboardSlotsContainer);
                obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f + (float)__instance.leaderboardSlotOffset);
                __instance.leaderboardSlotOffset -= 54;
                obj.GetComponent<ChallengeLeaderboardSlot>().SetSlotValues(GameNetworkManager.Instance.username, 1, __instance.challengeScore, 0, entryDetails);

                __instance.removeScoreButton.SetActive(false);
                __instance.HostSettingsScreen.transform.Find("ChallengeLeaderboard/LobbyList (1)/ListPanel/Dropdown")?.gameObject?.SetActive(false);
                __instance.requestingLeaderboard = false;
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Update")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileMoveNext(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundTagInputField = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundTagInputField && instruction.opcode == OpCodes.Ldfld && instruction.operand?.ToString() == "TMPro.TMP_InputField lobbyTagInputField")
                    {
                        foundTagInputField = true;
                    }
                    else if (foundTagInputField && instruction.opcode == OpCodes.Brtrue)
                    {
                        CodeInstruction popInst = new CodeInstruction(OpCodes.Pop);
                        newInstructions.Add(popInst);

                        CodeInstruction alwaysTrueInst = new CodeInstruction(OpCodes.Ldc_I4_1);
                        newInstructions.Add(alwaysTrueInst);

                        alreadyReplaced = true;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.StaticLogger.LogWarning($"MenuManager_Update failed to remove tag input field code");

            return (alreadyReplaced ? newInstructions : instructions).AsEnumerable();
        }
    }

    [HarmonyPatch]
    public class LANLobbyManager_InGame
    {
        // [Client] Fix LAN Above Head Usernames
        [HarmonyPatch(typeof(NetworkSceneManager), "PopulateScenePlacedObjects")]
        [HarmonyPostfix]
        public static void Fix_LANUsernameBillboard()
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                foreach (PlayerControllerB newPlayerScript in StartOfRound.Instance.allPlayerScripts) // Fix for billboards showing as Player # with no number in LAN (base game issue)
                {
                    newPlayerScript.usernameBillboardText.text = newPlayerScript.playerUsername;
                }
            }
        }

        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPostfix]
        private static void OpenQuickMenu(QuickMenuManager __instance)
        {
            // Add the lobby player count to the pause menu
            TextMeshProUGUI CrewHeaderText = __instance.menuContainer.transform.Find("PlayerList/Image/Header").GetComponentInChildren<TextMeshProUGUI>();
            if (CrewHeaderText != null)
            {
                CrewHeaderText.text = $"CREW ({(StartOfRound.Instance?.connectedPlayersAmount ?? 0) + 1}/{StartOfRound.Instance?.allPlayerScripts.Length ?? 4}):";
            }

            // Copy the current lobby code (or ip if on lan)
            GameObject ResumeObj = __instance.menuContainer.transform.Find("MainButtons/Resume/")?.gameObject;
            if (ResumeObj != null)
            {
                GameObject LobbyCodeObj = GameObject.Find("CopyCurrentLobbyCode");
                if (LobbyCodeObj == null)
                {
                    LobbyCodeObj = Object.Instantiate(ResumeObj.gameObject, ResumeObj.transform.parent);
                    LobbyCodeObj.name = "CopyCurrentLobbyCode";

                    TextMeshProUGUI LobbyCodeTextMesh = LobbyCodeObj.GetComponentInChildren<TextMeshProUGUI>();
                    string defaultText = GameNetworkManager.Instance.disableSteam ? "> Copy IP Address" : "> Copy Lobby Code";
                    LobbyCodeTextMesh.text = defaultText;

                    Button LobbyCodeButton = LobbyCodeObj.GetComponent<Button>();
                    LobbyCodeButton.onClick = new Button.ButtonClickedEvent();
                    LobbyCodeButton.onClick.AddListener(() => {
                        string lobbyId = "";
                        if (GameNetworkManager.Instance.disableSteam)
                        {
                            if (GameNetworkManager.Instance.isHostingGame)
                            {
                                lobbyId = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.ServerListenAddress;
                                if (lobbyId == "0.0.0.0")
                                {
                                    lobbyId = LobbyCodes_LAN.GetGlobalIPAddress();
                                }
                            }
                            else
                            {
                                lobbyId = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
                            }
                        }
                        else if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.currentLobby.HasValue)
                        {
                            lobbyId = GameNetworkManager.Instance.currentLobby.Value.Id.Value.ToString();
                        }
                        LobbyCodes.CopyLobbyCodeToClipboard(lobbyId, LobbyCodeTextMesh, [defaultText, "Copied To Clipboard", "Invalid Code"]);
                    });
                }

                RectTransform rect = LobbyCodeObj.GetComponent<RectTransform>();
                if (rect == null)
                {
                    return;
                }

                GameObject DebugMenu = __instance.menuContainer.transform.Find("DebugMenu")?.gameObject;
                if (DebugMenu != null && DebugMenu.activeSelf)
                {
                    LobbyCodeObj.transform.SetParent(DebugMenu.transform);
                    rect.localPosition = new Vector3(125f, 185f, 0f);
                    rect.localScale = new Vector3(1f, 1f, 1f);
                }
                else
                {
                    LobbyCodeObj.transform.SetParent(ResumeObj.transform.parent);
                    RectTransform resumeRect = ResumeObj.GetComponent<RectTransform>();
                    rect.localPosition = resumeRect.localPosition + new Vector3(0f, 182f, 0f);
                    rect.localScale = resumeRect.localScale;
                }
            }
        }
    }
}
