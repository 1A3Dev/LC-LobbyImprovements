using HarmonyLib;
using static UnityEngine.UI.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Steamworks.Data;

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
                if (GameNetworkManager.Instance.disableSteam)
                {
                    __instance.lanButtonContainer?.SetActive(false);
                    __instance.joinCrewButtonContainer?.SetActive(true);
                }

                Transform lobbyHostOptions = __instance.HostSettingsScreen.transform.Find("HostSettingsContainer/LobbyHostOptions");
                if (lobbyHostOptions != null && PluginLoader.setInviteOnlyButtonAnimator == null)
                {
                    bool isLAN = GameNetworkManager.Instance.disableSteam;
                    float height = 14.5f;
                    Transform publicButtonObject = __instance.HostSettingsOptionsNormal.transform.Find("Public");
                    if (publicButtonObject != null)
                    {
                        height = publicButtonObject.GetComponent<RectTransform>().localPosition.y;

                        __instance.lanSetAllowRemoteButtonAnimator = publicButtonObject.GetComponent<Animator>();
                        publicButtonObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.9f, 1f);
                        publicButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(-127f, height, 30f);
                        Button publicButton = publicButtonObject.GetComponent<Button>();
                        publicButton.onClick = new ButtonClickedEvent();
                        publicButton.onClick.AddListener(() =>
                        {
                            PluginLoader.setInviteOnly = false;
                            __instance.HostSetLobbyPublic(true);
                        });
                    }

                    Transform friendsButtonObject = __instance.HostSettingsOptionsNormal.transform.Find("Private");
                    if (friendsButtonObject != null)
                    {
                        __instance.lanSetLocalButtonAnimator = friendsButtonObject.GetComponent<Animator>();
                        friendsButtonObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.9f, 1f);
                        friendsButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(!isLAN ? 40f : 127f, height, 30f);
                        if (isLAN)
                            friendsButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = "Localhost";
                        Button friendsButton = friendsButtonObject.GetComponent<Button>();
                        friendsButton.onClick = new ButtonClickedEvent();
                        friendsButton.onClick.AddListener(() =>
                        {
                            PluginLoader.setInviteOnly = false;
                            __instance.HostSetLobbyPublic();
                        });

                        GameObject inviteOnlyButtonObject = Object.Instantiate(friendsButtonObject.gameObject, friendsButtonObject.transform.parent);
                        inviteOnlyButtonObject.name = "InviteOnly";
                        PluginLoader.setInviteOnlyButtonAnimator = inviteOnlyButtonObject.GetComponent<Animator>();
                        inviteOnlyButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(isLAN ? 40f : 127f, height, 30f);
                        inviteOnlyButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = isLAN ? "IP-only" : "Invite-only";
                        Button inviteOnlyButton = inviteOnlyButtonObject.GetComponent<Button>();
                        inviteOnlyButton.onClick = new ButtonClickedEvent();
                        inviteOnlyButton.onClick.AddListener(() =>
                        {
                            PluginLoader.setInviteOnly = true;
                            __instance.HostSetLobbyPublic(isLAN);
                        });
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
