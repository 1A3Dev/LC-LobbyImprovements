using System;
using System.Collections.Generic;
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

            PluginHelper.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, pluginVersion, CompatibilityLevel.Variable, VersionStrictness.None, variableCompatibilityCheck);

            try
            {
                harmony.PatchAll(typeof(LobbyCompatibility_Compat));
            }
            catch (Exception ex)
            {
                PluginLoader.StaticLogger.LogWarning($"LobbyCompatibility Patch Failed: {ex}");
            }
        }

        private static string GetData(IEnumerable<KeyValuePair<string, string>> kvpData, string keyName)
        {
            return kvpData.FirstOrDefault(x => x.Key.ToLower() == keyName).Value;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static CompatibilityLevel variableCompatibilityCheck(IEnumerable<KeyValuePair<string, string>> lobbyData)
        {
            if (GetData(lobbyData, "password") == "1" || GetData(lobbyData, "li_secure") == "1")
            {
                return CompatibilityLevel.Everyone;
            }
            
            return CompatibilityLevel.ClientOnly;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetLANLobbyModData(LANLobby lobby)
        {
            // Add plugin metadata to the lobby so clients can check if they have the required plugins
            var pluginInfo = PluginHelper.GetAllPluginInfo().CalculateCompatibilityLevel(null, new Dictionary<string, string> {
                { "maxplayers", lobby.MaxMembers.ToString() },
                { "password", lobby.IsPasswordProtected.ToString() },
                { "li_secure", lobby.IsSecure.ToString() },
            });
            
            var pluginsString = LobbyHelper.GetLobbyPluginsMetadata(pluginInfo).ToArray();
            lobby.ModPlugins = pluginsString.Join(delimiter: string.Empty);

            lobby.ModRequiredChecksum = PluginHelper.Checksum;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddModdedLobbySlotToLobby(LANLobbySlot lobbySlot)
        {
            ModdedLobbySlot moddedLobbySlot = lobbySlot.gameObject.AddComponent<ModdedLobbySlot>();
            moddedLobbySlot._lobbyDiff = LobbyHelper.GetLobbyDiff(null, lobbySlot.thisLobby.ModPlugins, new Dictionary<string, string> {
                { "maxplayers", lobbySlot.thisLobby.MaxMembers.ToString() },
                { "password", lobbySlot.thisLobby.IsPasswordProtected.ToString() },
                { "li_secure", lobbySlot.thisLobby.IsSecure.ToString() },
            });
            moddedLobbySlot.Setup(lobbySlot);
        }
        
        [HarmonyPatch(typeof(LobbyHelper), "GetLobbyDiff", new Type[] { typeof(Lobby), typeof(string), typeof(IEnumerable<KeyValuePair<string, string>>) })]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void GetLobbyDiff(ref string lobbyPluginString, ref IEnumerable<KeyValuePair<string, string>> lobbyData)
        {
            if (GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam && LANLobbyManager_InGame.currentLobby != null)
            {
                if (lobbyPluginString.IsNullOrWhiteSpace())
                {
                    lobbyPluginString = LANLobbyManager_InGame.currentLobby.ModPlugins;
                    lobbyData = new Dictionary<string, string>
                    {
                        { "maxplayers", LANLobbyManager_InGame.currentLobby.MaxMembers.ToString() },
                        { "password", LANLobbyManager_InGame.currentLobby.IsPasswordProtected.ToString() },
                        { "li_secure", LANLobbyManager_InGame.currentLobby.IsSecure.ToString() },
                    };
                }
            }
        }
    }
}
