using System;
using System.Collections;
using DiskCardGame;
using HarmonyLib;
using Pixelplacement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
	[HarmonyPatch(typeof(CardRemoveSequencer), "OnSlotSelected", new System.Type[] { typeof(MainInputInteractable) })]
	public class CardRemoveSequencer_OnSlotSelected
	{
		public static bool Prefix(CardRemoveSequencer __instance, MainInputInteractable slot)
		{
			if (!Plugin.Instance.CardRemoveOverrideEnabled)
			{
				return true;
			}
			
			Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
			ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
			cancelButton.Exit();
			return true;
		}
	}

	[HarmonyPatch(typeof(CardRemoveSequencer), "OnSelectionEnded", new System.Type[] {})]
	public class CardRemoveSequencer_OnSelectionEnded
	{
		public static bool Prefix(CardRemoveSequencer __instance)
		{
			if (!Plugin.Instance.CardRemoveOverrideEnabled)
			{
				return true;
			}

			Transform confirmStoneButton = __instance.transform.Find("CustomCancelButton");
			ConfirmStoneButton cancelButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
			cancelButton.Enter();
			return true;
		}
	}

	[HarmonyPatch(typeof (CardRemoveSequencer), "RemoveSequence", new System.Type[] {})]
    public class CardRemoveSequencer_RemoveSequence
    {
        public static bool Prefix(CardRemoveSequencer __instance, ref IEnumerator __result)
        {
	        if (!Plugin.Instance.CardRemoveOverrideEnabled)
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
            __result = Sequence(__instance, cancelButton);
            return false;
        }

        private static IEnumerator Sequence(CardRemoveSequencer __instance, ConfirmStoneButton cancelButton)
        {
            // Show intro animation and text
            yield return Intro(__instance, cancelButton);
            
            // Let player sacrifice constantly
            Coroutine coroutine = __instance.StartCoroutine(SacrificeCard(__instance, cancelButton));
            
            // Wait for them to press the cancel button though
            yield return cancelButton.WaitUntilConfirmation();
            
            __instance.StopCoroutine(coroutine);

            // Outro
            yield return Outro(__instance, cancelButton);
        }

        private static IEnumerator Outro(CardRemoveSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        ProgressionData.SetMechanicLearned(MechanicsConcept.CardRemoval);
	        yield return __instance.deckPile.DestroyCards(0);
	        __instance.stoneCircleAnim.SetTrigger("exit");
	        cancelButton.Exit();
	        Singleton<ExplorableAreaManager>.Instance.ResetHangingLightsToZoneColors(0.25f);
	        ParticleSystem.EmissionModule emission = __instance.dustParticles.emission;
	        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f, 0f);
	        yield return new WaitForSeconds(0.25f);
	        __instance.confirmStone.Exit();
	        yield return new WaitForSeconds(0.75f);
	        __instance.stoneCircleAnim.gameObject.SetActive(false);
	        __instance.skullEyes.SetActive(false);
	        __instance.confirmStone.SetStoneInactive();
	        RunState.Run.bonelordPuzzleActive = false;
	        Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map, null);
        }
        
        private static IEnumerator Intro(CardRemoveSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        __instance.sacrificeSlot.Disable();
	        Singleton<TableRuleBook>.Instance.SetOnBoard(true);
	        ParticleSystem.EmissionModule dustEmission = __instance.dustParticles.emission;
	        dustEmission.rateOverTime = new ParticleSystem.MinMaxCurve(10f, 10f);
	        Singleton<ViewManager>.Instance.Controller.SwitchToControlMode(ViewController.ControlMode.CardMerging, false);
	        Singleton<ViewManager>.Instance.Controller.LockState = ViewLockState.Locked;
	        yield return new WaitForSeconds(0.3f);
	        __instance.stoneCircleAnim.gameObject.SetActive(true);
	        cancelButton.transform.parent.parent.gameObject.SetActive(true);
	        cancelButton.Enter();
	        yield return new WaitForSeconds(0.5f);
	        if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardRemoval))
	        {
		        yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("You came across some familiar stones. But there was something different...", -2.5f, 0.5f, Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null, true);
	        }
	        yield return __instance.deckPile.SpawnCards(RunState.DeckList.Count, 0.5f);
	        Singleton<ViewManager>.Instance.SwitchToView(View.CardMergeSlots, false, false);
	        Singleton<ExplorableAreaManager>.Instance.TweenHangingLightColors(GameColors.Instance.glowRed, GameColors.Instance.orange, 0.1f);
	        if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardRemoval))
	        {
		        yield return new WaitForSeconds(0.1f);
		        yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("You intuited that the fate of the creature selected for __instance... would not be pleasant.", -0.65f, 0.4f, Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null, true);
	        }
        }

        private static IEnumerator SacrificeCard(CardRemoveSequencer __instance, ConfirmStoneButton cancelButton)
        {
	        while (true)
	        {
		        cancelButton.SetButtonInteractable();
				__instance.sacrificeSlot.RevealAndEnable();
				__instance.sacrificeSlot.ClearDelegates();
				SelectCardFromDeckSlot selectCardFromDeckSlot = __instance.sacrificeSlot;
				selectCardFromDeckSlot.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(selectCardFromDeckSlot.CursorSelectStarted, new Action<MainInputInteractable>(__instance.OnSlotSelected));
				__instance.sacrificeSlot.backOutInputPressed = null;
				SelectCardFromDeckSlot selectCardFromDeckSlot2 = __instance.sacrificeSlot;
				selectCardFromDeckSlot2.backOutInputPressed = (Action)Delegate.Combine(selectCardFromDeckSlot2.backOutInputPressed, new Action(delegate()
				{
					if (__instance.sacrificeSlot.Enabled)
					{
						__instance.OnSlotSelected(__instance.sacrificeSlot);
					}
				}));
				__instance.gamepadGrid.enabled = true;
				yield return __instance.confirmStone.WaitUntilConfirmation();
				cancelButton.Disable();
				__instance.sacrificeSlot.Disable();
				Singleton<RuleBookController>.Instance.SetShown(false, true);
				yield return new WaitForSeconds(0.25f);
				foreach (SpecialCardBehaviour specialCardBehaviour in __instance.sacrificeSlot.Card.GetComponents<SpecialCardBehaviour>())
				{
					yield return specialCardBehaviour.OnSelectedForCardRemoval();
				}
				CardInfo sacrificedInfo = __instance.sacrificeSlot.Card.Info;
				RunState.Run.playerDeck.RemoveCard(sacrificedInfo);
				__instance.sacrificeSlot.Card.Anim.PlayDeathAnimation(false);
				AudioController.Instance.PlaySound3D("sacrifice_default", MixerGroup.TableObjectsSFX, __instance.sacrificeSlot.transform.position, 1f, 0f, null, null, null, null, false);
				yield return new WaitForSeconds(0.5f);
				__instance.sacrificeSlot.DestroyCard();
				if (!sacrificedInfo.HasTrait(Trait.Pelt))
				{
					if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardRemoval))
					{
						yield return Singleton<TextDisplayer>.Instance.ShowUntilInput(string.Format(Localization.Translate("You callously slaughtered the [c:bR]{0}[c:]..."), sacrificedInfo.DisplayedNameLocalized), -0.65f, 0.4f, Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null, true);
					}
				}
				else
				{
					yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("CardRemovePeltChosen1", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
				}
				yield return new WaitForSeconds(0.5f);
				__instance.skullEyes.SetActive(true);
				AudioController.Instance.PlaySound2D("creepy_rattle_lofi", MixerGroup.None, 1f, 0f, null, null, null, null, false);
				yield return new WaitForSeconds(0.5f);
				if (sacrificedInfo.HasTrait(Trait.Pelt))
				{
					yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("CardRemovePeltChosen2", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
				}
				else if (sacrificedInfo.HasTrait(Trait.Goat))
				{
					yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("CardRemoveGoatChosen", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
				}
				else if (!ProgressionData.LearnedMechanic(MechanicsConcept.CardRemoval))
				{
					yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("However, the [c:bR]Bone Lord[c:] was pleased by your sacrifice.", 0f, 0.4f, Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter, DialogueEvent.Speaker.Single, null, true);
				}
				if (!sacrificedInfo.HasTrait(Trait.Pelt))
				{
					SelectableCard boonCard = __instance.SpawnCard(__instance.transform);
					boonCard.gameObject.SetActive(true);
					if (sacrificedInfo.HasTrait(Trait.Goat))
					{
						boonCard.SetInfo(BoonsUtil.CreateCardForBoon(BoonData.Type.StartingBones));
					}
					else
					{
						boonCard.SetInfo(BoonsUtil.CreateCardForBoon(BoonData.Type.MinorStartingBones));
					}
					boonCard.SetEnabled(false);
					__instance.gamepadGrid.Rows[0].interactables.Add(boonCard);
					boonCard.transform.position = __instance.sacrificeSlot.transform.position + Vector3.up * 3f;
					boonCard.Anim.Play("fly_on", 0f);
					__instance.sacrificeSlot.PlaceCardOnSlot(boonCard.transform, 0.5f, 0f, 0f);
					yield return new WaitForSeconds(0.5f);
					boonCard.SetEnabled(true);
					SelectableCard selectableCard = boonCard;
					selectableCard.CursorSelectEnded = (Action<MainInputInteractable>)Delegate.Combine(selectableCard.CursorSelectEnded, new Action<MainInputInteractable>(__instance.OnBoonSelected));
					__instance.boonTaken = false;
					yield return new WaitUntil(() => __instance.boonTaken);
					__instance.gamepadGrid.enabled = false;
					boonCard.Anim.PlayQuickRiffleSound();
					Tween.Position(boonCard.transform, boonCard.transform.position + Vector3.back * 4f + Vector3.up * 0.5f, 0.2f, 0f, Tween.EaseInOut, Tween.LoopType.None, null, null, true);
					Tween.Rotate(boonCard.transform, new Vector3(0f, 90f, 0f), Space.World, 0.2f, 0f, null, Tween.LoopType.None, null, null, true);
					yield return new WaitForSeconds(0.25f);
					Object.Destroy(boonCard.gameObject);
				}
	        }
        }
    }
}