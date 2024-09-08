using HarmonyLib;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class SortByPlayerCount
    {
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void MenuManager_Start()
        {
            GameObject refreshButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyList/ListPanel/RefreshButton");
            if (refreshButtonObject != null && !GameObject.Find("/Canvas/MenuContainer/LobbyList/ListPanel/SortPlayerCountButton"))
            {
                GameObject sortButtonObject = Object.Instantiate(refreshButtonObject.gameObject, refreshButtonObject.transform.parent);
                sortButtonObject.name = "SortPlayerCountButton";
                sortButtonObject.GetComponent<RectTransform>().anchoredPosition += new Vector2(0f, 18f);
                sortButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = "[ Sort ]";
                Button sortButton = sortButtonObject.GetComponent<Button>();
                sortButton.onClick = new Button.ButtonClickedEvent();
                sortButton.onClick.AddListener(() => {
                    LobbySlot[] lobbySlots = Object.FindObjectsByType<LobbySlot>(FindObjectsSortMode.InstanceID);
                    lobbySlots = lobbySlots.OrderByDescending(lobby => lobby.thisLobby.MemberCount).ToArray();
                    float lobbySlotPositionOffset = 0f;
                    for (int i = 0; i < lobbySlots.Length; i++)
                    {
                        lobbySlots[i].gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, lobbySlotPositionOffset);
                        lobbySlotPositionOffset -= 42f;
                    }
                });
            }
        }
    }
}
