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
    /*[HarmonyPatch(typeof (SelectableCardArray), "WaitForClearCardPile", new System.Type[] {typeof(CardPile)})]
    public class SelectableCardArray_WaitForClearCardPile
    {
        public static bool Prefix(SelectableCardArray __instance, CardPile pile, ref IEnumerator __result)
        {
            __result = WaitForClearCardPile(pile);
            return false;
        }

        private static IEnumerator WaitForClearCardPile(CardPile pile)
        {
            if (pile != null)
            {
                yield return new WaitUntil(() => !pile.DoingCardOperation);
                yield return pile.DestroyCards(0.5f);
            }

            yield return null;
        }
    }*/
}