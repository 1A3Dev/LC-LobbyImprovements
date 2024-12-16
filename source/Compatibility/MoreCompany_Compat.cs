using System.Runtime.CompilerServices;

namespace LobbyImprovements.Compatibility
{
    internal class MoreCompany_Compat
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int GetMaxPlayers()
        {
            return MoreCompany.MainClass.newPlayerCount;
        }
    }
}