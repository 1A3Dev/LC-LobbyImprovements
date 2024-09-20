using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;
using Lobby = Steamworks.Data.Lobby;
using System.Linq;
using BepInEx.Bootstrap;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class HostingUI
    {
        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        private static void MenuManager_Awake(MenuManager __instance)
        {
            if (!__instance.isInitScene)
            {
                Transform hostSettingsContainer = __instance.HostSettingsScreen.transform.Find("HostSettingsContainer");
                Transform lobbyHostOptions = hostSettingsContainer?.Find("LobbyHostOptions");
                if (lobbyHostOptions != null)
                {
                    Transform filesPanel = __instance.HostSettingsScreen.transform.Find("FilesPanel");
                    GameObject liPanel = GameObject.Instantiate(filesPanel.gameObject, filesPanel.parent);
                    liPanel.name = "LIPanel_Host";
                    TextMeshProUGUI panelTitle = liPanel.transform.Find("EnterAName").gameObject.GetComponent<TextMeshProUGUI>();
                    panelTitle.transform.localPosition = new Vector3(0f, 100f, 0f);
                    panelTitle.fontSize = 14f;
                    panelTitle.text = "LobbyImprovements";
                    liPanel.transform.localPosition = new Vector3(-252.0869f, -5.648f, -2.785f);
                    for (int i = liPanel.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = liPanel.transform.GetChild(i);
                        if (child.name != "EnterAName" && child.name != "Darken" && child.name != "Outline")
                        {
                            GameObject.Destroy(child.gameObject);
                        }
                    }

                    // Field Labels
                    GameObject serverNameLabel = lobbyHostOptions.Find("OptionsNormal/EnterAName")?.gameObject;
                    if (serverNameLabel != null)
                    {
                        // Main Panel
                        RectTransform serverNameRect = serverNameLabel.GetComponent<RectTransform>();
                        serverNameRect.anchorMin = new Vector2(0.5f, 0.5f);
                        serverNameRect.anchorMax = new Vector2(0.5f, 0.5f);
                        serverNameRect.pivot = new Vector2(0.5f, 0.5f);
                        serverNameLabel.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                        serverNameLabel.transform.localPosition = new Vector3(0f, 30f, 0f);
                        serverNameLabel.GetComponent<TextMeshProUGUI>().text = "Server Name:";

                        GameObject serverAccessLabel = GameObject.Instantiate(serverNameLabel.gameObject, serverNameLabel.transform.parent);
                        serverAccessLabel.name = "EnterAccess";
                        serverAccessLabel.transform.localPosition = new Vector3(0f, 70f, 0f);
                        serverAccessLabel.GetComponent<TextMeshProUGUI>().text = "Server Access:";

                        GameObject serverTagLabel = GameObject.Instantiate(serverNameLabel.gameObject, serverNameLabel.transform.parent);
                        serverTagLabel.name = "EnterATag";
                        serverTagLabel.transform.localPosition = new Vector3(0f, -10f, 0f);
                        serverTagLabel.GetComponent<TextMeshProUGUI>().text = "Server Tag:";

                        // Left Panel
                        GameObject serverPasswordLabel = GameObject.Instantiate(serverNameLabel.gameObject, liPanel.transform);
                        serverPasswordLabel.name = "EnterAPassword";
                        serverPasswordLabel.transform.localPosition = new Vector3(0f, 70f, 0f);
                        serverPasswordLabel.GetComponent<TextMeshProUGUI>().fontSize = 12f;
                        serverPasswordLabel.GetComponent<TextMeshProUGUI>().text = "Server Password:";
                    }

                    // Text Fields
                    GameObject serverTagObject = lobbyHostOptions.Find("OptionsNormal/ServerTagInputField")?.gameObject;
                    if (serverTagObject != null)
                    {
                        RectTransform serverTagRect = serverTagObject.GetComponent<RectTransform>();
                        serverTagRect.sizeDelta = new Vector2(310.55f, 30f);
                        serverTagRect.anchorMin = new Vector2(0.5f, 0.5f);
                        serverTagRect.anchorMax = new Vector2(0.5f, 0.5f);
                        serverTagRect.pivot = new Vector2(0.5f, 0.5f);
                        serverTagObject.transform.localPosition = new Vector3(0f, -30f, 0f);
                        serverTagObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().fontSize = 16f;
                        serverTagObject.transform.Find("Text Area/Text").GetComponent<TextMeshProUGUI>().fontSize = 16f;
                        __instance.lobbyTagInputField = serverTagObject.GetComponent<TMP_InputField>();

                        if (serverTagObject.transform.parent.Find("ServerNameField") != null)
                        {
                            GameObject.Destroy(serverTagObject.transform.parent.Find("ServerNameField")?.gameObject);

                            GameObject serverNameObject = GameObject.Instantiate(serverTagObject.gameObject, serverTagObject.transform.parent);
                            serverNameObject.name = "ServerNameField";
                            serverNameObject.transform.localPosition = new Vector3(0f, 10f, 0f);
                            serverNameObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().text = "Enter a name...";
                            __instance.lobbyNameInputField = serverNameObject.GetComponent<TMP_InputField>();
                        }

                        if (liPanel.transform.Find("ServerPasswordField") == null)
                        {
                            GameObject serverPasswordObject = GameObject.Instantiate(serverTagObject.gameObject, liPanel.transform);
                            serverPasswordObject.name = "ServerPasswordField";
                            serverPasswordObject.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 30f);
                            serverPasswordObject.transform.localPosition = new Vector3(0f, 50f, 0f);
                            TextMeshProUGUI placeholderText = serverPasswordObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>();
                            placeholderText.text = "None";
                            placeholderText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                            serverPasswordObject.transform.Find("Text Area/Text").GetComponent<TextMeshProUGUI>().horizontalAlignment = HorizontalAlignmentOptions.Center;
                            serverPasswordObject.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;
                        }
                    }

                    // Dropdown Fields
                    GameObject dropdownObj = GameObject.Find("Canvas/MenuContainer/LobbyList/ListPanel/Dropdown");
                    if (dropdownObj != null)
                    {
                        if (lobbyHostOptions.Find("OptionsNormal/ServerAccessDropdown") == null)
                        {
                            GameObject privacyDropdownObj = GameObject.Instantiate(dropdownObj, lobbyHostOptions.Find("OptionsNormal"));
                            privacyDropdownObj.name = "ServerAccessDropdown";
                            RectTransform privacyDropdownRect = privacyDropdownObj.GetComponent<RectTransform>();
                            privacyDropdownRect.sizeDelta = new Vector2(310.55f, 30f);
                            privacyDropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
                            privacyDropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
                            privacyDropdownRect.pivot = new Vector2(0.5f, 0.5f);
                            privacyDropdownObj.transform.localScale = new Vector3(0.7172f, 0.7172f, 0.7172f);
                            privacyDropdownObj.transform.localPosition = new Vector3(0f, 50f, 0f);

                            TMP_Dropdown privacyDropdown = privacyDropdownObj.GetComponent<TMP_Dropdown>();
                            privacyDropdown.ClearOptions();
                            if (GameNetworkManager.Instance.disableSteam)
                                privacyDropdown.AddOptions(["Public", "IP-only", "Localhost"]);
                            else
                                privacyDropdown.AddOptions(["Public", "Friends-only", "Invite-only"]);
                            privacyDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                            privacyDropdown.value = 0;
                            privacyDropdown.onValueChanged.AddListener((index) =>
                            {
                                PluginLoader.setInviteOnly = false;
                                if (GameNetworkManager.Instance.disableSteam)
                                {
                                    switch (index)
                                    {
                                        case 1:
                                            PluginLoader.setInviteOnly = true;
                                            __instance.HostSetLobbyPublic(true);
                                            break;
                                        case 2:
                                            __instance.HostSetLobbyPublic(false);
                                            break;
                                        default:
                                            __instance.HostSetLobbyPublic(true);
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (index)
                                    {
                                        case 1:
                                            __instance.HostSetLobbyPublic(false);
                                            break;
                                        case 2:
                                            PluginLoader.setInviteOnly = true;
                                            __instance.HostSetLobbyPublic(false);
                                            break;
                                        default:
                                            __instance.HostSetLobbyPublic(true);
                                            break;
                                    }
                                }
                            });
                        }
                    }

                    GameObject checkboxObj = GameObject.Find("Canvas/MenuContainer/LobbyList/ListPanel/ToggleChallengeSort");
                    if (checkboxObj != null)
                    {
                        if (liPanel.transform.Find("ServerSecureToggle") == null)
                        {
                            GameObject secureToggleObj = GameObject.Instantiate(checkboxObj, liPanel.transform);
                            secureToggleObj.name = "ServerSecureToggle";
                            RectTransform secureToggleRect = secureToggleObj.GetComponent<RectTransform>();
                            secureToggleRect.sizeDelta = new Vector2(200f, 30f);
                            secureToggleRect.anchorMin = new Vector2(0.5f, 0.5f);
                            secureToggleRect.anchorMax = new Vector2(0.5f, 0.5f);
                            secureToggleRect.pivot = new Vector2(0.5f, 0.5f);
                            secureToggleObj.transform.localScale = new Vector3(0.7172f, 0.7172f, 0.7172f);
                            secureToggleObj.transform.localPosition = new Vector3(0f, 20f, 0f);
                            TextMeshProUGUI secureToggleText = secureToggleObj.GetComponentInChildren<TextMeshProUGUI>();
                            secureToggleText.GetComponent<RectTransform>().sizeDelta = new Vector2(175f, 30f);
                            secureToggleText.transform.localPosition = new Vector3(-4f, 0f, 0f);
                            secureToggleText.fontSize = 12f;
                            secureToggleText.text = GameNetworkManager.Instance.disableSteam ? "Validate Client Tokens:" : "Validate Steam Sessions:";
                            Image secureToggleIcon = secureToggleObj.transform.Find("Arrow (1)").GetComponentInChildren<Image>();
                            Button secureToggleBtn = secureToggleObj.GetComponentInChildren<Button>();
                            secureToggleBtn.onClick = new Button.ButtonClickedEvent();
                            secureToggleBtn.onClick.AddListener(() =>
                            {
                                if (GameNetworkManager.Instance.disableSteam)
                                {
                                    PluginLoader.lanSecureLobby.Value = !PluginLoader.lanSecureLobby.Value;
                                    secureToggleIcon.enabled = PluginLoader.lanSecureLobby.Value;
                                }
                                else
                                {
                                    PluginLoader.steamSecureLobby.Value = !PluginLoader.steamSecureLobby.Value;
                                    secureToggleIcon.enabled = PluginLoader.steamSecureLobby.Value;
                                }
                                PluginLoader.StaticConfig.Save();
                            });

                            if (secureToggleObj.transform.Find("ServerMaxPlayers") == null)
                            {
                                GameObject maxPlayersParentObj = GameObject.Instantiate(secureToggleObj, secureToggleObj.transform.parent);
                                maxPlayersParentObj.name = "ServerMaxPlayers";
                                maxPlayersParentObj.GetComponentInChildren<TextMeshProUGUI>().text = "Max Players:";
                                maxPlayersParentObj.transform.localPosition = new Vector3(0f, -85f, 0f);
                                GameObject.Destroy(maxPlayersParentObj.transform.Find("Arrow")?.gameObject);
                                GameObject.Destroy(maxPlayersParentObj.transform.Find("Arrow (1)")?.gameObject);

                                if (maxPlayersParentObj.transform.Find("MC_CrewCount") == null)
                                {
                                    GameObject maxPlayersObject = GameObject.Instantiate(serverTagObject.gameObject, maxPlayersParentObj.transform);
                                    maxPlayersObject.name = "MC_CrewCount";
                                    RectTransform maxPlayersRect = maxPlayersObject.GetComponent<RectTransform>();
                                    maxPlayersRect.sizeDelta = new Vector2(55f, 30f);
                                    maxPlayersObject.transform.localPosition = new Vector3(75f, 0f, 0f);
                                    maxPlayersObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().text = "4";
                                    maxPlayersObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().horizontalAlignment = HorizontalAlignmentOptions.Center;
                                    maxPlayersObject.transform.Find("Text Area/Text").GetComponent<TextMeshProUGUI>().horizontalAlignment = HorizontalAlignmentOptions.Center;
                                    maxPlayersObject.GetComponent<TMP_InputField>().characterValidation = TMP_InputField.CharacterValidation.Integer;
                                    maxPlayersObject.GetComponent<TMP_InputField>().characterLimit = 3;
                                    maxPlayersObject.GetComponent<TMP_InputField>().readOnly = !Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany");
                                }
                            }
                        }
                    }

                    hostSettingsContainer.Find("Confirm").localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    hostSettingsContainer.Find("Confirm").localPosition = new Vector3(55f, -68f, 0f);
                    hostSettingsContainer.Find("Back").localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    hostSettingsContainer.Find("Back").localPosition = new Vector3(-68f, -68f, 0f);

                    __instance.privatePublicDescription.transform.localPosition = new Vector3(0f, 110f, 0f);
                    __instance.tipTextHostSettings.transform.localPosition = new Vector3(0f, -140f, 0f);

                    __instance.HostSettingsOptionsNormal.transform.Find("Public")?.gameObject?.SetActive(false);
                    __instance.HostSettingsOptionsNormal.transform.Find("Private")?.gameObject?.SetActive(false);


                    // Lobby List Panel
                    if (!GameNetworkManager.Instance.disableSteam)
                    {
                        GameObject lobbyFilterPopup = GameObject.Instantiate(liPanel.gameObject, GameObject.Find("Canvas/MenuContainer/LobbyList").transform);
                        lobbyFilterPopup.name = "LIPanel_List";
                        RectTransform lobbyFilterPopupRect = lobbyFilterPopup.GetComponent<RectTransform>();
                        lobbyFilterPopupRect.anchorMin = new Vector2(0.5f, 0.5f);
                        lobbyFilterPopupRect.anchorMax = new Vector2(0.5f, 0.5f);
                        lobbyFilterPopupRect.pivot = new Vector2(0.5f, 0.5f);
                        lobbyFilterPopup.transform.localPosition = new Vector3(210f, 0f, 0f);
                        for (int i = lobbyFilterPopup.transform.childCount - 1; i >= 0; i--)
                        {
                            Transform child = lobbyFilterPopup.transform.GetChild(i);
                            if (child.name != "EnterAName" && child.name != "Darken" && child.name != "Outline")
                            {
                                GameObject.Destroy(child.gameObject);
                            }
                        }

                        float currentHeight = 65f;
                        GameObject vanillaToggleObj = GameObject.Instantiate(liPanel.transform.Find("ServerSecureToggle").gameObject, lobbyFilterPopup.transform);
                        vanillaToggleObj.name = "VanillaLobbyToggle";
                        vanillaToggleObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                        currentHeight = currentHeight - 25f;
                        TextMeshProUGUI vanillaToggleText = vanillaToggleObj.GetComponentInChildren<TextMeshProUGUI>();
                        vanillaToggleText.text = "Show Vanilla:";
                        Image vanillaToggleIcon = vanillaToggleObj.transform.Find("Arrow (1)").GetComponentInChildren<Image>();
                        Button vanillaToggleBtn = vanillaToggleObj.GetComponentInChildren<Button>();
                        vanillaToggleBtn.onClick = new Button.ButtonClickedEvent();
                        vanillaToggleBtn.onClick.AddListener(() =>
                        {
                            PluginLoader.steamLobbyType_Vanilla.Value = !PluginLoader.steamLobbyType_Vanilla.Value;
                            vanillaToggleIcon.enabled = PluginLoader.steamLobbyType_Vanilla.Value;
                            PluginLoader.StaticConfig.Save();
                        });

                        GameObject ppToggleObj = GameObject.Instantiate(liPanel.transform.Find("ServerSecureToggle").gameObject, lobbyFilterPopup.transform);
                        ppToggleObj.name = "PPLobbyToggle";
                        ppToggleObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                        currentHeight = currentHeight - 25f;
                        TextMeshProUGUI ppToggleText = ppToggleObj.GetComponentInChildren<TextMeshProUGUI>();
                        ppToggleText.text = "Show Password Protected:";
                        Image ppToggleIcon = ppToggleObj.transform.Find("Arrow (1)").GetComponentInChildren<Image>();
                        Button ppToggleBtn = ppToggleObj.GetComponentInChildren<Button>();
                        ppToggleBtn.onClick = new Button.ButtonClickedEvent();
                        ppToggleBtn.onClick.AddListener(() =>
                        {
                            PluginLoader.steamLobbyType_Password.Value = !PluginLoader.steamLobbyType_Password.Value;
                            ppToggleIcon.enabled = PluginLoader.steamLobbyType_Password.Value;
                            PluginLoader.StaticConfig.Save();
                        });

                        if (Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
                        {
                            GameObject mcToggleObj = GameObject.Instantiate(liPanel.transform.Find("ServerSecureToggle").gameObject, lobbyFilterPopup.transform);
                            mcToggleObj.name = "MCLobbyToggle";
                            mcToggleObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                            currentHeight = currentHeight - 25f;
                            TextMeshProUGUI mcToggleText = mcToggleObj.GetComponentInChildren<TextMeshProUGUI>();
                            mcToggleText.text = "Show More Company:";
                            Image mcToggleIcon = mcToggleObj.transform.Find("Arrow (1)").GetComponentInChildren<Image>();
                            Button mcToggleBtn = mcToggleObj.GetComponentInChildren<Button>();
                            mcToggleBtn.onClick = new Button.ButtonClickedEvent();
                            mcToggleBtn.onClick.AddListener(() =>
                            {
                                PluginLoader.steamLobbyType_MoreCompany.Value = !PluginLoader.steamLobbyType_MoreCompany.Value;
                                mcToggleIcon.enabled = PluginLoader.steamLobbyType_MoreCompany.Value;
                                PluginLoader.StaticConfig.Save();
                            });
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MenuManager), "ClickHostButton")]
        [HarmonyPostfix]
        private static void MenuManager_ClickHostButton(MenuManager __instance)
        {
            if (GameNetworkManager.Instance.disableSteam && __instance.HostSettingsOptionsLAN.activeSelf)
            {
                __instance.HostSettingsOptionsLAN.SetActive(value: false);
                __instance.HostSettingsOptionsNormal.SetActive(value: true);
                Object.FindFirstObjectByType<SaveFileUISlot>()?.SetButtonColorForAllFileSlots();
                __instance.HostSetLobbyPublic(__instance.hostSettings_LobbyPublic);
            }

            Image secureToggleIcon = __instance.HostSettingsScreen.transform.Find("HostSettingsContainer/LobbyHostOptions/OptionsNormal/ServerSecureToggle/Arrow (1)")?.GetComponentInChildren<Image>();
            if (secureToggleIcon != null)
            {
                if (GameNetworkManager.Instance.disableSteam)
                    secureToggleIcon.enabled = PluginLoader.lanSecureLobby.Value;
                else
                    secureToggleIcon.enabled = PluginLoader.steamSecureLobby.Value;
            }

            TMP_Dropdown accessDropdown = __instance.HostSettingsScreen.transform.Find("HostSettingsContainer/LobbyHostOptions/OptionsNormal/ServerAccessDropdown")?.GetComponent<TMP_Dropdown>();
            if (accessDropdown != null)
            {
                if (GameNetworkManager.Instance.disableSteam)
                    accessDropdown.value = PluginLoader.setInviteOnly ? 1 : __instance.hostSettings_LobbyPublic ? 0 : 2;
                else
                    accessDropdown.value = PluginLoader.setInviteOnly ? 2 : __instance.hostSettings_LobbyPublic ? 0 : 1;
            }
        }

        [HarmonyPatch(typeof(MenuManager), "ConfirmHostButton")]
        [HarmonyPrefix]
        private static bool MM_ConfirmHostButton(MenuManager __instance)
        {
            if (__instance.hostSettings_LobbyPublic && !PluginLoader.setInviteOnly && LobbyNameFilter.offensiveWords.Any(x => __instance.lobbyNameInputField.text.ToLower().Contains(x)))
            {
                string blockMessage = "This lobby name is blocked in vanilla. If you wish to use it anyway click confirm again.";
                if (__instance.tipTextHostSettings.text != blockMessage)
                {
                    __instance.tipTextHostSettings.text = blockMessage;
                    return false;
                }
            }

            PluginLoader.SetLobbyPassword(__instance.HostSettingsOptionsNormal.transform.Find("ServerPasswordField")?.gameObject?.GetComponent<TMP_InputField>()?.text);
            return true;
        }

        internal static ulong protectedLobbyId = 0;
        internal static string protectedLobbyPassword = null;
        [HarmonyPatch(typeof(MenuManager), "DisplayMenuNotification")]
        [HarmonyPostfix]
        private static void MM_DisplayMenuNotification(MenuManager __instance)
        {
            protectedLobbyId = 0;
            protectedLobbyPassword = null;

            if (__instance.menuNotificationText.text.EndsWith("You have entered an incorrect password."))
            {
                if (!__instance.menuNotification.transform.Find("Panel/ServerPasswordField"))
                {
                    GameObject tmpField = GameObject.Instantiate(__instance.HostSettingsOptionsNormal.transform.Find("ServerPasswordField")?.gameObject, __instance.menuNotification.transform.Find("Panel"));
                    tmpField.name = "ServerPasswordField";
                    tmpField.transform.localPosition = new Vector3(0f, 5f, 0f);
                }

                if (!__instance.menuNotification.transform.Find("Panel/JoinButton"))
                {
                    GameObject backButtonObj = __instance.menuNotification.transform.Find("Panel/ResponseButton")?.gameObject;
                    backButtonObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    backButtonObj.transform.localPosition = new Vector3(-65f, -65f, 0f);

                    GameObject joinButtonObj = GameObject.Instantiate(backButtonObj.gameObject, backButtonObj.transform.parent);
                    joinButtonObj.name = "JoinButton";
                    joinButtonObj.transform.localPosition = new Vector3(65f, -65f, 0f);
                    joinButtonObj.GetComponentInChildren<TextMeshProUGUI>().text = "[ Join ]";
                    Button joinButton = joinButtonObj.GetComponent<Button>();
                    joinButton.onClick.AddListener(() => {
                        protectedLobbyPassword = __instance.menuNotification.transform.Find("Panel/ServerPasswordField")?.GetComponent<TMP_InputField>()?.text;
                        if (GameNetworkManager.Instance.disableSteam)
                        {
                            __instance.StartAClient();
                        }
                        else
                        {
                            SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
                            __instance.StartCoroutine(LobbyCodes_Steam.JoinLobbyByID(lobbyManager, protectedLobbyId));
                        }
                    });
                }

                if (!GameNetworkManager.Instance.disableSteam && GameNetworkManager.Instance.currentLobby.HasValue)
                    protectedLobbyId = GameNetworkManager.Instance.currentLobby.Value.Id;

                bool showPasswordInput = protectedLobbyId != 0 || GameNetworkManager.Instance.disableSteam;
                __instance.menuNotification.transform.Find("Panel/ServerPasswordField")?.gameObject?.SetActive(showPasswordInput);
                __instance.menuNotification.transform.Find("Panel/JoinButton")?.gameObject?.SetActive(showPasswordInput);
            }
            else
            {
                __instance.menuNotification.transform.Find("Panel/ServerPasswordField")?.gameObject?.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SteamMatchmaking_OnLobbyCreated")]
        [HarmonyPostfix]
        private static void SteamMatchmaking_OnLobbyCreated(ref GameNetworkManager __instance, ref Steamworks.Result result, ref Lobby lobby)
        {
            if (PluginLoader.setInviteOnly)
            {
                __instance.lobbyHostSettings.isLobbyPublic = false;
                lobby.SetPrivate();
            }

            if (!string.IsNullOrWhiteSpace(PluginLoader.lobbyPassword))
                lobby.SetData("password", "1");
            if (PluginLoader.steamSecureLobby.Value)
                lobby.SetData("li_secure", "1");
        }

        [HarmonyPatch(typeof(QuickMenuManager), "NonHostPlayerSlotsEnabled")]
        [HarmonyPostfix]
        private static void NonHostPlayerSlotsEnabled(ref bool __result)
        {
            __result = true;
        }
    }
}
