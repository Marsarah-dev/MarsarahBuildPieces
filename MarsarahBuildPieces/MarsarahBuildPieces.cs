using BepInEx;
using HarmonyLib;
using MarsarahBuildPieces.Managers;

namespace MarsarahBuildPieces
{
	[BepInPlugin(ModGUID, ModName, ModVersion)]
	[BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)]
	[BepInDependency("Marsarah.MarsarahTweaks", BepInDependency.DependencyFlags.SoftDependency)]
	public class MarsarahBuildPieces : BaseUnityPlugin
	{
		internal const string ModName = "MarsarahBuildPieces";
		internal const string ModVersion = "1.1.0";
		internal const string Author = "Marsarah";
		public const string ModGUID = Author + "." + ModName;

		private readonly Harmony harmony = new Harmony(ModGUID);
		private static readonly LogManager log = new LogManager(nameof(MarsarahBuildPieces), LogManager.LogLevel.Warning);

		private void Awake()
		{
			LogManager.SetGlobalLogLevel(LogManager.LogLevel.Warning);

			ConfigManager.Init(Config);

			log.Info($"{ModName} {ModVersion} loaded.");

			harmony.PatchAll();
		}

		private void Start()
		{
			CompatibilityManager.Initialize();
			CompatibilityManager.UpdateIncompatibilities();
		}

		private void OnDestroy()
		{
			Config.Save();
		}
	}
}