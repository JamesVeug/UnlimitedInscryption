using System;
using System.Collections;
using System.Collections.Generic;
using APIPlugin;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
	[HarmonyPatch(typeof(BuildTotemSequencer), "Start", new System.Type[] { })]
	public class BuildTotemSequencer_Start
	{
		public static void Postfix(BuildTotemSequencer __instance)
		{
			if (!Plugin.Instance.TotemOverrideEnabled)
			{
				return;
			}

			List<SelectableItemSlot> slots = __instance.inventorySlots;
			int count = slots.Count;
			for (int i = 0; i < count; i++)
			{
				SelectableItemSlot template = __instance.inventorySlots[i];
				SelectableItemSlot clone = GameObject.Instantiate(template, template.transform.parent);

				Vector3 currentPos = clone.transform.localPosition;
				currentPos.y += 1.5f;
				clone.transform.localPosition = currentPos;

				slots.Add(clone);
			}

			for (int i = 0; i < count; i++)
			{
				SelectableItemSlot template = __instance.inventorySlots[i];
				SelectableItemSlot clone = GameObject.Instantiate(template, template.transform.parent);

				Vector3 currentPos = clone.transform.localPosition;
				currentPos.y += 3f;
				clone.transform.localPosition = currentPos;

				slots.Add(clone);
			}
			
			Plugin.Log.LogInfo("[BuildTotemSequencer_Start] slots: " + __instance.inventorySlots.Count);
			
			List<ItemData> list = new List<ItemData>();
			List<ItemData> list2 = new List<ItemData>();
			foreach (Tribe trait in RunState.Run.totemTops)
			{
				list.Add(new TotemTopData(trait));
			}
			foreach (Ability ability in RunState.Run.totemBottoms)
			{
				list2.Add(new TotemBottomData(ability));
			}
			List<ItemData> inventory = TotemsUtil.AlternateTopsAndBottoms(list, list2);
			Plugin.Log.LogInfo("[BuildTotemSequencer_Start] inventory: " + inventory.Count);
		}
	}

	[HarmonyPatch(typeof (BuildTotemSequencer), "NewPiecePhase", new System.Type[] {typeof(BuildTotemNodeData)})]
    public class BuildTotemSequencer_NewPiecePhase
    {
        public static bool Prefix(BuildTotemSequencer __instance, BuildTotemNodeData nodeData, ref IEnumerator __result)
        {
	        if (!Plugin.Instance.TotemOverrideEnabled)
	        {
		        return true;
	        }
	        
	        //Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase] Starting");
	        Transform confirmStoneButton = __instance.transform.Find("CustomSelectButton");
	        if (confirmStoneButton == null)
	        {
		        confirmStoneButton = GameObject.Find("ConfirmStoneButton").transform;
		        GameObject clone = Object.Instantiate(confirmStoneButton.gameObject, confirmStoneButton.parent);
		        clone.name = "CustomSelectButton";
		        clone.transform.GetChild(0).gameObject.SetActive(true);
		        clone.transform.localPosition = new Vector3(0, 5, -3.9f);

		        // Assign new icon
		        Transform quad = clone.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0);
		        MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
		        Material[] materials = quadRenderer.materials;
		        materials[0].mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");
		        quadRenderer.materials = materials;

		        confirmStoneButton = clone.transform;
		        ConfirmStoneButton tempSelectButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
		        tempSelectButton.SetPrivateFieldValue("confirmView", View.Default);
	        }

	        ConfirmStoneButton selectButton = confirmStoneButton.GetComponentInChildren<ConfirmStoneButton>(true);
	        selectButton.transform.parent.parent.gameObject.SetActive(true);
	        
            __result = Sequence(__instance, nodeData, selectButton);
            return false;
        }

        private static IEnumerator Sequence(BuildTotemSequencer __instance, BuildTotemNodeData nodeData, ConfirmStoneButton selectButton)
        {
	        //Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Starting");
	        Singleton<ViewManager>.Instance.Controller.SwitchToControlMode(ViewController.ControlMode.TotemPieceSelection, false);

	        int seed = SaveManager.SaveFile.GetCurrentRandomSeed();
			List<ItemData> totemChoices = __instance.GenerateTotemChoices(nodeData, seed); // Contains ALL totems
			//Plugin.Log.LogInfo($"[BuildTotemSequencer_NewPiecePhase][Sequence] Got {totemChoices.Count} totems to fill with");
			
			yield return new WaitForSeconds(0.25f);

			List<bool> finishedSlots = new List<bool>() { false, false };

			void ClickTotemPieceCallback(MainInputInteractable i)
			{
				//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Clicked totem");
				SelectableItemSlot itemSlot = i as SelectableItemSlot;
				Item currentItem = itemSlot.Item;

				if (currentItem.Data is TotemTopData)
				{
					RunState.Run.totemTops.Add((currentItem.Data as TotemTopData).prerequisites.tribe);
				}
				else
				{
					RunState.Run.totemBottoms.Add((currentItem.Data as TotemBottomData).effectParams.ability);
				}

				currentItem.PlayExitAnimation();
				currentItem.ShowHighlighted(false, false);
				__instance.CreatePieceInSlot(currentItem.Data, __instance.GetFirstEmptyInventorySlot());

				if (__instance.GetFirstEmptyInventorySlot() == null)
				{
					finishedSlots[0] = true;
				}
				else
				{
					//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Free space");
					if (totemChoices.Count > 0)
					{
						//Plugin.Log.LogInfo($"[BuildTotemSequencer_NewPiecePhase][Sequence] {totemChoices.Count} left");
						__instance.CreatePieceInSlot(totemChoices[0], itemSlot);
						totemChoices.RemoveAt(0);
					}
					else
					{
						//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] No more totems to fill");
						itemSlot.SetEnabled(false);
						int totalEnabledSlots = __instance.slots.FindAll((a) => a.Enabled).Count;
						if (totalEnabledSlots == 0)
						{
							finishedSlots[0] = true;
							//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] All disabled. Force finishing");
						}
					}
				}

				//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Clicked totem done");
			}

			//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Creating totems");
			for (int i = 0; i < __instance.slots.Count && totemChoices.Count > 0; i++)
			{
				__instance.CreatePieceInSlot(totemChoices[0], __instance.slots[i]);
				InitializeSlot(__instance, totemChoices[0], __instance.slots[i], ClickTotemPieceCallback);
				totemChoices.RemoveAt(0);
				
				yield return new WaitForSeconds(0.1f);
			}
			//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Creating totems done");

			yield return new WaitForSeconds(0.5f);
			__instance.SetSlotCollidersEnabled(true);
			
			//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Waiting for select button");
			selectButton.Enter();
			Coroutine coroutine = __instance.StartCoroutine(WaitForSelectButton(selectButton, finishedSlots));
			//Plugin.Log.LogInfo("[BuildTotemSequencer_NewPiecePhase][Sequence] Waiting for select button clicked");
			
			yield return new WaitUntil((() => finishedSlots[0] || finishedSlots[1]));
			if (finishedSlots[0])
			{
				yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("WoodcarverMaxPieces", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
			}
			selectButton.Exit();

			if (coroutine != null)
			{
				__instance.StopCoroutine(coroutine);
			}
			
			Singleton<RuleBookController>.Instance.SetShown(false, true);
			Singleton<ViewManager>.Instance.Controller.LockState = ViewLockState.Locked;

			__instance.DisableSlotsAndExitItems(null); 
			Singleton<OpponentAnimationController>.Instance.ClearLookTarget();
			yield return new WaitForSeconds(0.2f);
			//selectedSlot.Item.PlayExitAnimation();
			yield return new WaitForSeconds(0.15f);
			__instance.SetSlotsActive(false);
        }

        private static IEnumerator WaitForSelectButton(ConfirmStoneButton selectButton, List<bool> finished)
        {
	        yield return selectButton.WaitUntilConfirmation();
	        finished[1] = true;
        }

        private static void InitializeSlot(BuildTotemSequencer __instance, ItemData data, SelectableItemSlot selectableItemSlot, Action<MainInputInteractable> callback)
        {
	        SelectableItemSlot selectableItemSlot2 = selectableItemSlot;
	        Delegate cursorSelectStarted = selectableItemSlot2.CursorSelectStarted;
	        selectableItemSlot2.CursorSelectStarted = (Action<MainInputInteractable>)Delegate.Combine(cursorSelectStarted, callback);
	        SelectableItemSlot selectableItemSlot3 = selectableItemSlot;
	        selectableItemSlot3.CursorEntered = (Action<MainInputInteractable>)Delegate.Combine(selectableItemSlot3.CursorEntered, new Action<MainInputInteractable>(delegate(MainInputInteractable i)
	        {
		        Singleton<OpponentAnimationController>.Instance.SetLookTarget(i.transform, Vector3.up * 2f);
	        }));
        }
    }

    [HarmonyPatch(typeof(SelectItemsSequencer), "DisableSlotsAndExitItems", new System.Type[] { typeof(SelectableItemSlot) })]
    public class SelectItemsSequencer_DisableSlotsAndExitItems
    {
	    public static bool Prefix(SelectItemsSequencer __instance, SelectableItemSlot selectedSlot)
	    {
		    if (!Plugin.Instance.TotemOverrideEnabled)
		    {
			    return true;
		    }

		    if (selectedSlot != null)
		    {
			    selectedSlot.Item.ShowHighlighted(false, false);
		    }

		    __instance.SetSlotCollidersEnabled(false);
		    foreach (SelectableItemSlot selectableItemSlot in __instance.slots)
		    {
			    if (selectableItemSlot != selectedSlot && selectableItemSlot.Item != null && selectableItemSlot.Enabled)
			    {
				    selectableItemSlot.Item.PlayExitAnimation();
			    }
		    }

		    return false;
	    }

    }
    
    [HarmonyPatch(typeof(BuildTotemSequencer), "GenerateTotemChoices", new System.Type[] {typeof(BuildTotemNodeData), typeof(int)})]
    public class BuildTotemSequencer_GenerateTotemChoices
    {
	    public static bool Prefix(BuildTotemSequencer __instance, BuildTotemNodeData nodeData, int randomSeed, ref List<ItemData> __result)
	    {
		    if (!Plugin.Instance.TotemOverrideEnabled)
		    {
			    return true;
		    }

		    //
		    // Tops / Tribes
		    //
		    
		    List<Tribe> tribes = new List<Tribe>
			{
				Tribe.Bird,
				Tribe.Canine,
				Tribe.Hooved,
				Tribe.Insect,
				Tribe.Reptile
			};
			if (StoryEventsData.EventCompleted(StoryEvent.SquirrelHeadDiscovered))
			{
				tribes.Add(Tribe.Squirrel);
			}
			foreach (Tribe item in RunState.Run.totemTops)
			{
				tribes.Remove(item);
			}

			int totalHeads = tribes.Count;
			List<ItemData> heads = new List<ItemData>();
			for (int i = 0; i < totalHeads; i++)
			{
				TotemTopData totemTopData = new TotemTopData();
				totemTopData.prerequisites = new TotemTopData.TriggerCardPrerequisites();
				totemTopData.prerequisites.tribe = tribes[SeededRandom.Range(0, tribes.Count, randomSeed++)];
				tribes.Remove(totemTopData.prerequisites.tribe);
				heads.Add(totemTopData);
			}

			//
			// Bottoms / Abilities
			//

			List<Ability> abilities = new List<Ability>();
			if (Plugin.Instance.TotemIncludeUnlearnedAbilities)
			{
				// Vanilla
				//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Adding Vanilla abilities");
				foreach (object allAbilities in Enum.GetValues(typeof(Ability)))
				{
					Ability ability = (Ability)allAbilities;
					if (ability != Ability.None && ability != Ability.NUM_ABILITIES)
					{
						//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Added Vanilla " + ability.ToString());
						abilities.Add(ability);
					}
				}

				// Custom Abilities
				//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Adding Custom abilities");
				foreach (NewAbility newAbility in NewAbility.abilities)
				{
					if (!abilities.Contains(newAbility.ability))
					{
						//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Added Custom " + newAbility.id);
						abilities.Add(newAbility.ability);
					}
				}
			}
			else
			{
				abilities.AddRange(ProgressionData.Data.learnedAbilities);
			}
			
			abilities.RemoveAll((Ability x) =>
			{
				if (RunState.Run.totemBottoms.Contains(x)) 
					return true;

				AbilityInfo abilityInfo = AbilitiesUtil.GetInfo(x);
				if (abilityInfo == null)
				{
					return true;
				}
				
				if (!Plugin.Instance.TotemIncludeOverpoweredAbilities)
				{
					if (abilityInfo.powerLevel > 7 || abilityInfo.powerLevel < 0)
						return true;
				}

				if (!Plugin.Instance.TotemIncludeNonAct1Abilities)
				{
					if (!abilityInfo.metaCategories.Contains(AbilityMetaCategory.Part1Modular))
						return true;
				}

				return false;
			});
			
			//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Making bottoms");
			int totalBottoms = abilities.Count;
			List<ItemData> bottoms = new List<ItemData>();
			while(bottoms.Count < totalBottoms)
			{
				int minPowerLevel = int.MaxValue;
				int maxPowerLevel = int.MinValue;
				for (int i = 0; i < abilities.Count; i++)
				{
					int powerLevel = AbilitiesUtil.GetInfo(abilities[i]).powerLevel;
					minPowerLevel = Mathf.Min(minPowerLevel, powerLevel);
					maxPowerLevel = Mathf.Max(maxPowerLevel, powerLevel);
				}
				
				int powerLevelRoll = SeededRandom.Range(minPowerLevel, maxPowerLevel, randomSeed++);
				List<Ability> list5 = abilities.FindAll((Ability x) => AbilitiesUtil.GetInfo(x).powerLevel <= powerLevelRoll);
				if (list5.Count > 0)
				{
					TotemBottomData totemBottomData = new TotemBottomData();
					totemBottomData.effect = TotemEffect.CardGainAbility;
					totemBottomData.effectParams = new TotemBottomData.EffectParameters();
					totemBottomData.effectParams.ability = list5[SeededRandom.Range(0, list5.Count, randomSeed++)];
					abilities.Remove(totemBottomData.effectParams.ability);
					bottoms.Add(totemBottomData);
				}
				else
				{
					//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] No abilities for power level roll " + powerLevelRoll);
				}
			}
			//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Making bottoms done");
			
			__result = TotemsUtil.AlternateTopsAndBottoms(heads, bottoms);
			//Plugin.Log.LogInfo("[BuildTotemSequencer_GenerateTotemChoices] Done");
			return false;
	    }
    }
}