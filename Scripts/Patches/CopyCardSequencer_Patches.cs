using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnlimitedInscryption.Scripts.Patches
{
    [HarmonyPatch(typeof(SpecialNodeHandler), nameof(SpecialNodeHandler.StartSpecialNodeSequence))]
    public class CopyCardSequencer_StartSpecialNodeSequence
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(CopyCardSequencer), nameof(CopyCardSequencer.CopyCardSequence));
            MethodInfo replacement = AccessTools.Method(typeof(CopyCardSequencer_StartSpecialNodeSequence), nameof(CreateSequence));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException("Expected one Goobert sequence call in StartSpecialNodeSequence, found " + replaced);
            }
        }

        public static IEnumerator CreateSequence(CopyCardSequencer sequencer)
        {
            return Configs.CopyCardOverrideEnabled ? RepeatSequence(sequencer) : sequencer.CopyCardSequence();
        }

        private static IEnumerator RepeatSequence(CopyCardSequencer sequencer)
        {
            Transform template = sequencer.transform.Find("ConfirmStoneButton");
            if (template == null)
            {
                throw new InvalidOperationException("Goobert's ConfirmStoneButton template was not found.");
            }

            GameObject repeatObject = null;
            GameObject exitObject = null;
            try
            {
                repeatObject = Object.Instantiate(template.gameObject, template.parent);
                exitObject = Object.Instantiate(template.gameObject, template.parent);
                ConfirmStoneButton repeatButton = PrepareButton(repeatObject, "CustomCopyRepeatButton", -3f);
                ConfirmStoneButton exitButton = PrepareButton(exitObject, "CustomCopyExitButton", 3f);
                MeshRenderer icon = exitObject.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetComponent<MeshRenderer>();
                icon.material.mainTexture = Utils.GetTextureFromPath("Artwork/close_button.png");

                Plugin.Log.LogInfo("[Goobert] Starting repeatable paintings.");
                while (true)
                {
                    // Run the game's selection, painting, reward and cleanup for each attempt.
                    // Only its final map transition is deferred until the player chooses Exit.
                    IEnumerator painting = sequencer.CopyCardSequence();
                    try
                    {
                        while (AdvancePainting(painting))
                        {
                            yield return painting.Current;
                        }
                    }
                    finally
                    {
                        (painting as IDisposable)?.Dispose();
                    }

                    yield return new WaitUntil(() => !sequencer.pile.DoingCardOperation);
                    sequencer.selectionSlot.ClearDelegates();
                    sequencer.selectionSlot.Disable();
                    sequencer.confirmStone.ClearDelegates();
                    sequencer.confirmStone.SetStoneInactive();
                    Singleton<TextDisplayer>.Instance.ShowMessage("Paint another card? Or leave?",
                        Emotion.Neutral, TextDisplayer.LetterAnimation.WavyJitter);

                    bool canRepeat = RunState.DeckList.Count > 0;
                    if (canRepeat)
                    {
                        repeatButton.Enter();
                    }
                    exitButton.Enter();
                    Coroutine repeatWait = canRepeat ? sequencer.StartCoroutine(repeatButton.WaitUntilConfirmation()) : null;
                    Coroutine exitWait = sequencer.StartCoroutine(exitButton.WaitUntilConfirmation());
                    bool leave;
                    try
                    {
                        yield return new WaitUntil(() => exitButton.SelectionConfirmed ||
                            (canRepeat && repeatButton.SelectionConfirmed));
                        leave = exitButton.SelectionConfirmed;
                    }
                    finally
                    {
                        if (repeatWait != null)
                        {
                            sequencer.StopCoroutine(repeatWait);
                        }
                        sequencer.StopCoroutine(exitWait);
                        repeatButton.ClearDelegates();
                        exitButton.ClearDelegates();
                        repeatButton.SetStoneInactive();
                        exitButton.SetStoneInactive();
                        Singleton<TextDisplayer>.Instance.Clear();
                    }

                    if (leave)
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (repeatObject != null) Object.Destroy(repeatObject);
                if (exitObject != null) Object.Destroy(exitObject);
            }

            if (Singleton<GameFlowManager>.Instance != null)
            {
                Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map);
            }
        }

        private static ConfirmStoneButton PrepareButton(GameObject clone, string name, float x)
        {
            clone.name = name;
            clone.transform.localPosition = new Vector3(x, 5f, -1.3f);
            clone.SetActive(true);
            clone.transform.GetChild(0).gameObject.SetActive(true);
            ConfirmStoneButton button = clone.GetComponentInChildren<ConfirmStoneButton>(true);
            button.confirmView = View.Default;
            button.ClearDelegates();
            button.SetStoneInactive();
            return button;
        }

        // Scope transition suppression to synchronous MoveNext calls, never to time spent waiting for input.
        [ThreadStatic] internal static bool AdvancingPainting;

        private static bool AdvancePainting(IEnumerator painting)
        {
            bool previous = AdvancingPainting;
            AdvancingPainting = true;
            try
            {
                return painting.MoveNext();
            }
            finally
            {
                AdvancingPainting = previous;
            }
        }
    }

    [HarmonyPatch]
    public class CopyCardSequencer_FinishPainting
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(CopyCardSequencer), nameof(CopyCardSequencer.CopyCardSequence)));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(GameFlowManager), nameof(GameFlowManager.TransitionToGameState));
            MethodInfo replacement = AccessTools.Method(typeof(CopyCardSequencer_FinishPainting), nameof(TransitionAfterPainting));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException("Expected one Goobert map transition, found " + replaced);
            }
        }

        public static void TransitionAfterPainting(GameFlowManager manager, GameState state, NodeData nodeData)
        {
            if (state == GameState.Map && CopyCardSequencer_StartSpecialNodeSequence.AdvancingPainting)
            {
                return;
            }
            manager.TransitionToGameState(state, nodeData);
        }
    }
}
