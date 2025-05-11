using System;
using System.Collections;
using System.Reflection;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
	[HarmonyPatch]
    public class CardStatBoostSequencer_RemoveSequence
    {
	    internal static bool m_IsCancelButtonShowing = false;
	    private static MethodBase TargetMethod()
	    {
		    MethodBase baseMethod = AccessTools.Method(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.StatBoostSequence));
		    return AccessTools.EnumeratorMoveNext(baseMethod);
	    }
	    
	    [HarmonyPrefix]
        public static bool CardStatBoostSequencer_StatBoostSequence()
        {
	        if (!Configs.FlameOverrideEnabled)
	        {
		        Plugin.Log.LogError("FlameOverrideEnabled is false, returning original enumerator");
		        return true;
	        }

	        var __instance = GameObject.FindObjectOfType<CardStatBoostSequencer>();
	        Transform instanceTransform = __instance.transform;
	        Transform confirmStoneButton = instanceTransform.Find("CustomCancelButton");
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
            else
            {
	            Plugin.Log.LogError("Cancel button found, using existing one");
            }

            ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
            Debug.Assert(cancelButton, "cancelButton is null but the gameObject is not");

            m_IsCancelButtonShowing = false;
            __instance.StartCoroutine(Sequence(__instance, cancelButton));
            return false;
        }

        private static IEnumerator Sequence(CardStatBoostSequencer __instance, ConfirmStoneButton cancelButton)
        {	        
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

			// cancelButton.Unpress();
			yield return __instance.confirmStone.WaitUntilConfirmation();
			// cancelButton.Disable();
			//
			// END OF INTRO
			//
			
			//
			// Buff card once and reloop
			//
			bool finishedBuffing = false;
			int numBuffsGiven = 0;
			while (!finishedBuffing)
			{
				Plugin.Log.LogError("Loop index: " + numBuffsGiven);
				numBuffsGiven++;
				__instance.selectionSlot.Disable();
				Singleton<RuleBookController>.Instance.SetShown(false);
				yield return new WaitForSeconds(0.25f);
				AudioController.Instance.PlaySound3D("card_blessing", MixerGroup.TableObjectsSFX, __instance.selectionSlot.transform.position);
				__instance.selectionSlot.Card.Anim.PlayTransformAnimation();
				__instance.ApplyModToCard(__instance.selectionSlot.Card.Info, attackMod);
				yield return new WaitForSeconds(0.15f);
				__instance.selectionSlot.Card.SetInfo(__instance.selectionSlot.Card.Info);
				__instance.selectionSlot.Card.SetInteractionEnabled(false);
				// __instance.selectionSlot.Card.SetInteractionEnabled(true);
				yield return new WaitForSeconds(0.75f);
				if (SaveManager.SaveFile.pastRuns.Count >= 4 || SaveFile.IsAscension)
				{
					if (!RunState.Run.survivorsDead)
					{
						yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostPushLuck" + numBuffsGiven, TextDisplayer.MessageAdvanceMode.Input);
						yield return new WaitForSeconds(0.1f);
						switch (numBuffsGiven)
						{
						case 1:
							Singleton<TextDisplayer>.Instance.ShowMessage("Push your luck? Or pull away?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter);
							break;
						case 2:
							Singleton<TextDisplayer>.Instance.ShowMessage("Push your luck further? Or run back?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter);
							break;
						default:
							Singleton<TextDisplayer>.Instance.ShowMessage("Recklessly continue?", Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter);
							break;
						}
					}
					
					RechooseCard:
					__instance.selectionSlot.RevealAndEnable();
					__instance.selectionSlot.Card.SetInteractionEnabled(true);
					// bool cancelledByClickingCard = false;
					// __instance.retrieveCardInteractable.gameObject.SetActive(true);
					// __instance.retrieveCardInteractable.CursorSelectEnded = null;
					// GenericMainInputInteractable genericMainInputInteractable = __instance.retrieveCardInteractable;
					// genericMainInputInteractable.CursorSelectEnded = (Action<MainInputInteractable>)Delegate.Combine(genericMainInputInteractable.CursorSelectEnded, (Action<MainInputInteractable>)delegate
					// {
					// 	cancelledByClickingCard = true;
					// });
					if (__instance.selectionSlot.Card != null)
					{
						__instance.confirmStone.Unpress();
					}


					if (!m_IsCancelButtonShowing)
					{
						m_IsCancelButtonShowing = true;
						cancelButton.Enter();
					}
					else
					{
						cancelButton.Unpress();
					}

					__instance.StartCoroutine(__instance.confirmStone.WaitUntilConfirmation());
					__instance.StartCoroutine(cancelButton.WaitUntilConfirmation());
					yield return new WaitUntil(() =>
					{
						return __instance.confirmStone.SelectionConfirmed || cancelButton.SelectionConfirmed ||
						       InputButtons.GetButton(Button.Cancel);
					});
					Singleton<TextDisplayer>.Instance.Clear();
					__instance.retrieveCardInteractable.gameObject.SetActive(false);
					__instance.confirmStone.Disable();
					cancelButton.Disable();
					yield return new WaitForSeconds(0.1f);
					if (cancelButton.SelectionConfirmed || InputButtons.GetButton(Button.Cancel))
					{
						finishedBuffing = true;
					}
					else if (__instance.confirmStone.SelectionConfirmed)
					{
						if (!RunState.Run.survivorsDead && !AlwaysSucceedFire())
						{
							float num = SaveFile.IsAscension ? 0.5f : 1f - (Configs.FlameDestroyCardChance / 100);
							if (SeededRandom.Value(SaveManager.SaveFile.GetCurrentRandomSeed() + numBuffsGiven) > num)
							{
								// Destroy card
								if (__instance.selectionSlot.Card.Info.HasTrait(Trait.KillsSurvivors) ||
								    __instance.selectionSlot.Card.Info.HasAbility(Ability.Deathtouch))
								{
									killSurvivors = true;
								}

								Plugin.Log.LogError("Destroying card");
								__instance.selectionSlot.Card.Anim.PlayDeathAnimation();
								RunState.Run.playerDeck.RemoveCard(__instance.selectionSlot.Card.Info);
								yield return new WaitForSeconds(1f);

								Plugin.Log.LogError("Playing text");
								yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostCardEaten",
									TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait,
									new string[1] { __instance.selectionSlot.Card.Info.DisplayedNameLocalized });
								yield return new WaitForSeconds(0.1f);

								Plugin.Log.LogError("Destroying card");
								__instance.selectionSlot.DestroyCard();


								if (RunState.Run.consumables.Count < RunState.Run.MaxConsumables)
								{
									Plugin.Log.LogError("Giving an item");
									yield return new WaitForSeconds(0.4f);
									Singleton<ViewManager>.Instance.SwitchToView(View.Consumables);
									yield return new WaitForSeconds(0.2f);
									RunState.Run.consumables.Add("PiggyBank");
									Singleton<ItemsManager>.Instance.UpdateItems();
									yield return new WaitForSeconds(0.5f);
									yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent(
										"StatBoostCardEatenBones", TextDisplayer.MessageAdvanceMode.Input);
									__instance.selectionSlot.FlyOffCard();
								}

								numBuffsGiven++;
								Plugin.Log.LogError("Checking if card is still in deck: " + __instance.GetValidCards(attackMod).Count);
								if (__instance.GetValidCards(attackMod).Count > 1)
									goto RechooseCard;
							}
						}
					}

					Plugin.Log.LogError("Checking if card is still in deck: " + __instance.GetValidCards(attackMod).Count);
					if (__instance.GetValidCards(attackMod).Count == 1)
					{
						finishedBuffing = true;
					}
				}
				else
				{
					finishedBuffing = true;
				}
			}
			
			// if (!RunState.Run.survivorsDead)
			// {
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("StatBoostOutro", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, new string[2]
				{
					__instance.GetTranslatedStatText(attackMod),
					__instance.selectionSlot.Card.Info.DisplayedNameLocalized
				});
			// }
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

        private static bool AlwaysSucceedFire()
        {
	        if (RunState.Run.survivorsDead)
	        {
		        return true;
	        }

	        if (Configs.FlameDestroyCardChance <= 0)
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
    
    [HarmonyPatch(typeof(CardStatBoostSequencer), nameof(CardStatBoostSequencer.OnSlotSelected))]
    public class CardStatBoostSequencer_OnSlotSelected
    {
	    public static bool Prefix(CardStatBoostSequencer __instance, MainInputInteractable slot)
	    {
		    if (!Configs.FlameOverrideEnabled || !CardStatBoostSequencer_RemoveSequence.m_IsCancelButtonShowing)
		    {
			    return true;
		    }
			 
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
    
		    Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
		    Debug.Assert(confirmStoneButton, "Confirm stone button not created yet!");
		    ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		    cancelButton.Enter();
		    return true;
	    }
    }
}