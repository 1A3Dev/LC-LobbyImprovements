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
    public class InviteOnly
    {
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void MenuManager_Start(MenuManager __instance)
        {
            if (!GameNetworkManager.Instance.disableSteam)
            {
                float height = 14.5f;
                GameObject publicButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/HostSettingsContainer/LobbyHostOptions/OptionsNormal/Public") ?? GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/Panel/LobbyHostOptions/OptionsNormal/Public");
                if (publicButtonObject != null)
                {
                    height = publicButtonObject.GetComponent<RectTransform>().localPosition.y;

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

                GameObject friendsButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/HostSettingsContainer/LobbyHostOptions/OptionsNormal/Private") ?? GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/Panel/LobbyHostOptions/OptionsNormal/Private");
                if (friendsButtonObject != null)
                {
                    friendsButtonObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.9f, 1f);
                    friendsButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(40f, height, 30f);
                    Button friendsButton = friendsButtonObject.GetComponent<Button>();
                    friendsButton.onClick = new ButtonClickedEvent();
                    friendsButton.onClick.AddListener(() =>
                    {
                        PluginLoader.setInviteOnly = false;
                        __instance.HostSetLobbyPublic();
                    });

                    GameObject inviteOnlyButtonObject = Object.Instantiate(friendsButtonObject.gameObject, friendsButtonObject.transform.parent);
                    inviteOnlyButtonObject.name = "InviteOnly";
                    inviteOnlyButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(127f, height, 30f);
                    inviteOnlyButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = "Invite-only";
                    PluginLoader.setInviteOnlyButtonAnimator = inviteOnlyButtonObject.GetComponent<Animator>();
                    Button inviteOnlyButton = inviteOnlyButtonObject.GetComponent<Button>();
                    inviteOnlyButton.onClick = new ButtonClickedEvent();
                    inviteOnlyButton.onClick.AddListener(() =>
                    {
                        PluginLoader.setInviteOnly = true;
                        __instance.HostSetLobbyPublic();
                    });
                }
            }
        }

        [HarmonyPatch(typeof(MenuManager), "HostSetLobbyPublic")]
        [HarmonyPostfix]
        private static void MenuManager_HostSetLobbyPublic(ref MenuManager __instance, bool setPublic = false)
        {
            if (!GameNetworkManager.Instance.disableSteam)
            {
                __instance.setPrivateButtonAnimator.SetBool("isPressed", !setPublic && !PluginLoader.setInviteOnly);
                PluginLoader.setInviteOnlyButtonAnimator?.SetBool("isPressed", PluginLoader.setInviteOnly);
                if (!setPublic)
                {
                    if (PluginLoader.setInviteOnly)
                        __instance.privatePublicDescription.text = "INVITE ONLY means you must send invites through Steam for players to join.";
                    else
                        __instance.privatePublicDescription.text = "FRIENDS ONLY means only friends or invited people can join.";
                }
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
                lobby.SetData("inviteOnly", "true");
                lobby.SetData("lobbyType", "Private");
            }
            else
            {
                lobby.SetData("lobbyType", __instance.lobbyHostSettings.isLobbyPublic ? "Public" : "Friends Only");
            }
        }
    }
}
