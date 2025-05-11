using System;
using BepInEx.Configuration;
using UnityEngine;

namespace UnlimitedInscryption
{
    public static class Configs
    {
        public static bool DuplicateMergeOverrideEnabled =
            Plugin.Instance.Config.Bind("Mycologist", "Enabled", true,
                new ConfigDescription(
                    "Enables the mods new behaviour to change how the Mycologist works when combining Duplicates when True")).Value;

        public static bool CardRemoveOverrideEnabled =
            Plugin.Instance.Config.Bind("Bone Lord", "Enabled", true,
                new ConfigDescription("Enables the mods new behaviour to change how the Bone Lord works when True")).Value;

        public static bool CardMergeOverrideEnabled =
            Plugin.Instance.Config.Bind("Card Merge", "Enabled", true,
                new ConfigDescription(
                    "Enables the mods new behaviour to change how the Card Merging  map node works when True")).Value;

        public static bool CardMergeAllowMultipleSigilTransfers =
            Plugin.Instance.Config.Bind("Card Merge", "Allow Multiple Merges", true,
                new ConfigDescription("Screw restrictions of what can merge into what. Anything goes!")).Value;

        public static bool TotemOverrideEnabled =
            Plugin.Instance.Config.Bind("Wood Carver", "Enabled", true,
                new ConfigDescription(
                    "Enables the mods new behaviour to change how the Wood Carver selection works when True")).Value;

        public static bool TotemIncludeUnlearnedAbilities =
            Plugin.Instance.Config.Bind("Wood Carver", "Unlearned Abilities", true,
                    new ConfigDescription("Include unlearned abilities as totem bottoms"))
                .Value;

        public static bool TotemIncludeOverpoweredAbilities =
            Plugin.Instance.Config.Bind("Wood Carver", "Overpowered Abilities", true,
                new ConfigDescription("Include abilities with a power level higher than normal")).Value;

        public static bool TotemIncludeNonAct1Abilities =
            Plugin.Instance.Config.Bind("Wood Carver", "Include Non Act1 Abilities", false,
                new ConfigDescription("Include all abilities not from Act1")).Value;

        public static bool FlameOverrideEnabled =
            Plugin.Instance.Config.Bind("Flame", "Enabled", true,
                new ConfigDescription("Enables the mods new behaviour to change how the Flame works when True")).Value;

        public static float FlameDestroyCardChance =
            Mathf.Clamp(Plugin.Instance.Config.Bind("Flame", "Destroy Chance", 22.5f,
                new ConfigDescription(
                    "Flame never destroys your cards when True. Default = 22.5%. 0 = never destroys. 100 = Always destroys")).Value, 0f, 100f);

        public static void Init()
        {
            // Call to Assign everything on startup
        }
    }
}