using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using LobbyCompatibility.Behaviours;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;
using LobbyImprovements.LANDiscovery;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Steamworks.Data;

namespace LobbyImprovements.Compatibility
{
    internal class LobbyCompatibility_Compat
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Init(Harmony harmony)
        {
            PluginLoader.StaticLogger.LogWarning("LobbyCompatibility detected, registering plugin with LobbyCompatibility.");

            Version pluginVersion = Version.Parse(MyPluginInfo.PLUGIN_VERSION);

            PluginHelper.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, pluginVersion, CompatibilityLevel.ClientOnly, VersionStrictness.None);

            try
            {
                harmony.PatchAll(typeof(LobbyCompatibility_Compat));
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogWarning($"LobbyCompatibility Patch Failed: {ex}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetLANLobbyModData(LANLobby lobby)
        {
            // Add plugin metadata to the lobby so clients can check if they have the required plugins
            lobby.Mods = JsonConvert.SerializeObject(PluginHelper.GetAllPluginInfo().ToList(), new VersionConverter());
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddModdedLobbySlotToLobby(LANLobbySlot lobbySlot)
        {
            ModdedLobbySlot moddedLobbySlot = lobbySlot.gameObject.AddComponent<ModdedLobbySlot>();
            moddedLobbySlot._lobbyDiff = LobbyHelper.GetLobbyDiff(null, lobbySlot.thisLobby.Mods);
            moddedLobbySlot.Setup(lobbySlot);
        }
        
        [HarmonyPatch(typeof(LobbyHelper), "GetLobbyDiff", new Type[] { typeof(Lobby), typeof(string) })]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void GetLobbyDiff(ref string lobbyPluginString)
        {
            if (GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam && lobbyPluginString.IsNullOrWhiteSpace() && LANLobbyManager_InGame.currentLobby != null)
            {
                lobbyPluginString = LANLobbyManager_InGame.currentLobby.Mods;
            }
        }
    }
}
