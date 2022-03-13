using System;
using System.Collections;
using System.Collections.Generic;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using UnlimitedInscryption.Scripts.Sigils;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
    [HarmonyPatch(typeof(PlayableCard), "CanBeSacrificed", MethodType.Getter)]
    public class PlayableCard_CanBeSacrificed
    {
        public static void Postfix(ref PlayableCard __instance, ref bool __result)
        {
            if (__result)
            {
                if (__instance.HasAbility(DeadAbility.ability))
                {
                    __result = false;
                }
            }
        }
    }
}