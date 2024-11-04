
namespace LobbyImprovements.Compatibility
{
    internal class MoreCompany_Compat
    {
        internal static int GetMaxPlayers()
        {
            return MoreCompany.MainClass.newPlayerCount;
        }
    }
}