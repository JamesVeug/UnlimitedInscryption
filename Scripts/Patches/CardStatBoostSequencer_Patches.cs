using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
    [HarmonyPatch(typeof(SpecialNodeHandler), nameof(SpecialNodeHandler.StartSpecialNodeSequence))]
    public class CardStatBoostSequencer_StartSpecialNodeSequence
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.StatBoostSequence));
            var replacement = AccessTools.Method(typeof(CardStatBoostSequencer_StartSpecialNodeSequence), nameof(CreateSequence));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    // Replace the call before Mono can inline the coroutine factory into the dispatcher.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException("Expected one campfire sequence call in StartSpecialNodeSequence, found " + replaced);
            }
        }

        public static IEnumerator CreateSequence(CardStatBoostSequencer sequencer)
        {
            IEnumerator result = null;
            if (CardStatBoostSequencer_RemoveSequence.CardStatBoostSequencer_StatBoostSequence(sequencer, ref result))
            {
                return sequencer.StatBoostSequence();
            }
            return result;
        }
    }

	[HarmonyPatch(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.StatBoostSequence), new Type[] { })]
    public class CardStatBoostSequencer_RemoveSequence
    {
	    internal static bool m_IsCancelButtonShowing = false;
	    [HarmonyPrefix]
        public static bool CardStatBoostSequencer_StatBoostSequence(CardStatBoostSequencer __instance, ref IEnumerator __result)
        {
	        if (!Configs.FlameOverrideEnabled)
	        {
		        Plugin.Log.LogError("FlameOverrideEnabled is false, returning original enumerator");
		        return true;
	        }

	        Transform instanceTransform = __instance.transform;
	        Transform confirmStoneButton = instanceTransform.Find("CustomCancelButton");
            if (confirmStoneButton == null)
            {
                confirmStoneButton = __instance.transform.Find("ConfirmStoneButton");
                GameObject clone = Object.Instantiate(confirmStoneButton.gameObject, confirmStoneButton.parent);
                clone.name = "CustomCancelButton";
                clone.transform.GetChild(0).gameObject.SetActive(true);
                clone.transform.localPosition = new Vector3(2, 5, -0.5f);

                // Assign new icon
                Transform quad = clone.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0);
                MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
                Material[] materials = quadRenderer.materials;
                materials[0].mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");
                quadRenderer.materials = materials;

                confirmStoneButton = clone.transform;
            }
            else
            {
	            Plugin.Log.LogError("Cancel button found, using existing one");
            }

            ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
            Debug.Assert(cancelButton, "cancelButton is null but the gameObject is not");
            cancelButton.confirmView = View.Default;
            confirmStoneButton.gameObject.SetActive(true);
            cancelButton.transform.parent.parent.gameObject.SetActive(true);

            m_IsCancelButtonShowing = false;
            __result = Sequence(__instance, cancelButton);
            return false;
        }

        private static IEnumerator Sequence(CardStatBoostSequencer __instance, ConfirmStoneButton cancelButton)
        {
            Plugin.Log.LogInfo("[Campfire] Starting unlimited buffs with exit button.");
	        bool attackMod;
	        bool killSurvivors = false;
			if (__instance.GetValidCards(true).Count == 0)
			{
				attackMod = false;
			}
			else if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardStatBoost))
			{
				attackMod = true;
			}
			else
			{
				attackMod = SeededRandom.Bool(SaveManager.SaveFile.GetCurrentRandomSeed());
			}
			__instance.selectionSlot.specificRenderers[0].material.mainTexture = (attackMod ? __instance.attackModSlotTexture : __instance.healthModSlotTexture);
			__instance.figurines.ForEach(delegate(CompositeFigurine x)
			{
				x.SetArms((!attackMod) ? CompositeFigurine.FigurineType.SettlerWoman : CompositeFigurine.FigurineType.Wildling);
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
			if (!RunState.Run.survivorsDead)
			{
				__instance.figurines.ForEach(delegate(CompositeFigurine x)
				{
					x.gameObject.SetActive(true);
				});
			}
			__instance.stakeRingParent.SetActive(true);
			Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(true);
			__instance.campfireLight.gameObject.SetActive(true);
			__instance.selectionSlot.gameObject.SetActive(true);
			__instance.selectionSlot.RevealAndEnable();
			__instance.selectionSlot.ClearDelegates();
			SelectCardFromDeckSlot selectCardFromDeckSlot = __instance.selectionSlot;
			selectCardFromDeckSlot.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardFromDeckSlot.CursorSelectStarted, (Action<MainInputInteractable>)delegate(MainInputInteractable i)
			{
				__instance.OnSlotSelected(i, attackMod);
			});
			if (UnityEngine.Random.value < 0.25f && Singleton<VideoCameraRig>.Instance != null)
			{
				Singleton<VideoCameraRig>.Instance.PlayCameraAnim("refocus_quick");
			}
			AudioController.Instance.PlaySound3D("campfire_light", MixerGroup.TableObjectsSFX, __instance.selectionSlot.transform.position);
			AudioController.Instance.SetLoopAndPlay("campfire_loop", 1);
			AudioController.Instance.SetLoopVolumeImmediate(0f, 1);
			AudioController.Instance.FadeInLoop(0.5f, 0.75f, 1);
			Singleton<InteractionCursor>.Instance.SetEnabled(false);
			yield return new WaitForSeconds(0.25f);
			yield return __instance.pile.SpawnCards(RunState.DeckList.Count, 0.5f);
			Singleton<TableRuleBook>.Instance.SetOnBoard(true);
			Singleton<InteractionCursor>.Instance.SetEnabled(true);

			if (RunState.Run.survivorsDead)
			{
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostSurvivorsDead",
					TextDisplayer.MessageAdvanceMode.Input);
			}
			else
			{
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostIntro",
					TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait,
					new string[1] { __instance.GetTranslatedStatText(attackMod) });
			}

            int attempt = 0;
            int randomSeed = SaveManager.SaveFile.GetCurrentRandomSeed();
            cancelButton.Enter();
            while (true)
            {
                bool hasValidCards = __instance.GetValidCards(attackMod).Count > 0;
                if (hasValidCards)
                {
                    __instance.selectionSlot.RevealAndEnable();
                }
                else
                {
                    __instance.selectionSlot.Disable();
                }

                if (__instance.selectionSlot.Card != null)
                {
                    __instance.selectionSlot.Card.SetInteractionEnabled(true);
                    __instance.confirmStone.Unpress();
                }
                else
                {
                    __instance.confirmStone.Exit();
                    __instance.confirmStone.Disable();
                }

                cancelButton.transform.parent.parent.gameObject.SetActive(true);
                m_IsCancelButtonShowing = true;
                cancelButton.Unpress();
                Coroutine confirm = __instance.StartCoroutine(__instance.confirmStone.WaitUntilConfirmation());
                Coroutine exit = __instance.StartCoroutine(cancelButton.WaitUntilConfirmation());
                try
                {
                    yield return new WaitUntil(() => cancelButton.SelectionConfirmed ||
                        (__instance.confirmStone.SelectionConfirmed && __instance.selectionSlot.Card != null));
                }
                finally
                {
                    __instance.StopCoroutine(confirm);
                    __instance.StopCoroutine(exit);
                    __instance.confirmStone.ClearDelegates();
                    cancelButton.ClearDelegates();
                }

                bool exitRequested = cancelButton.SelectionConfirmed;
                m_IsCancelButtonShowing = false;
                cancelButton.Disable();
                __instance.confirmStone.Disable();
                __instance.selectionSlot.Disable();
                Singleton<TextDisplayer>.Instance.Clear();
                Singleton<RuleBookController>.Instance.SetShown(false);
                if (exitRequested)
                {
                    break;
                }

                CardInfo cardInfo = __instance.selectionSlot.Card.Info;
                __instance.selectionSlot.Card.SetInteractionEnabled(false);
                float chance = Configs.FlameDestroyCardChance / 100f;
                float roll = SeededRandom.Value(unchecked(randomSeed + attempt++));
                bool destroyed = !RunState.Run.survivorsDead && !killSurvivors &&
                    (chance >= 1f || (chance > 0f && roll < chance));
                if (destroyed)
                {
                    __instance.confirmStone.Exit();
                    __instance.confirmStone.SelectionConfirmed = false;
                    if (cardInfo.HasTrait(Trait.KillsSurvivors) || cardInfo.HasAbility(Ability.Deathtouch))
                    {
                        killSurvivors = true;
                        RunState.Run.survivorsDead = true;
                    }

                    __instance.selectionSlot.Card.Anim.PlayDeathAnimation();
                    RunState.Run.playerDeck.RemoveCard(cardInfo);
                    yield return new WaitForSeconds(1f);
                    yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostCardEaten",
                        TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait,
                        new string[] { cardInfo.DisplayedNameLocalized });
                    __instance.selectionSlot.DestroyCard();
                    // Unity destroys the card at the end of the frame. Do not re-enable its button meanwhile.
                    yield return null;
                    if (killSurvivors)
                    {
                        __instance.figurines.ForEach(x => x.gameObject.SetActive(false));
                    }

                    if (RunState.Run.consumables.Count < RunState.Run.MaxConsumables)
                    {
                        Singleton<ViewManager>.Instance.SwitchToView(View.Consumables);
                        yield return new WaitForSeconds(0.2f);
                        RunState.Run.consumables.Add("PiggyBank");
                        Singleton<ItemsManager>.Instance.UpdateItems();
                        yield return new WaitForSeconds(0.5f);
                        yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent(
                            "StatBoostCardEatenBones", TextDisplayer.MessageAdvanceMode.Input);
                    }
                    Singleton<ViewManager>.Instance.SwitchToView(View.Default);
                }
                else
                {
                    yield return new WaitForSeconds(0.25f);
                    AudioController.Instance.PlaySound3D("card_blessing", MixerGroup.TableObjectsSFX,
                        __instance.selectionSlot.transform.position);
                    __instance.selectionSlot.Card.Anim.PlayTransformAnimation();
                    __instance.ApplyModToCard(cardInfo, attackMod);
                    yield return new WaitForSeconds(0.15f);
                    __instance.selectionSlot.Card.SetInfo(cardInfo);
                    yield return new WaitForSeconds(0.75f);
                }
            }

            __instance.selectionSlot.ClearDelegates();
            __instance.retrieveCardInteractable.gameObject.SetActive(false);
            if (__instance.selectionSlot.Card != null)
            {
                yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostOutro",
                    TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait,
                    new string[] { __instance.GetTranslatedStatText(attackMod),
                        __instance.selectionSlot.Card.Info.DisplayedNameLocalized });
            }
			SaveManager.SaveToFile();
			yield return new WaitForSeconds(0.1f);
			if (__instance.selectionSlot.Card != null)
			{
				__instance.selectionSlot.FlyOffCard();
			}

			Singleton<ViewManager>.Instance.SwitchToView(View.Default);
			yield return new WaitForSeconds(0.25f);
			AudioController.Instance.PlaySound3D("campfire_putout", MixerGroup.TableObjectsSFX, __instance.selectionSlot.transform.position);
			AudioController.Instance.StopLoop(1);
			__instance.campfireLight.gameObject.SetActive(false);
			Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(false);
			yield return __instance.pile.DestroyCards();
			yield return new WaitForSeconds(0.2f);
			__instance.figurines.ForEach(delegate(CompositeFigurine x)
			{
				x.gameObject.SetActive(false);
			});
			__instance.stakeRingParent.SetActive(false);
			__instance.confirmStone.SetStoneInactive();
			__instance.selectionSlot.gameObject.SetActive(false);
			cancelButton.SetStoneInactive();
			CustomCoroutine.WaitThenExecute(0.4f, delegate
			{
				Singleton<ExplorableAreaManager>.Instance.HangingLight.intensity = 0f;
				Singleton<ExplorableAreaManager>.Instance.HangingLight.gameObject.SetActive(true);
				Singleton<ExplorableAreaManager>.Instance.HandLight.intensity = 0f;
				Singleton<ExplorableAreaManager>.Instance.HandLight.gameObject.SetActive(true);
			});
			if (killSurvivors && !RunState.Run.survivorsDead)
			{
				RunState.Run.survivorsDead = true;
			}
			ProgressionData.SetMechanicLearned(MechanicsConcept.CardStatBoost);
			if (Singleton<GameFlowManager>.Instance != null)
			{
				Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map);
			}
        }


    }
    
    [HarmonyPatch(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.OnSlotSelected))]
    public class CardStatBoostSequencer_OnSlotSelected
    {
	    public static bool Prefix(CardStatBoostSequencer __instance, MainInputInteractable slot)
	    {
		    if (!Configs.FlameOverrideEnabled || !CardStatBoostSequencer_RemoveSequence.m_IsCancelButtonShowing)
		    {
			    return true;
		    }
			 
            __instance.confirmStone.SelectionConfirmed = false;
		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    Debug.Assert(confirmStoneButton, "Confirm stone button not created yet!");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Exit();
		    return true;
	    }
    }
    
    [HarmonyPatch(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.OnSelectionEnded))]
    public class CardStatBoostSequencer_OnSelectionEnded
    {
	    public static bool Prefix(CardStatBoostSequencer __instance)
	    {
		    if (!Configs.FlameOverrideEnabled || !CardStatBoostSequencer_RemoveSequence.m_IsCancelButtonShowing)
		    {
			    return true;
		    }
    
            // Selecting a new card must always require a fresh press of the buff button.
            __instance.confirmStone.SelectionConfirmed = false;
		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    Debug.Assert(confirmStoneButton, "Confirm stone button not created yet!");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Enter();
		    return true;
	    }
    }
}
