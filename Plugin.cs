using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnlimitedInscryption.Scripts.Backgrounds;
using UnlimitedInscryption.Scripts.Cards;
using UnlimitedInscryption.Scripts.Sigils;

namespace UnlimitedInscryption
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("cyantist.inscryption.api", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
	    public const string PluginGuid = "jamesgames.inscryption.unlimitedinscryption";
	    public const string PluginName = "Unlimited Inscryption";
	    public const string PluginVersion = "0.4.1.0";

        public static string PluginDirectory;
        public static ManualLogSource Log;
        public static Plugin Instance;

        public bool DuplicateMergeOverrideEnabled => base.Config.Bind<bool>("Mycologist", "Enabled", true, new ConfigDescription("Enables the mods new behaviour to change how the Mycologist works when combining Duplicates when True", null, Array.Empty<object>())).Value;
        public bool CardRemoveOverrideEnabled => base.Config.Bind<bool>("Bone Lord", "Enabled", true, new ConfigDescription("Enables the mods new behaviour to change how the Bone Lord works when True", null, Array.Empty<object>())).Value;
        public bool CardMergeOverrideEnabled => base.Config.Bind<bool>("Card Merge", "Enabled", true, new ConfigDescription("Enables the mods new behaviour to change how the Card Merging  map node works when True", null, Array.Empty<object>())).Value;
        public bool CardMergeAllowMultipleSigilTransfers => base.Config.Bind<bool>("Card Merge", "Allow Multiple Merges", true, new ConfigDescription("Screw restrictions of what can merge into what. Anything goes!", null, Array.Empty<object>())).Value;
        public bool TotemOverrideEnabled => base.Config.Bind<bool>("Wood Carver", "Enabled", true, new ConfigDescription("Enables the mods new behaviour to change how the Wood Carver selection works when True", null, Array.Empty<object>())).Value;
        public bool TotemIncludeUnlearnedAbilities => base.Config.Bind<bool>("Wood Carver", "Unlearned Abilities", true, new ConfigDescription("Include unlearned abilities as totem bottoms", null, Array.Empty<object>())).Value;
        public bool TotemIncludeOverpoweredAbilities => base.Config.Bind<bool>("Wood Carver", "Overpowered Abilities", true, new ConfigDescription("Include abilities with a power level higher than normal", null, Array.Empty<object>())).Value;
        public bool TotemIncludeNonAct1Abilities => base.Config.Bind<bool>("Wood Carver", "Include Non Act1 Abilities", false, new ConfigDescription("Include all abilities not from Act1", null, Array.Empty<object>())).Value;
        public bool FlameOverrideEnabled => base.Config.Bind<bool>("Flame", "Enabled", true, new ConfigDescription("Enables the mods new behaviour to change how the Flame works when True", null, Array.Empty<object>())).Value;
        public float FlameDestroyCardChance => base.Config.Bind<float>("Flame", "Destroy Chance", 22.5f, new ConfigDescription("Flame never destroys your cards when True. Default = 22.5%. 0 = never destroys. 100 = Always destroys", null, Array.Empty<object>())).Value;
        public bool FlameBypassPastRunLimit => base.Config.Bind<bool>("Flame", "Bypass Past Runs Limit", true, new ConfigDescription("Bypass Flames limit requiring at least 4 past runs to burn multiple times", null, Array.Empty<object>())).Value;
        public float FlameExtinguishChance => base.Config.Bind<float>("Flame", "Extinguish Chance", 10.0f, new ConfigDescription("Chance for the Flame to extinguish and burn your card. 0% = never. 100% = Always", null, Array.Empty<object>())).Value;

        public bool FuckSacrificeRestrictionsInstalled { get; private set; }
        public bool FirePitAlwaysAbleToUpgradeInstalled { get; private set; }
        
        private void Awake()
        {
	        Log = Logger;
	        Instance = this;
            Logger.LogInfo($"Loading {PluginName}...");
            PluginDirectory = this.Info.Location.Replace("UnlimitedInscryption.dll", "");
            
            new Harmony(PluginGuid).PatchAll();
            
            // Backgrounds
            BurnedBackground.Initialize();
            
            // Abilities
            DeadAbility.Initialize();
            
            // Cards
            BurnedCard.Initialize();
            
            
            // Initialize Config
            foreach (PropertyInfo propertyInfo in GetType().GetProperties())
            {
	            bool flag = propertyInfo.DeclaringType == typeof(Plugin);
	            if (flag)
	            {
		            propertyInfo.GetValue(this, null);
	            }
            }

            // Check other mods installed
            FuckSacrificeRestrictionsInstalled = DLLExists("FuckSacrificeRestrictions.dll");
            FirePitAlwaysAbleToUpgradeInstalled = DLLExists("FirePitAlwaysAbleToUpgrade.dll");

            Logger.LogInfo($"Loaded {PluginName}!");
        }

        private bool DLLExists(string dllName)
        {
	        string pluginPath = Paths.PluginPath;
	        foreach (string path in Directory.EnumerateFiles(pluginPath, "*dll", SearchOption.AllDirectories))
	        {
		        if (path.EndsWith(dllName))
		        {
			        Plugin.Log.LogInfo($"Found {dllName}!");
			        return true;
		        }
	        }

	        return false;
        }
    }
}
