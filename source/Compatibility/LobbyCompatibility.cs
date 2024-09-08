using System;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;

namespace LobbyImprovements.Compatibility
{
    internal class LobbyCompatibility
    {
        public static void Init()
        {
            PluginLoader.StaticLogger.LogWarning("LobbyCompatibility detected, registering plugin with LobbyCompatibility.");

            Version pluginVersion = Version.Parse(MyPluginInfo.PLUGIN_VERSION);

            PluginHelper.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, pluginVersion, CompatibilityLevel.ClientOnly, VersionStrictness.None);
        }
    }
}
