using HarmonyLib;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class SortByPlayerCount
    {
        public static bool waitingForSort = false;

        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void MenuManager_Start(MenuManager __instance)
        {
            GameObject refreshButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyList/ListPanel/RefreshButton");
            if (!__instance.isInitScene && refreshButtonObject != null && !GameObject.Find("/Canvas/MenuContainer/LobbyList/ListPanel/SortPlayerCountButton"))
            {
                GameObject sortButtonObject = Object.Instantiate(refreshButtonObject.gameObject, refreshButtonObject.transform.parent);
                sortButtonObject.name = "SortPlayerCountButton";
                sortButtonObject.GetComponent<RectTransform>().anchoredPosition += new Vector2(0f, 18f);
                sortButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = "[ Sort ]";
                Button sortButton = sortButtonObject.GetComponent<Button>();
                sortButton.onClick = new Button.ButtonClickedEvent();
                sortButton.onClick.AddListener(() =>
                {
                    if (waitingForSort) return;
                    waitingForSort = true;
                    __instance.StartCoroutine(SortLobbies());
                });
            }
        }

        public static IEnumerator SortLobbies()
        {
            yield return new WaitUntil(() => !GameNetworkManager.Instance.waitingForLobbyDataRefresh);

            LobbySlot[] lobbySlots = Object.FindObjectsByType<LobbySlot>(FindObjectsSortMode.InstanceID);
            Array.Sort(lobbySlots, (x, y) => {
                if (x.thisLobby.MemberCount == y.thisLobby.MemberCount)
                {
                    if (x.thisLobby.MaxMembers == y.thisLobby.MaxMembers)
                    {
                        return x.thisLobby.GetData("name").CompareTo(y.thisLobby.GetData("name"));
                    }
                    else
                    {
                        return y.thisLobby.MaxMembers - x.thisLobby.MaxMembers;
                    }
                }
                else
                {
                    return y.thisLobby.MemberCount - x.thisLobby.MemberCount;
                }
            });
            float lobbySlotPositionOffset = 0f;
            for (int i = 0; i < lobbySlots.Length; i++)
            {
                lobbySlots[i].gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, lobbySlotPositionOffset);
                lobbySlotPositionOffset -= 42f;
            }

            waitingForSort = false;
        }
    }
}
