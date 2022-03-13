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
    [HarmonyPatch(typeof (CardMergeSequencer), "MergeSequence", new System.Type[] {typeof(CardMergeNodeData)})]
    public class CardMergeSequencer_MergeSequence
    {
        public static bool Prefix(CardMergeNodeData nodeData, CardMergeSequencer __instance, ref IEnumerator __result)
        {
	        if (!Plugin.Instance.CardMergeOverrideEnabled)
	        {
		        return true;
	        }

            Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
            if (confirmStoneButton == null)
            {
                confirmStoneButton = __instance.transform.Find("ConfirmStoneButton");
                GameObject clone = Object.Instantiate(confirmStoneButton.gameObject, confirmStoneButton.parent);
                clone.name = "CustomCancelButton";
                clone.transform.GetChild(0).gameObject.SetActive(true);
                clone.transform.localPosition = new Vector3(3, 5, -1.3f);

                // Assign new icon
                Transform quad = clone.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0);
                MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
                Material[] materials = quadRenderer.materials;
                materials[0].mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");
                quadRenderer.materials = materials;

                confirmStoneButton = clone.transform;
            }

            ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
            __result = Sequence(nodeData, __instance, cancelButton);
            return false;
        }

        private static IEnumerator Sequence(CardMergeNodeData nodeData, CardMergeSequencer __instance, ConfirmStoneButton cancelButton)
        {
            // Show intro animation and text
            yield return Intro(__instance, cancelButton);
            
            // Check if we have enough cards 
            Plugin.Log.LogInfo("[Sequence] spawning cards " + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);
            yield return __instance.pile.SpawnCards(RunState.DeckList.Count, 0.5f);
            Plugin.Log.LogInfo("[Sequence] cards spawned " + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);
            
            List<CardInfo> validHosts = __instance.GetValidCardsForHost(null);
            List<CardInfo> validCardsForSacrifice = __instance.GetValidCardsForSacrifice(null);
            bool flag = validHosts.Count == 1 && validCardsForSacrifice.Count == 1 && validHosts[0] == validCardsForSacrifice[0];
            bool flag2 = validCardsForSacrifice.Exists((CardInfo s) => validHosts.Exists((CardInfo h) => __instance.SacrificeOffersNewAbility(h, s)));
            if (validHosts.Count == 0 || validCardsForSacrifice.Count == 0 || flag || !flag2)
            {
	            // Not enough cards
                yield return __instance.InvalidCardsSequence();
            }
            else
            {
	            // Let player sacrifice constantly
	            Coroutine coroutine = __instance.StartCoroutine(SacrificeCard(__instance, cancelButton));
	            
	            // Wait for them to press the cancel button though
	            yield return cancelButton.WaitUntilConfirmation();
	            
	            __instance.StopCoroutine(coroutine);
            }

            // Outro
            yield return Outro(nodeData, __instance, cancelButton);
        }

        private static IEnumerator Outro(CardMergeNodeData nodeData, CardMergeSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
	        yield return new WaitForSeconds(0.25f);
	        __instance.hostSlot.FlyOffCard();
	        __instance.sacrificeSlot.FlyOffCard();
	        
	        Plugin.Log.LogInfo("[Outro] destroying cards" + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);
	        yield return __instance.pile.DestroyCards(0);
	        Plugin.Log.LogInfo("[Outro] cards destroyed " + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);
	        __instance.stoneCircleAnim.SetTrigger("exit");
	        Transform cancelButtonAnim = cancelButton.transform.parent.parent;
	        cancelButtonAnim.GetComponent<Animator>().SetTrigger("exit");
	        
	        ParticleSystem.EmissionModule emission = __instance.dustParticles.emission;
	        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f, 0f);
	        yield return new WaitForSeconds(0.25f);
	        __instance.confirmStone.Exit();
	        cancelButton.Exit();
	        yield return new WaitForSeconds(0.75f);
	        __instance.stoneCircleAnim.gameObject.SetActive(false);
	        cancelButtonAnim.gameObject.SetActive(false);
	        __instance.confirmStone.SetStoneInactive();
	        cancelButton.SetStoneInactive();
	        __instance.sacrificeSlot.DestroyCard();
	        __instance.hostSlot.DestroyCard();
	        Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map, null);
        }
        
        private static IEnumerator Intro(CardMergeSequencer __instance, ConfirmStoneButton buttonParent)
        {
	        Plugin.Log.LogInfo("[Intro] starting");
            __instance.hostSlot.Disable();
            __instance.sacrificeSlot.Disable();
            Singleton<TableRuleBook>.Instance.SetOnBoard(true);
            ParticleSystem.EmissionModule dustEmission = __instance.dustParticles.emission;
            dustEmission.rateOverTime = new ParticleSystem.MinMaxCurve(10f, 10f);
            Singleton<ViewManager>.Instance.Controller.SwitchToControlMode(ViewController.ControlMode.CardMerging, false);
            Singleton<ViewManager>.Instance.Controller.LockState = ViewLockState.Locked;
            yield return new WaitForSeconds(0.3f);
            __instance.stoneCircleAnim.gameObject.SetActive(true);
            buttonParent.transform.parent.parent.gameObject.SetActive(true);
            buttonParent.Enter();
            yield return new WaitForSeconds(0.5f);
            if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardMerging))
            {
                yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("You stumbled into some strange stones in the mist.", -2.5f, 0.5f, Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null, true);
            }
            Plugin.Log.LogInfo("[Intro] done");
        }

        private static IEnumerator SacrificeCard(CardMergeSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        Plugin.Log.LogInfo("[SacrificeCard] starting");
	        __instance.sacrificeSlot.ClearDelegates();
	        SelectCardFromDeckSlot selectCardFromDeckSlot = __instance.sacrificeSlot;
	        selectCardFromDeckSlot.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardFromDeckSlot.CursorSelectStarted, new Action<MainInputInteractable>(__instance.OnSlotSelected));
	        
	        __instance.hostSlot.ClearDelegates();
	        SelectCardFromDeckSlot selectCardFromDeckSlot2 = __instance.hostSlot;
	        selectCardFromDeckSlot2.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardFromDeckSlot2.CursorSelectStarted, new Action<MainInputInteractable>(__instance.OnSlotSelected));
	        while (true)
	        {
		        Plugin.Log.LogInfo("[SacrificeCard] A");
				Singleton<ViewManager>.Instance.SwitchToView(View.CardMergeSlots, false, false);
				__instance.gamepadGrid.enabled = true;
				__instance.sacrificeSlot.RevealAndEnable();
				if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardMerging))
				{
					yield return new WaitForSeconds(0.5f);
					Singleton<TextDisplayer>.Instance.Clear();
					yield return new WaitForSeconds(0.1f);
					Singleton<TextDisplayer>.Instance.ShowMessage("You were compelled to choose a worthy [c:bR]sacrifice.[c:] One that will be lost forever...", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null);
					yield return new WaitUntil(() => __instance.sacrificeSlot.Card != null);
					yield return new WaitForSeconds(0.25f);
					Singleton<TextDisplayer>.Instance.Clear();
				}
				__instance.hostSlot.RevealAndEnable();
				if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardMerging))
				{
					yield return new WaitUntil(() => Singleton<ViewManager>.Instance.CurrentView == View.DeckSelection);
					Singleton<TextDisplayer>.Instance.Clear();
					Singleton<TextDisplayer>.Instance.ShowMessage("You looked upon your menagerie and selected a healthy [c:bR]host.[c:]", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null);
					yield return new WaitUntil(() => __instance.hostSlot.Card || __instance.sacrificeSlot.Card == null);
					Singleton<TextDisplayer>.Instance.Clear();
				}
				yield return __instance.confirmStone.WaitUntilConfirmation();
				__instance.gamepadGrid.enabled = false;
				__instance.hostSlot.Disable();
				__instance.sacrificeSlot.Disable();
				cancelButton.Disable();
				yield return new WaitForSeconds(0.25f);
				Singleton<ViewManager>.Instance.SwitchToView(View.CardMergeSlots, false, true);
				Singleton<RuleBookController>.Instance.SetShown(false, true);
				yield return new WaitForSeconds(1f);
				foreach (SpecialCardBehaviour specialCardBehaviour in __instance.hostSlot.Card.GetComponents<SpecialCardBehaviour>())
				{
					yield return specialCardBehaviour.OnSelectedForCardMergeHost();
				}
				CardInfo sacrificedInfo = __instance.sacrificeSlot.Card.Info;
				__instance.ModifyHostCard(__instance.hostSlot.Card.Info, sacrificedInfo);
				RunState.Run.playerDeck.RemoveCard(sacrificedInfo);
				__instance.sacrificeSlot.Card.Anim.SetSacrificeHoverMarkerShown(false);
				__instance.sacrificeSlot.Card.Anim.PlayDeathAnimation(false);
				AudioController.Instance.PlaySound3D("sacrifice_default", MixerGroup.TableObjectsSFX, __instance.transform.position, 1f, 0f, null, null, null, null, false);
				yield return new WaitForSeconds(0.5f);
				AudioController.Instance.PlaySound3D("card_blessing", MixerGroup.TableObjectsSFX, __instance.hostSlot.transform.position, 1f, 0f, new AudioParams.Pitch(0.6f), null, null, null, false);
				__instance.hostSlot.Card.Anim.PlayTransformAnimation();
				yield return new WaitForSeconds(0.15f);
				__instance.hostSlot.Card.SetInfo(__instance.hostSlot.Card.Info);
				__instance.hostSlot.Card.SetInteractionEnabled(false);
				__instance.transformParticles.gameObject.SetActive(false);
				__instance.transformParticles.gameObject.SetActive(true);
				yield return new WaitForSeconds(0.25f);
				if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardMerging))
				{
					yield return Singleton<TextDisplayer>.Instance.ShowUntilInput(string.Format(Localization.Translate("A ghastly spectactle. But the soul of the [c:bR]{0}[c:] now lives in the [c:bR]{1}[c:]."), sacrificedInfo.DisplayedNameLocalized, __instance.hostSlot.Card.Info.DisplayedNameLocalized), -0.65f, 0.4f, Emotion.Neutral, TextDisplayer.LetterAnimation.Jitter, DialogueEvent.Speaker.Single, null, true);
					ProgressionData.SetMechanicLearned(MechanicsConcept.CardMerging);
				}

				cancelButton.SetButtonInteractable();
				if (!AllowMultipleSacrificesOnOneCard())
				{
					Plugin.Log.LogInfo("Not allowing multiple sacrifices. Waiting for player to remove card");
					
					// Spots disabled but the card is not
					yield return new WaitForSeconds(0.25f); // Wait because RevealAndEnable stops all tweens on the host slot. Test if this works! 
					__instance.hostSlot.RevealAndEnable();
					yield return new WaitWhile(() => __instance.hostSlot.Card != null);
				}
	        }
        }

        private static bool AllowMultipleSacrificesOnOneCard()
        {
	        // Player has turned this on
	        if (Plugin.Instance.CardMergeAllowMultipleSigilTransfers)
	        {
		        return true;
	        }
	        
	        // Mod FuckSacrificeRestrictions is installed
	        if (Plugin.Instance.FuckSacrificeRestrictionsInstalled)
	        {
		        return true;
	        }
	        
	        return false;
        }
    }
    
    [HarmonyPatch(typeof(CardMergeSequencer), "OnSlotSelected", new System.Type[] { typeof(MainInputInteractable) })]
    public class CardMergeSequencer_OnSlotSelected
    {
	    public static bool Prefix(CardMergeSequencer __instance, MainInputInteractable slot)
	    {
		    if (!Plugin.Instance.CardMergeOverrideEnabled)
		    {
			    return true;
		    }
		    Plugin.Log.LogInfo("[OnSlotSelected] " + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);

		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Exit();
		    return true;
	    }
    }

    [HarmonyPatch(typeof(CardMergeSequencer), "OnSelectionEnded", new System.Type[] {typeof(SelectCardFromDeckSlot)})]
    public class CardMergeSequencer_OnSelectionEnded
    {
	    public static bool Prefix(CardMergeSequencer __instance, SelectCardFromDeckSlot slot)
	    {
		    if (!Plugin.Instance.CardMergeOverrideEnabled)
		    {
			    return true;
		    }
		    Plugin.Log.LogInfo("[OnSelectionEnded] " + __instance.pile.cards.Count + " " + __instance.pile.DoingCardOperation);
		    
		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Enter();
		    return true;
	    }
    }
    
    [HarmonyPatch(typeof(CardMergeSequencer), "GetValidCardsForSacrifice")]
    public class CardMergeSequencer_GetValidCardsForSacrifice
    {
	    [HarmonyPrefix]
	    public static bool Prefix(ref List<CardInfo> __result, CardMergeSequencer __instance, CardInfo host = null)
	    {
		    if (!Plugin.Instance.CardMergeOverrideEnabled)
		    {
			    return true;
		    }

		    if (Plugin.Instance.FuckSacrificeRestrictionsInstalled)
		    {
			    return false;
		    }
		    
		    if (!Plugin.Instance.CardMergeAllowMultipleSigilTransfers)
		    {
			    return true;
		    }
		    
		    List<CardInfo> list = new List<CardInfo>(RunState.DeckList);
		    list.RemoveAll((CardInfo x) => x.NumAbilities == 0);
		    bool flag = host != null;
		    if (flag)
		    {
			    list.RemoveAll((CardInfo s) => !__instance.SacrificeOffersNewAbility(host, s));
		    }
		    __result = list;
		    return false;
	    }
	    
	    [HarmonyPrefix]
	    public static void Postfix(ref List<CardInfo> __result, CardMergeSequencer __instance, CardInfo host = null)
	    {
		    if (__result != null)
		    {
			    __result.RemoveAll((a) => a == null || a.HasAbility(DeadAbility.ability));
		    }
	    }
    }
    
    [HarmonyPatch(typeof(CardMergeSequencer), "GetValidCardsForHost")]
    public class CardMergeSequencer_CardMergeSequencer
    {
	    [HarmonyPrefix]
	    public static bool Prefix(ref List<CardInfo> __result, CardMergeSequencer __instance, CardInfo sacrifice = null)
	    {
		    if (!Plugin.Instance.CardMergeOverrideEnabled)
		    {
			    return true;
		    }

		    if (Plugin.Instance.FuckSacrificeRestrictionsInstalled)
		    {
			    return false;
		    }
		    
		    if (!Plugin.Instance.CardMergeAllowMultipleSigilTransfers)
		    {
			    return true;
		    }

		    List<CardInfo> list = new List<CardInfo>(RunState.DeckList);
		    bool flag = sacrifice != null;
		    if (flag)
		    {
			    list.RemoveAll((CardInfo h) => !__instance.SacrificeOffersNewAbility(h, sacrifice));
		    }
		    __result = list;
		    return false;
	    }
	    
	    [HarmonyPrefix]
	    public static void Postfix(ref List<CardInfo> __result, CardMergeSequencer __instance, CardInfo sacrifice = null)
	    {
		    if (__result != null)
		    {
			    __result.RemoveAll((a) => a == null || a.HasAbility(DeadAbility.ability));
		    }
	    }
    }
}