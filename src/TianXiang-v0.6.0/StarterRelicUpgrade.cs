using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
namespace TianXiang;

// 0.107.1 uses a closed starter mapping and otherwise returns Circlet.
// Scope the replacement to our starter only; preserve every vanilla mapping.
[HarmonyPatch(typeof(TouchOfOrobas),nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
public static class StarterRelicUpgrade
{
    static void Postfix(RelicModel starterRelic,ref RelicModel __result)
    {
        if(starterRelic is WeatherCompass)__result=ModelDb.Relic<CelestialCompass>();
    }
}
