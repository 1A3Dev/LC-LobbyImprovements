using TMPro;

namespace LobbyImprovements.LANDiscovery
{
    public class LANLobbySlot : LobbySlot
    {
        public TextMeshProUGUI HostName;

        public new LANLobby thisLobby;

        public new void JoinButton()
        {
            LobbyCodes_LAN.JoinLobbyByIP(thisLobby.IPAddress, thisLobby.Port);
        }
    }
}
