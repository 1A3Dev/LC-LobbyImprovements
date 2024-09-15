using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;
using Lobby = Steamworks.Data.Lobby;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class LobbyInviteOnly
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
                    RectTransform rectTransform = hostSettingsContainer.GetComponent<RectTransform>();
                    rectTransform.sizeDelta = new Vector2(256.65f, 250f); // Increase height

                    // Field Labels
                    GameObject serverNameLabel = lobbyHostOptions.Find("OptionsNormal/EnterAName")?.gameObject;
                    if (serverNameLabel != null)
                    {
                        serverNameLabel.transform.localPosition = new Vector3(0f, 120f, 0f);
                        serverNameLabel.GetComponent<TextMeshProUGUI>().text = "Server name:";

                        GameObject serverPasswordLabel = GameObject.Instantiate(serverNameLabel.gameObject, serverNameLabel.transform.parent);
                        serverPasswordLabel.name = "EnterAPassword";
                        serverPasswordLabel.transform.localPosition = new Vector3(0f, 78f, 0f);
                        serverPasswordLabel.GetComponent<TextMeshProUGUI>().text = "Server password:";

                        GameObject serverAccessLabel = GameObject.Instantiate(serverNameLabel.gameObject, serverNameLabel.transform.parent);
                        serverAccessLabel.name = "EnterAccess";
                        serverAccessLabel.transform.localPosition = new Vector3(0f, 36f, 0f);
                        serverAccessLabel.GetComponent<TextMeshProUGUI>().text = "Server access:";

                        GameObject serverTagLabel = GameObject.Instantiate(serverNameLabel.gameObject, serverNameLabel.transform.parent);
                        serverTagLabel.name = "EnterATag";
                        serverTagLabel.transform.localPosition = new Vector3(0f, -5f, 0f);
                        serverTagLabel.GetComponent<TextMeshProUGUI>().text = "Server tag:";
                    }

                    // Text Fields
                    GameObject serverTagObject = lobbyHostOptions.Find("OptionsNormal/ServerTagInputField")?.gameObject;
                    if (serverTagObject != null)
                    {
                        serverTagObject.transform.localPosition = new Vector3(0f, -28f, 0f);
                        serverTagObject.GetComponent<RectTransform>().sizeDelta = new Vector2(310.55f, 30f);
                        __instance.lobbyTagInputField = serverTagObject.GetComponent<TMP_InputField>();

                        if (lobbyHostOptions.Find("OptionsNormal/ServerNameField") != null)
                        {
                            GameObject.Destroy(lobbyHostOptions.Find("OptionsNormal/ServerNameField")?.gameObject);

                            GameObject serverNameObject = GameObject.Instantiate(serverTagObject.gameObject, serverTagObject.transform.parent);
                            serverNameObject.name = "ServerNameField";
                            serverNameObject.transform.localPosition = new Vector3(0f, 98f, 0f);
                            serverNameObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().text = "Enter a name...";
                            __instance.lobbyNameInputField = serverNameObject.GetComponent<TMP_InputField>();

                            GameObject serverPasswordObject = GameObject.Instantiate(serverTagObject.gameObject, serverTagObject.transform.parent);
                            serverPasswordObject.name = "ServerPasswordField";
                            serverPasswordObject.transform.localPosition = new Vector3(0f, 55f, 0f);
                            serverPasswordObject.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().text = "Enter a password...";
                            serverPasswordObject.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;
                            serverPasswordObject.GetComponent<TMP_InputField>().text = PluginLoader.lobbyPassword;
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
                            privacyDropdownObj.transform.localScale = new Vector3(0.7172f, 0.7172f, 0.7172f);
                            privacyDropdownObj.transform.localPosition = new Vector3(111f, 14f, 0f);
                            privacyDropdownObj.GetComponent<RectTransform>().sizeDelta = new Vector2(310.55f, 30f);

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

                    hostSettingsContainer.Find("Confirm").localPosition = new Vector3(0f, -80f, 0f);
                    hostSettingsContainer.Find("Back").localPosition = new Vector3(0f, -105f, 0f);

                    __instance.HostSettingsOptionsNormal.transform.Find("Public")?.gameObject?.SetActive(false);
                    __instance.HostSettingsOptionsNormal.transform.Find("Private")?.gameObject?.SetActive(false);
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
        }

        [HarmonyPatch(typeof(MenuManager), "ConfirmHostButton")]
        [HarmonyPrefix]
        private static void MM_ConfirmHostButton(MenuManager __instance)
        {
            PluginLoader.SetLobbyPassword(__instance.HostSettingsOptionsNormal.transform.Find("ServerPasswordField")?.gameObject?.GetComponent<TMP_InputField>()?.text);
        }

        internal static ulong protectedLobbyId = 0;
        [HarmonyPatch(typeof(MenuManager), "DisplayMenuNotification")]
        [HarmonyPostfix]
        private static void MM_DisplayMenuNotification(MenuManager __instance)
        {
            protectedLobbyId = 0;

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
                        PluginLoader.lobbyPassword = __instance.menuNotification.transform.Find("Panel/ServerPasswordField")?.GetComponent<TMP_InputField>()?.text;
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
                lobby.SetData("password", "t");
            if (PluginLoader.steamSecureLobby.Value)
                lobby.SetData("secure", "t");
        }

        [HarmonyPatch(typeof(QuickMenuManager), "NonHostPlayerSlotsEnabled")]
        [HarmonyPostfix]
        private static void NonHostPlayerSlotsEnabled(ref bool __result)
        {
            __result = true;
        }
    }
}
