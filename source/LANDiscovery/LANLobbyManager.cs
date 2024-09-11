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

        private static bool lanWarningShown = false;

        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void MenuManager_Start(MenuManager __instance)
        {
            if (!__instance.isInitScene && GameNetworkManager.Instance.disableSteam)
            {
                __instance.lanButtonContainer?.SetActive(false);
                __instance.joinCrewButtonContainer?.SetActive(true);

                // Make the LAN warning only show once for each game startup
                if (lanWarningShown)
                    __instance.lanWarningContainer?.SetActive(false);
                else
                    lanWarningShown = true;
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
            if (PluginLoader.setInviteOnly)
                __instance.privatePublicDescription.text = "IP ONLY means your game will be joinable by anyone who has the ip & port.";
            else
                __instance.privatePublicDescription.text = "PUBLIC means your game will be visible on the lobby list by anyone on your network.";
            __instance.lanSetAllowRemoteButtonAnimator?.SetBool("isPressed", !PluginLoader.setInviteOnly);
            PluginLoader.setInviteOnlyButtonAnimator?.SetBool("isPressed", PluginLoader.setInviteOnly);
        }

        [HarmonyPatch(typeof(MenuManager), "LAN_HostSetLocal")]
        [HarmonyPostfix]
        private static void LAN_HostSetLocal(MenuManager __instance)
        {
            __instance.hostSettings_LobbyPublic = false;
            __instance.lobbyTagInputField.gameObject.SetActive(__instance.hostSettings_LobbyPublic);
            __instance.privatePublicDescription.text = "LOCALHOST means your game will only be joinable from your local machine.";
            PluginLoader.setInviteOnlyButtonAnimator?.SetBool("isPressed", false);
        }

        [HarmonyPatch(typeof(MenuManager), "HostSetLobbyPublic")]
        [HarmonyPostfix]
        private static void HostSetLobbyPublic(MenuManager __instance, bool setPublic)
        {
            if (GameNetworkManager.Instance.disableSteam)
            {
                if (setPublic)
                    __instance.LAN_HostSetAllowRemoteConnections();
                else
                    __instance.LAN_HostSetLocal();
            }
            else
            {
                PluginLoader.setInviteOnlyButtonAnimator?.SetBool("isPressed", PluginLoader.setInviteOnly);
                if (!setPublic)
                {
                    __instance.setPrivateButtonAnimator.SetBool("isPressed", !PluginLoader.setInviteOnly);
                    if (PluginLoader.setInviteOnly)
                        __instance.privatePublicDescription.text = "INVITE ONLY means you must send invites through Steam for players to join.";
                    else
                        __instance.privatePublicDescription.text = "FRIENDS ONLY means only friends or invited people can join.";
                }
            }
            __instance.lobbyTagInputField.gameObject.SetActive(__instance.hostSettings_LobbyPublic && !PluginLoader.setInviteOnly);
        }

        // Make the challenge leaderboard show your own stat whilst on LAN
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
        public static bool waitingForLobbyDataRefresh;
        public static LANLobby currentLobby;

        public static LANLobby GetLANLobby()
        {
            if (waitingForLobbyDataRefresh)
                return null;
            else
                return currentLobby;
        }

        internal static async void UpdateCurrentLANLobby(LANLobby foundLobby = null, bool reset = false, bool startAClient = false)
        {
            if (foundLobby == null)
            {
                if (!LANLobbyManager_LobbyList.clientDiscovery)
                    LANLobbyManager_LobbyList.clientDiscovery = new ClientDiscovery();
                if (LANLobbyManager_LobbyList.clientDiscovery.isListening) return;

                LANLobbyManager_LobbyList.clientDiscovery.listenPort = PluginLoader.lanDiscoveryPort.Value;
                waitingForLobbyDataRefresh = true;
                string lobbyIP = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
                int lobbyPort = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Port;
                foundLobby = await LANLobbyManager_LobbyList.clientDiscovery.DiscoverSpecificLobbyAsync(lobbyIP, lobbyPort, 2f);
            }
            currentLobby = foundLobby;
            if (foundLobby != null)
            {
                GameNetworkManager.Instance.steamLobbyName = currentLobby.LobbyName;
            }
            else
            {
                GameNetworkManager.Instance.steamLobbyName = "";
            }
            waitingForLobbyDataRefresh = false;

            if (startAClient)
            {
                GameObject.Find("MenuManager").GetComponent<MenuManager>().StartAClient();
            }
        }

        internal static void CopyCurrentLobbyCode(TextMeshProUGUI textMesh, string defaultText)
        {
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
            LobbyCodes.CopyLobbyCodeToClipboard(lobbyId, textMesh, [defaultText, "Copied To Clipboard", "Invalid Code"]);
        }


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

        [HarmonyPatch(typeof(QuickMenuManager), "KickUserFromServer")]
        [HarmonyPostfix]
        private static void QMM_KickUserFromServer(QuickMenuManager __instance, int playerObjId)
        {
            __instance.ConfirmKickPlayerText.text = "Kick out " + StartOfRound.Instance.allPlayerScripts[playerObjId].playerUsername.Substring(0, Mathf.Min(32, StartOfRound.Instance.allPlayerScripts[playerObjId].playerUsername.Length)) + "?";
            __instance.ConfirmKickPlayerText.fontSize = 18f;
        }

        private static string copyDefaultText = "> Copy Lobby ID";
        [HarmonyPatch(typeof(QuickMenuManager), "Start")]
        [HarmonyPrefix]
        private static void QMM_Start(QuickMenuManager __instance)
        {
            // Adds button to the ban confirmation to kick instead
            if (__instance.ConfirmKickUserPanel)
            {
                Vector3 kickBtnScale = new Vector3(0.75f, 0.75f, 1f);
                GameObject BanButtonObj = __instance.ConfirmKickUserPanel.transform.Find("Panel/Confirm")?.gameObject;
                if (BanButtonObj)
                {
                    BanButtonObj.transform.localPosition = new Vector3(90f, -6f, 2f);
                    BanButtonObj.transform.localScale = kickBtnScale;
                    BanButtonObj.GetComponentInChildren<TextMeshProUGUI>().text = "Ban";

                    Button BanButton = BanButtonObj.GetComponent<Button>();
                    BanButton.onClick = new Button.ButtonClickedEvent();
                    BanButton.onClick.AddListener(() => {
                        __instance.ConfirmKickUserFromServer();
                        if (!GameNetworkManager.Instance.disableSteam)
                        {
                            ulong playerSteamId = StartOfRound.Instance.allPlayerScripts[__instance.playerObjToKick].playerSteamId;
                            if (!StartOfRound.Instance.KickedClientIds.Contains(playerSteamId))
                            {
                                StartOfRound.Instance.KickedClientIds.Add(playerSteamId);
                            }
                        }
                    });
                }

                GameObject KickButtonObj = __instance.ConfirmKickUserPanel.transform.Find("Panel/ConfirmKick")?.gameObject;
                if (!KickButtonObj && BanButtonObj)
                {
                    KickButtonObj = Object.Instantiate(BanButtonObj.gameObject, BanButtonObj.transform.parent);
                    KickButtonObj.name = "ConfirmKick";
                    KickButtonObj.GetComponentInChildren<TextMeshProUGUI>().text = "Kick";

                    Button KickButton = KickButtonObj.GetComponent<Button>();
                    KickButton.onClick = new Button.ButtonClickedEvent();
                    KickButton.onClick.AddListener(() => {
                        __instance.ConfirmKickUserFromServer();
                        if (!GameNetworkManager.Instance.disableSteam)
                        {
                            ulong playerSteamId = StartOfRound.Instance.allPlayerScripts[__instance.playerObjToKick].playerSteamId;
                            StartOfRound.Instance.KickedClientIds.Remove(playerSteamId);
                        }
                    });
                }
                if (KickButtonObj)
                {
                    KickButtonObj.transform.localPosition = new Vector3(-90f, -6f, 2f);
                    KickButtonObj.transform.localScale = kickBtnScale;
                }

                GameObject CancelButtonObj = __instance.ConfirmKickUserPanel.transform.Find("Panel/Deny")?.gameObject;
                if (CancelButtonObj)
                {
                    CancelButtonObj.transform.localPosition = new Vector3(0f, -61f, 4f);
                    CancelButtonObj.transform.localScale = kickBtnScale;
                }
            }

            // Copy the current lobby code (or ip if on lan)
            GameObject ResumeObj = __instance.menuContainer.transform.Find("MainButtons/Resume/")?.gameObject;
            if (ResumeObj != null)
            {
                TextMeshProUGUI InviteButton = __instance.inviteFriendsTextAlpha?.GetComponentInChildren<TextMeshProUGUI>();
                if (InviteButton != null && GameNetworkManager.Instance.disableSteam)
                {
                    copyDefaultText = GameNetworkManager.Instance.disableSteam ? "> Copy Lobby IP" : "> Copy Lobby ID";
                    InviteButton.text = copyDefaultText;
                }
                else
                {
                    GameObject LobbyCodeObj = GameObject.Find("CopyCurrentLobbyCode");
                    if (LobbyCodeObj == null)
                    {
                        LobbyCodeObj = Object.Instantiate(ResumeObj.gameObject, ResumeObj.transform.parent);
                        LobbyCodeObj.name = "CopyCurrentLobbyCode";

                        TextMeshProUGUI LobbyCodeTextMesh = LobbyCodeObj.GetComponentInChildren<TextMeshProUGUI>();
                        copyDefaultText = GameNetworkManager.Instance.disableSteam ? "> Copy Lobby IP" : "> Copy Lobby ID";
                        LobbyCodeTextMesh.text = copyDefaultText;

                        Button LobbyCodeButton = LobbyCodeObj.GetComponent<Button>();
                        LobbyCodeButton.onClick = new Button.ButtonClickedEvent();
                        LobbyCodeButton.onClick.AddListener(() => CopyCurrentLobbyCode(LobbyCodeTextMesh, copyDefaultText));
                    }

                    RectTransform rect = LobbyCodeObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        GameObject DebugMenu = __instance.menuContainer.transform.Find("DebugMenu")?.gameObject;
                        if (DebugMenu != null && __instance.CanEnableDebugMenu())
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

        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPostfix]
        private static void QMM_OpenQuickMenu(QuickMenuManager __instance)
        {
            // Add the lobby player count to the pause menu
            TextMeshProUGUI CrewHeaderText = __instance.menuContainer.transform.Find("PlayerList/Image/Header").GetComponentInChildren<TextMeshProUGUI>();
            if (CrewHeaderText != null)
            {
                CrewHeaderText.fontSize = 16f;
                if (!string.IsNullOrEmpty(GameNetworkManager.Instance.steamLobbyName))
                {
                    CrewHeaderText.text = $"{GameNetworkManager.Instance.steamLobbyName}\nPlayers: {(StartOfRound.Instance?.connectedPlayersAmount ?? 0) + 1}/{StartOfRound.Instance?.allPlayerScripts.Length ?? 4}";
                }
                else
                {
                    CrewHeaderText.text = $"CREW ({(StartOfRound.Instance?.connectedPlayersAmount ?? 0) + 1}/{StartOfRound.Instance?.allPlayerScripts.Length ?? 4}):";
                }
            }
        }


        [HarmonyPatch(typeof(GameNetworkManager), "InviteFriendsUI")]
        [HarmonyPrefix]
        private static bool GNM_InviteFriendsUI(GameNetworkManager __instance)
        {
            if (__instance.disableSteam)
            {
                QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
                TextMeshProUGUI LobbyCodeTextMesh = quickMenuManager.inviteFriendsTextAlpha?.GetComponentInChildren<TextMeshProUGUI>();
                CopyCurrentLobbyCode(LobbyCodeTextMesh, copyDefaultText);
                return false;
            }

            return true;
        }
    }
}
