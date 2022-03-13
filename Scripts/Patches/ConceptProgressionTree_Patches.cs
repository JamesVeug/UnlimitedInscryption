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
    [HarmonyPatch(typeof(ConceptProgressionTree), "CardUnlocked", new System.Type[] {typeof(CardInfo), typeof(bool)})]
    public class ConceptProgressionTree_MergeSequence
    {
        public static bool Prefix(ConceptProgressionTree __instance, ref IEnumerator __result)
        {
            // Choice node always gives all cards
            return true;
        }
    }
}