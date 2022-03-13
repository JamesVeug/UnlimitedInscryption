using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using UnlimitedInscryption.Scripts.Backgrounds;
using UnlimitedInscryption.Scripts.Sigils;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UnlimitedInscryption.Scripts.Patches
{
	[HarmonyPatch(typeof (CardStatBoostSequencer), "StatBoostSequence", new System.Type[] {})]
    public class CardStatBoostSequencer_RemoveSequence
    {
        public static bool Prefix(CardStatBoostSequencer __instance, ref IEnumerator __result)
        {
	        if (!Plugin.Instance.FlameOverrideEnabled)
	        {
		        return true;
	        }

            Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
            if (confirmStoneButton == null)
            {
                confirmStoneButton = __instance.transform.Find("ConfirmStoneButton");
                GameObject clone = Object.Instantiate(confirmStoneButton.gameObject, confirmStoneButton.parent);
                clone.name = "CustomCancelButton";
                clone.transform.localPosition = new Vector3(2, 5, -0.5f);

                // Assign new icon
                Transform quad = clone.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0);
                MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
                Material[] materials = quadRenderer.materials;
                materials[0].mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");
                quadRenderer.materials = materials;

                confirmStoneButton = clone.transform;
            }

            ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);

            __result = Sequence(__instance, cancelButton);
            return false;
        }

        private static IEnumerator Sequence(CardStatBoostSequencer __instance, ConfirmStoneButton cancelButton)
        {	        
            // Show intro animation and text
            yield return Intro(__instance, cancelButton);

            bool[] completeList = new bool[] { false };
            
            // Let player sacrifice constantly
            Coroutine coroutine1 = __instance.StartCoroutine(SequenceLoop(__instance, completeList));
            Coroutine coroutine2 = __instance.StartCoroutine(WaitForCancelButton(cancelButton, completeList));
            
            // Wait for them to press the cancel button or run out of sacrifices
            yield return new WaitUntil(()=> completeList[0]);

            if (coroutine1 != null)
            {
	            __instance.StopCoroutine(coroutine1);
            }

            if (coroutine2 != null)
            {
	            __instance.StopCoroutine(coroutine2);
            }

            // Outro
            yield return Outro(__instance, cancelButton);
        }

        private static IEnumerator WaitForCancelButton(ConfirmStoneButton cancelButton, bool[] list)
        {
	        yield return cancelButton.WaitUntilConfirmation();
	        list[0] = true;
        }

        private static IEnumerator Outro(CardStatBoostSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        Singleton<TextDisplayer>.Instance.Clear();
	        __instance.confirmStone.SetStoneInactive();
	        cancelButton.SetStoneInactive();
	        yield return new WaitForSeconds(0.1f);
	        if (__instance.selectionSlot.Card != null)
	        {
		        __instance.selectionSlot.FlyOffCard();
	        }
	        
	        Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
			yield return new WaitForSeconds(0.25f);
			if (__instance.selectionSlot.transform.Find("FireAnim").gameObject.activeSelf)
			{
				AudioController.Instance.PlaySound3D("campfire_putout", MixerGroup.TableObjectsSFX,
					__instance.selectionSlot.transform.position, 1f, 0f, null, null, null, null, false);
				AudioController.Instance.StopLoop(1);
			}

			__instance.campfireLight.gameObject.SetActive(false);
			Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(false);
			yield return __instance.pile.DestroyCards(0.5f);
			yield return new WaitForSeconds(0.2f);
			__instance.figurines.ForEach(delegate(CompositeFigurine x)
			{
				x.gameObject.SetActive(false);
			});
			__instance.stakeRingParent.SetActive(false);
			__instance.selectionSlot.gameObject.SetActive(false);
			CustomCoroutine.WaitThenExecute(0.4f, delegate
			{
				Singleton<ExplorableAreaManager>.Instance.HangingLight.intensity = 0f;
				Singleton<ExplorableAreaManager>.Instance.HangingLight.gameObject.SetActive(true);
				Singleton<ExplorableAreaManager>.Instance.HandLight.intensity = 0f;
				Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(true);
			}, false);
			
			ProgressionData.SetMechanicLearned(MechanicsConcept.CardStatBoost);
			if (Singleton<GameFlowManager>.Instance != null)
			{
				Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map, null);
			}
        }
        
        private static IEnumerator Intro(CardStatBoostSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardStatBoost))
			{
				__instance.attackMod = true;
			}
			else
			{
				__instance.attackMod = SeededRandom.Bool(SaveManager.SaveFile.GetCurrentRandomSeed());
			}
			__instance.selectionSlot.specificRenderers[0].material.mainTexture = (__instance.attackMod ? __instance.attackModSlotTexture : __instance.healthModSlotTexture);
			__instance.figurines.ForEach(delegate(CompositeFigurine x)
			{
				x.SetArms(__instance.attackMod ? CompositeFigurine.FigurineType.Wildling : CompositeFigurine.FigurineType.SettlerWoman);
			});
			__instance.figurines.ForEach(delegate(CompositeFigurine x)
			{
				x.gameObject.SetActive(false);
			});
			__instance.stakeRingParent.SetActive(false);
			__instance.campfireLight.gameObject.SetActive(false);
			__instance.campfireLight.intensity = 0f;
			__instance.campfireCardLight.intensity = 0f;
			__instance.selectionSlot.Disable();
			__instance.selectionSlot.gameObject.SetActive(false);
			yield return new WaitForSeconds(0.3f);
			Singleton<ExplorableAreaManager>.Instance.HangingLight.gameObject.SetActive(false);
			Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(false);
			Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, true);
			Singleton<ViewManager>.Instance.OffsetPosition(new Vector3(0f, 0f, 2.25f), 0.1f);
			yield return new WaitForSeconds(1f);
			if (!AlwaysSucceedFire())
			{
				__instance.figurines.ForEach(delegate(CompositeFigurine x)
				{
					x.gameObject.SetActive(true);
				});
			}
			__instance.stakeRingParent.SetActive(true);
			Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(true);
			__instance.campfireLight.gameObject.SetActive(true);
			__instance.selectionSlot.transform.Find("FireAnim").gameObject.SetActive(false);
			__instance.selectionSlot.gameObject.SetActive(true);
			__instance.selectionSlot.RevealAndEnable();
			__instance.selectionSlot.ClearDelegates();
			cancelButton.Enter();
			SelectCardFromDeckSlot selectCardFromDeckSlot = __instance.selectionSlot;
			selectCardFromDeckSlot.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardFromDeckSlot.CursorSelectStarted, new Action<MainInputInteractable>(__instance.OnSlotSelected));
			if (Random.value < 0.25f && Singleton<VideoCameraRig>.Instance != null)
			{
				Singleton<VideoCameraRig>.Instance.PlayCameraAnim("refocus_quick");
			}
			AudioController.Instance.PlaySound3D("campfire_light", MixerGroup.TableObjectsSFX, __instance.selectionSlot.transform.position, 1f, 0f, null, null, null, null, false);
			AudioController.Instance.SetLoopAndPlay("campfire_loop", 1, true, true);
			AudioController.Instance.SetLoopVolumeImmediate(0f, 1);
			AudioController.Instance.FadeInLoop(0.5f, 0.75f, new int[]
			{
				1
			});
			Singleton<InteractionCursor>.Instance.SetEnabled(false);
			yield return new WaitForSeconds(0.25f);
			yield return __instance.pile.SpawnCards(RunState.DeckList.Count, 0.5f);
			Singleton<TableRuleBook>.Instance.SetOnBoard(true);
			Singleton<InteractionCursor>.Instance.SetEnabled(true);
			if (RunState.Run.survivorsDead)
			{
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostSurvivorsDead", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
			}
			else
			{
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostIntro", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, new string[]
				{
					__instance.GetTranslatedStatText(__instance.attackMod)
				}, null);
			}
        }

        private static IEnumerator SequenceLoop(CardStatBoostSequencer __instance, bool[] completeList)
        {
	        int numBuffsGiven = 0;
	        bool extinguishd = false;
	        while (true)
	        {
				// Dialogue
				if (!RunState.Run.survivorsDead)
				{
					yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostPushLuck" + numBuffsGiven, TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
					yield return new WaitForSeconds(0.1f);
					switch (numBuffsGiven)
					{
						case 1:
							Singleton<TextDisplayer>.Instance.ShowMessage("Push your luck? Or pull away?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null);
							break;
						case 2:
							Singleton<TextDisplayer>.Instance.ShowMessage("Push your luck further? Or run back?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null);
							break;
						case 3:
							Singleton<TextDisplayer>.Instance.ShowMessage("Recklessly continue?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null);
							break;
					}
				}
				
	        
				// Let player change 
				__instance.selectionSlot.RevealAndEnable();
				
				// Wait to have a card to use
				if(__instance.selectionSlot.Card == null)
				{
					yield return new WaitWhile(() => __instance.selectionSlot.Card == null);
				}
				
				__instance.confirmStone.Unpress();
				
				yield return __instance.confirmStone.WaitUntilConfirmation();
				
				Singleton<TextDisplayer>.Instance.Clear();
				__instance.confirmStone.Disable();
				__instance.selectionSlot.Disable();
				__instance.selectionSlot.Card.SetInfo(__instance.selectionSlot.Card.Info);
				yield return new WaitForSeconds(0.1f);
				if (numBuffsGiven > 0)
				{
					if (!AlwaysSucceedFire())
					{
						// Buff
						float testValue = Plugin.Instance.FlameDestroyCardChance / 100f; // 22.5 / 100 = 0.225f
						float destroyChance = Mathf.Clamp(1f - testValue, 0.0f, 1.0f); // 77.5
						float value = SeededRandom.Value(SaveManager.SaveFile.GetCurrentRandomSeed() + numBuffsGiven);
						if (value > destroyChance)
						{
							yield return DestroyCard(__instance);
						}
					}
					else
					{
						// Extinguish
						float testValue = Plugin.Instance.FlameExtinguishChance / 100f; // 10.0 / 100 = 0.1f
						float extinguishChance = Mathf.Clamp(1f - testValue, 0.0f, 1.0f); // 0.9
						float value = SeededRandom.Value(SaveManager.SaveFile.GetCurrentRandomSeed() + numBuffsGiven);
						extinguishd = value > extinguishChance;
					}
				}
				
				// Buff card
				if (__instance.selectionSlot.Card != null)
				{
					// Disable slot changes while animating
					__instance.selectionSlot.Disable();

					Singleton<RuleBookController>.Instance.SetShown(false, true);
					yield return new WaitForSeconds(0.25f);
					__instance.selectionSlot.Card.Anim.PlayTransformAnimation();

					if (extinguishd)
					{
						yield return Extinguish(__instance);
					}
					else
					{
						BuffCard(__instance);
					}
					yield return new WaitForSeconds(0.15f);
				}

				numBuffsGiven++;
				if (extinguishd)
				{
					break;
				}

				if (!Plugin.Instance.FlameBypassPastRunLimit && SaveManager.SaveFile.pastRuns.Count < 4)
				{
					break;
				}
	        }

	        //Plugin.Log.LogInfo("[CardStatBoostSequencer_RemoveSequence] Sacrificing done");
	        completeList[0] = true;
        }

        private static IEnumerator Extinguish(CardStatBoostSequencer __instance)
        {
	        AudioController.Instance.PlaySound3D("campfire_putout", MixerGroup.TableObjectsSFX,
		        __instance.selectionSlot.transform.position, 1f, 0f, null, null, null, null, false);
	        AudioController.Instance.StopLoop(1);
	        
	        __instance.selectionSlot.transform.Find("FireAnim").gameObject.SetActive(false);
	        __instance.selectionSlot.Disable();
	        __instance.confirmStone.Disable();
	        
	        CardInfo newInfo = CardLoader.GetCardByName("BurnedCard");
	        SelectableCard card = __instance.selectionSlot.Card;
	        CardInfo oldInfo = card.Info;
	        
	        newInfo.Mods.Add(new CardModificationInfo()
	        {
		        nameReplacement = card.Info.displayedName,
		        bloodCostAdjustment = oldInfo.BloodCost,
		        bonesCostAdjustment = oldInfo.bonesCost,
		        energyCostAdjustment = oldInfo.energyCost
	        });
	        
	        card.SetInfo(newInfo);
	        RunState.Run.playerDeck.RemoveCard(oldInfo);
	        RunState.Run.playerDeck.AddCard(newInfo);
	        
	        yield return Singleton<TextDisplayer>.Instance.ShowUntilInput($"An ember ignites the [c:bR]{newInfo.displayedName}[c:] on fire burning it and extinguishes the Flame.", 0, 0.4f, Emotion.Laughter, TextDisplayer.LetterAnimation.Jitter, DialogueEvent.Speaker.Single, null);
        }

        private static void BuffCard(CardStatBoostSequencer __instance)
        {
	        AudioController.Instance.PlaySound3D("card_blessing", MixerGroup.TableObjectsSFX,
		        __instance.selectionSlot.transform.position, 1f, 0f, null, null, null, null, false);
	        __instance.ApplyModToCard(__instance.selectionSlot.Card.Info);
	        __instance.selectionSlot.Card.SetInfo(__instance.selectionSlot.Card.Info);
        }

        private static IEnumerator DestroyCard(CardStatBoostSequencer __instance)
        {
	        // Destroy card
	        SelectableCard destroyingCard = __instance.selectionSlot.Card;
	        CardInfo destroyedCardInfo = destroyingCard.Info;
	        destroyingCard.Anim.PlayDeathAnimation(true);
	        RunState.Run.playerDeck.RemoveCard(destroyedCardInfo);
	        yield return new WaitForSeconds(1f);
	        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostCardEaten",
		        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, new string[]
		        {
			        destroyedCardInfo.DisplayedNameLocalized
		        }, null);
	        yield return new WaitForSeconds(0.1f);
	        __instance.selectionSlot.DestroyCard();
	        __instance.selectionSlot.FlyOffCard();

	        // Give item
	        if (RunState.Run.consumables.Count < 3)
	        {
		        yield return new WaitForSeconds(0.4f);
		        Singleton<ViewManager>.Instance.SwitchToView(View.Consumables, false, false);
		        yield return new WaitForSeconds(0.2f);
		        RunState.Run.consumables.Add("PiggyBank");
		        Singleton<ItemsManager>.Instance.UpdateItems(false);
		        yield return new WaitForSeconds(0.5f);
		        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostCardEatenBones",
			        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
		        Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
	        }

	        // Kill survivors
	        if (destroyedCardInfo.HasTrait(Trait.KillsSurvivors))
	        {
		        RunState.Run.survivorsDead = true;
		        __instance.figurines.ForEach(delegate(CompositeFigurine x) { x.gameObject.SetActive(false); });
		        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostSurvivorsDead",
			        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
	        }
        }

        private static bool AlwaysSucceedFire()
        {
	        if (RunState.Run.survivorsDead)
	        {
		        return true;
	        }

	        if (Plugin.Instance.FlameDestroyCardChance <= 0)
	        {
		        return true;
	        }

	        if (Plugin.Instance.FirePitAlwaysAbleToUpgradeInstalled)
	        {
		        return true;
	        }

	        return false;
        }
    }
    
    [HarmonyPatch(typeof(CardStatBoostSequencer), "OnSlotSelected", new System.Type[] { typeof(MainInputInteractable) })]
    public class CardStatBoostSequencer_OnSlotSelected
    {
	    public static bool Prefix(CardStatBoostSequencer __instance, MainInputInteractable slot)
	    {
		    if (!Plugin.Instance.FlameOverrideEnabled)
		    {
			    return true;
		    }
			
		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Exit();
		    return true;
	    }
    }

    [HarmonyPatch(typeof(CardStatBoostSequencer), "OnSelectionEnded", new System.Type[] {})]
    public class CardStatBoostSequencer_OnSelectionEnded
    {
	    public static bool Prefix(CardStatBoostSequencer __instance)
	    {
		    if (!Plugin.Instance.FlameOverrideEnabled)
		    {
			    return true;
		    }

		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Enter();
		    return true;
	    }
    }

    [HarmonyPatch(typeof(CardStatBoostSequencer), "GetValidCards", new System.Type[] {})]
    public class CardStatBoostSequencer_GetValidCards
    {
	    public static void Postfix(CardStatBoostSequencer __instance, ref List<CardInfo> __result)
	    {
		    __result.RemoveAll((a) => a.HasAbility(DeadAbility.ability));
	    }
    }
}