using System;
using System.Collections;
using System.Collections.Generic;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
    [HarmonyPatch(typeof (DuplicateMergeSequencer), "MergeSequence", new System.Type[] {})]
    public class DuplicateMergeSequencer_MergeSequence
    {
        public static bool Prefix(DuplicateMergeSequencer __instance, ref IEnumerator __result)
        {
	        if (!Configs.DuplicateMergeOverrideEnabled)
	        {
		        return true;
	        }

            Transform cancelButtonTransform = __instance.transform.Find("CustomCancelButton");
            if (cancelButtonTransform == null)
            {
                cancelButtonTransform = __instance.transform.Find("ConfirmStoneButton");
                GameObject clone = Object.Instantiate(cancelButtonTransform.gameObject, cancelButtonTransform.parent);
                clone.name = "CustomCancelButton";
                clone.transform.GetChild(0).gameObject.SetActive(true);
                clone.transform.localPosition = new Vector3(3, 5, -1.3f);

                // Assign new icon
                Transform quad = clone.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0);
                MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
                Material[] materials = quadRenderer.materials;
                materials[0].mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");
                quadRenderer.materials = materials;

                cancelButtonTransform = clone.transform;
            }

            ConfirmStoneButton cancelButton = cancelButtonTransform.GetComponentInChildren<ConfirmStoneButton>(true);
            __result = Sequence(__instance, cancelButton);
            return false;
        }

        private static IEnumerator Sequence(DuplicateMergeSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        yield return Intro(__instance);

	        if (__instance.GetValidDuplicateCards().Count == 0)
	        {
		        yield return __instance.SelectDuplicateSequence();
	        }
            else
            {
	            AudioController.Instance.PlaySound3D("mushroom_large_appear", MixerGroup.TableObjectsSFX, __instance.largeMushroom.transform.position, 1f, 0f, null, null, null, null, false);
	            CustomCoroutine.WaitThenExecute(0.8f, delegate
	            {
		            AudioController.Instance.PlaySound3D("mushroom_large_hit", MixerGroup.TableObjectsSFX, __instance.largeMushroom.transform.position, 1f, 0f, null, null, null, null, false);
	            }, false);
	            __instance.largeMushroom.SetActive(true);
	            yield return new WaitForSeconds(0.5f);
	            foreach (GameObject mushroom in __instance.slotMushrooms)
	            {
		            mushroom.SetActive(true);
		            yield return new WaitForSeconds(0.05f);
		            mushroom.GetComponent<Collider>().enabled = true;
	            }
	            
	            // Let player sacrifice constantly
	            List<bool> done = new List<bool>() { false, false };
	            Coroutine doneSacrificing = __instance.StartCoroutine(SacrificeCard(__instance, cancelButton, done));
	            Coroutine waitForCancel = __instance.StartCoroutine(WaitUntilConfirmation(cancelButton, done));
	            
	            
	            // Wait for them to press the cancel button or run out of cards to merge
	            yield return new WaitUntil(() =>
	            {
		            return done[0] || done[1]; // Yes i know gross but don't care about nice code
	            });

	            if (doneSacrificing != null)
	            {
		            __instance.StopCoroutine(doneSacrificing);
	            }

	            if (waitForCancel != null)
	            {
		            __instance.StopCoroutine(waitForCancel);
	            }
            }

            // Outro
            yield return Outro(__instance, cancelButton);
        }

        private static IEnumerator WaitUntilConfirmation(ConfirmStoneButton cancelButton, List<bool> done)
        {
	        yield return cancelButton.WaitUntilConfirmation();
	        done[1] = true;
        }

        private static IEnumerator Intro(DuplicateMergeSequencer __instance)
        {
	        __instance.selectionSlot.gameObject.SetActive(true);
	        __instance.selectionSlot.Disable();
	        Singleton<TableRuleBook>.Instance.SetOnBoard(true);
	        Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
	        yield return new WaitForSeconds(0.2f);
	        AudioController.Instance.PlaySound3D("mushrooms_small_appear", MixerGroup.TableObjectsSFX,
		        __instance.largeMushroom.transform.position, 1f, 0f, null, null, null, null, false);
	        foreach (GameObject mushroom in __instance.mushrooms)
	        {
		        mushroom.SetActive(true);
		        yield return new WaitForSeconds(0.05f);
		        mushroom.GetComponent<Collider>().enabled = true;
	        }

	        if (!ProgressionData.LearnedMechanic(MechanicsConcept.DuplicateMerge))
	        {
		        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("DuplicateMergeIntro",
			        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
	        }

	        LeshyAnimationController.Instance.PutOnMask(LeshyAnimationController.Mask.Doctor, true);
	        yield return new WaitForSeconds(1f);
	        Singleton<ViewManager>.Instance.SwitchToView(View.MaskDialogue, false, true);
	        Singleton<OpponentAnimationController>.Instance.SetHeadTrigger("doctor_idle");
	        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("DoctorIntro",
		        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null,
		        new Action<DialogueEvent.Line>(__instance.OnDoctorDialogueLine));
	        __instance.SetSideHeadTalking(false);
	        Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
	        yield return new WaitForSeconds(0.1f);
        }

        private static IEnumerator Outro(DuplicateMergeSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        __instance.StartCoroutine(LeshyAnimationController.Instance.TakeOffMask());
	        cancelButton.Exit();
	        __instance.selectionSlot.FlyOffCard();
	        yield return __instance.pile.DestroyCards();
	        yield return __instance.CleanUp();
	        if (Singleton<GameFlowManager>.Instance != null)
	        {
		        Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map, null);
	        }
        }

        private static IEnumerator SacrificeCard(DuplicateMergeSequencer __instance, ConfirmStoneButton cancelButton, List<bool> bools)
        {
	        while (__instance.GetValidDuplicateCards().Count > 0)
	        {
		        yield return __instance.pile.SpawnCards(RunState.DeckList.Count, 0.5f);
	        
		        cancelButton.transform.parent.parent.gameObject.SetActive(true);
		        cancelButton.Enter();
		        
		        __instance.selectionSlot.SetShown(true, false);
		        __instance.selectionSlot.ShowState(HighlightedInteractable.State.Interactable, false, 0.15f);
		        Singleton<ViewManager>.Instance.SwitchToView(View.CardMergeSlots, false, true);
		        
		        __instance.selectionSlot.gameObject.SetActive(true);
		        __instance.selectionSlot.RevealAndEnable();
		        __instance.selectionSlot.ClearDelegates();
		        
		        SelectCardPairFromDeckSlot selectCardPairFromDeckSlot = __instance.selectionSlot;
		        selectCardPairFromDeckSlot.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardPairFromDeckSlot.CursorSelectStarted, new Action<MainInputInteractable>(__instance.OnSlotSelected));
		        yield return __instance.confirmStone.WaitUntilConfirmation();
		        cancelButton.Disable();
		        __instance.selectionSlot.ClearDelegates();
		        
		        // Combine
		        Singleton<RuleBookController>.Instance.SetShown(false, true);
		        yield return CombinePair(__instance, __instance.selectionSlot.SelectedPair);
		        ProgressionData.SetMechanicLearned(MechanicsConcept.DuplicateMerge);
	        }

	        bools[0] = true;
        }
        
        private static IEnumerator CombinePair(DuplicateMergeSequencer __instance, SelectableCardPair pair)
		{
			// Dialogue
			Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
			yield return new WaitForSeconds(0.15f);
			LeshyAnimationController.Instance.RightArm.PlayAnimation("doctor_hand_intro");
			yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("DuplicateMergeLookAway", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, new Action<DialogueEvent.Line>(__instance.OnDoctorDialogueLine));
			__instance.SetSideHeadTalking(false);
			
			// Show looking down animation
			Singleton<ViewManager>.Instance.SwitchToView(View.TableStraightDown, false, false);
			AudioController.Instance.PlaySound3D("mycologist_carnage", MixerGroup.TableObjectsSFX, __instance.largeMushroom.transform.position, 1f, 0f, null, null, null, null, false);
			yield return new WaitForSeconds(1f);
			Singleton<CameraEffects>.Instance.Shake(0.1f, 0.25f);
			__instance.bloodParticles1.gameObject.SetActive(true);
			yield return new WaitForSeconds(0.5f);
			Singleton<CameraEffects>.Instance.Shake(0.05f, 0.4f);
			__instance.paperParticles.gameObject.SetActive(true);
			yield return new WaitForSeconds(1f);
			__instance.bloodParticles2.gameObject.SetActive(true);
			Singleton<CameraEffects>.Instance.Shake(0.1f, 0.4f);
			yield return new WaitForSeconds(0.5f);
			
			// Achievement
			if (!pair.LeftCard.Info.Mods.Exists((CardModificationInfo x) => x.fromDuplicateMerge))
			{
				if (!pair.RightCard.Info.Mods.Exists((CardModificationInfo x) => x.fromDuplicateMerge))
				{
					goto IL_26F;
				}
			}
			AchievementManager.Unlock(Achievement.PART1_SPECIAL2);
			IL_26F:
			
			// Create merged card
			CardInfo info = __instance.MergeCards(pair.LeftCard.Info, pair.RightCard.Info);
			__instance.mergedCard = __instance.SpawnMergedCard(info, pair.transform.position, pair.transform.eulerAngles);
			__instance.selectionSlot.DestroyCard();
			__instance.selectionSlot.gameObject.SetActive(false);
			
			// Show merged card
			Singleton<ViewManager>.Instance.SwitchToView(View.CardMergeSlots, false, false);
			
			// Dialogue
			yield return new WaitForSeconds(0.15f);
			yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("DuplicateMergeResult", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, new string[]
			{
				pair.LeftCard.Info.DisplayedNameLocalized
			}, null);
			
			// Cleanup
			Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
			yield return new WaitForSeconds(0.1f);
			__instance.mergedCard.ExitBoard(0.4f, new Vector3(1f, 0f, -2f));
			yield return new WaitForSeconds(0.5f);
			LeshyAnimationController.Instance.RightArm.SetTrigger("doctor_hide");
			CustomCoroutine.WaitThenExecute(1f, delegate
			{
				LeshyAnimationController.Instance.RightArm.SetHidden(true);
			}, false);
			__instance.bloodParticles1.gameObject.SetActive(false);
			__instance.bloodParticles2.gameObject.SetActive(false);
			__instance.paperParticles.gameObject.SetActive(false);
			yield break;
		}
    }

    [HarmonyPatch(typeof(DuplicateMergeSequencer), "GetValidDuplicateCards", new System.Type[] { })]
    public class DuplicateMergeSequencer_GetValidDuplicateCards
    {
	    public static bool Prefix(CardMergeSequencer __instance, ref List<CardInfo> __result)
	    {
		    if (!Configs.DuplicateMergeOverrideEnabled)
		    {
			    return true;
		    }

		    List<CardInfo> list = new List<CardInfo>(RunState.DeckList);
		    list.RemoveAll((a => a.name.Contains("DEATHCARD")));

		    for (var i = 0; i < list.Count; i++)
		    {
			    CardInfo card = list[i];
			    bool notEventNumbered = list.FindAll((CardInfo x) => x.name == card.name).Count % 2 != 0;
			    if (notEventNumbered)
			    {
				    list.RemoveAt(i--);
			    }
		    }

		    __result = list;
		    return false;
	    }
    }
    
    [HarmonyPatch(typeof(DuplicateMergeSequencer), "OnSlotSelected", new System.Type[] { typeof(MainInputInteractable) })]
    public class DuplicateMergeSequencer_OnSlotSelected
    {
	    public static bool Prefix(DuplicateMergeSequencer __instance, MainInputInteractable slot)
	    {
		    if (!Configs.DuplicateMergeOverrideEnabled)
		    {
			    return true;
		    }

		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Exit();
		    return true;
	    }
    }

    [HarmonyPatch(typeof(DuplicateMergeSequencer), "OnSelectionEnded", new System.Type[] {})]
    public class DuplicateMergeSequencer_OnSelectionEnded
    {
	    public static bool Prefix(DuplicateMergeSequencer __instance)
	    {
		    if (!Configs.DuplicateMergeOverrideEnabled)
		    {
			    return true;
		    }

		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Enter();
		    return true;
	    }
    }
}