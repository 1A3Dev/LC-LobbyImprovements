using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class LobbyNameFilter
    {
        [HarmonyPatch(typeof(SteamLobbyManager), "OnEnable")]
        [HarmonyPrefix]
        private static void Prefix(ref SteamLobbyManager __instance)
        {
            __instance.censorOffensiveLobbyNames = PluginLoader.lobbyFilterEnabled.Value && PluginLoader.BlockedTermsRaw.Length > 0;
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileMoveNext(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool shouldSkip = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    // check for IL_0022: ldc.i4.s
                    if (!shouldSkip && instruction.opcode == OpCodes.Ldc_I4_S)
                    {
                        newInstructions.Add(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(PluginLoader), "BlockedTermsRaw")));
                        shouldSkip = true;
                    }
                    else if (shouldSkip && instruction.opcode == OpCodes.Stfld)
                    {
                        shouldSkip = false;
                        alreadyReplaced = true;
                    }

                    if (shouldSkip) continue;
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.StaticLogger.LogWarning($"LobbyNameFilter failed to replace offensiveWords");

            return newInstructions.AsEnumerable();
        }
    }
}
