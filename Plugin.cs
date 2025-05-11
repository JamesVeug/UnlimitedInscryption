using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

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

        public bool FuckSacrificeRestrictionsInstalled { get; private set; }
        public bool FirePitAlwaysAbleToUpgradeInstalled { get; private set; }

        private void Awake()
        {
	        Log = Logger;
	        Instance = this;
            Logger.LogInfo($"Loading {PluginName}...");
            PluginDirectory = this.Info.Location.Replace("UnlimitedInscryption.dll", "");

            Harmony harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            
            // Initialize Config
            Configs.Init();

            Logger.LogInfo($"Loaded {PluginName}!");
        }
    }
}
