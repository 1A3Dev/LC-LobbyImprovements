using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LobbyImprovements.LANDiscovery;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Button;

namespace LobbyImprovements
{
    [BepInPlugin(modGUID, "LobbyImprovements", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private const string modGUID = "Dev1A3.LobbyImprovements";

        private readonly Harmony harmony = new Harmony(modGUID);

        private const string modVersion = "1.0.0";

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }

        public static int MaxPlayers = 4;

        public static ConfigEntry<bool> lobbyFilterEnabled;
        public static ConfigEntry<string> lobbyFilteredNames;
        public static string[] BlockedTermsRaw;

        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            StaticLogger = Logger;
            StaticConfig = Config;

            lobbyFilterEnabled = StaticConfig.Bind("Lobby Names", "Filter Enabled", true, "Should the lobby name filter be enabled?");
            lobbyFilterEnabled.SettingChanged += (sender, args) =>
            {
                SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
                if (lobbyManager != null)
                {
                    lobbyManager.censorOffensiveLobbyNames = lobbyFilterEnabled.Value && BlockedTermsRaw.Length > 0;
                }
            };
            lobbyFilteredNames = StaticConfig.Bind("Lobby Names", "Filter Terms", "nigger,faggot,n1g,nigers,cunt,pussies,pussy,minors,children,kids,chink,buttrape,molest,rape,coon,negro,beastiality,cocks,cumshot,ejaculate,pedophile,furfag,necrophilia,yiff,sex,porn", "This should be a comma-separated list. Leaving this blank will also disable the filter.");
            BlockedTermsRaw = lobbyFilteredNames.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            lobbyFilteredNames.SettingChanged += (sender, args) =>
            {
                BlockedTermsRaw = lobbyFilteredNames.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

                SteamLobbyManager lobbyManager = Object.FindFirstObjectByType<SteamLobbyManager>();
                if (lobbyManager != null)
                {
                    lobbyManager.censorOffensiveLobbyNames = lobbyFilterEnabled.Value && BlockedTermsRaw.Length > 0;
                }
            };

            AcceptableValueRange<int> acceptablePortRange = new AcceptableValueRange<int>(1, 65535);
            lanDefaultPort = StaticConfig.Bind("LAN", "Default Port", 7777, new ConfigDescription("The port used for hosting a lobby", acceptablePortRange));
            lanDiscoveryPort = StaticConfig.Bind("LAN", "Discovery Port", 47777, new ConfigDescription("The port used for lobby discovery", acceptablePortRange));

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);
        }

        public static bool setInviteOnly = false;
        public static Animator setInviteOnlyButtonAnimator;
    }

    [HarmonyPatch]
    internal class General_Patches
    {
        [HarmonyPatch(typeof(SteamLobbyManager), "RefreshServerListButton")]
        [HarmonyPrefix]
        private static bool RefreshServerListButton(SteamLobbyManager __instance)
        {
            if (__instance.serverTagInputField.text != string.Empty)
            {
                if (GameNetworkManager.Instance.disableSteam)
                {
                    if (LobbyCodes_LAN.TryParseIpAndPort(__instance.serverTagInputField.text, out IPAddress ipAddress, out int port))
                    {
                        LobbyCodes_LAN.JoinLobbyByIP(ipAddress.ToString(), (ushort)port);
                    }
                }
                else if (ulong.TryParse(__instance.serverTagInputField.text, out ulong lobbyId) && lobbyId.ToString().Length >= 15 && lobbyId.ToString().Length <= 20)
                {
                    __instance.StartCoroutine(LobbyCodes_Steam.JoinLobbyByID(__instance, lobbyId));
                    return false;
                }
            }

            return true;
        }
    }
}