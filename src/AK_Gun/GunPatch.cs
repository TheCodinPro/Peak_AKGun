using HarmonyLib;
using UnityEngine;

namespace AK_Gun;

[HarmonyPatch(typeof(Character))]
internal class GunPatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AddGunCharacterLaunch(Character __instance)
    {
        ((Component)__instance).gameObject.AddComponent<GunCharacterLaunch>();
    }
}
