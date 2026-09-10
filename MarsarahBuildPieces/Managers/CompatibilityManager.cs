using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MarsarahBuildPieces.Patches.BuildPieces;

namespace MarsarahBuildPieces.Managers
{
	internal static class CompatibilityManager
	{
		private static readonly LogManager log = new LogManager("Compatibility Manager", LogManager.LogLevel.Info);

		private const string TweaksGuid = "Marsarah.MarsarahTweaks";

		private static ConfigEntry<bool> tweaksBuildPieceAmounts;
		private static ConfigEntry<bool> tweaksPermanentLights;
		private static ConfigEntry<bool> tweaksBrighterLanterns;

		public static bool TweaksLoaded { get; private set; }

		public static bool BuildPieceAmountsEnabled => TweaksLoaded && tweaksBuildPieceAmounts?.Value == true;
		public static bool PermanentLightsEnabled => TweaksLoaded && tweaksPermanentLights?.Value == true;
		public static bool BrighterLanternsEnabled => TweaksLoaded && tweaksBrighterLanterns?.Value == true;

		public static void Initialize()
		{
			if (!Chainloader.PluginInfos.TryGetValue(TweaksGuid, out var pluginInfo) || pluginInfo.Instance == null)
				return;

			TweaksLoaded = true;

			ConfigFile tweaksConfig = pluginInfo.Instance.Config;

			tweaksBuildPieceAmounts = GetBoolConfig(tweaksConfig, "2 - Grind Reduction (Synced with Server)", "4 - Cheaper Build Pieces Amounts");
			tweaksPermanentLights = GetBoolConfig(tweaksConfig, "5 - QOL (Synced with Server)", "13 - Permanent Lights");
			tweaksBrighterLanterns = GetBoolConfig(tweaksConfig, "4 - Features (Synced with Server)", "10 - Brighter Lanterns");

			if (tweaksBuildPieceAmounts != null)
				tweaksBuildPieceAmounts.SettingChanged += (_, __) => RefreshLightRequirements();

			if (tweaksPermanentLights != null)
				tweaksPermanentLights.SettingChanged += (_, __) => RefreshLightRequirements();

			if (tweaksBrighterLanterns != null)
				tweaksBrighterLanterns.SettingChanged += (_, __) => RefreshLanternIntensity();

			log.Info("MarsarahTweaks detected. Compatibility enabled.");
		}

		private static ConfigEntry<bool> GetBoolConfig(ConfigFile config, string section, string key)
		{
			ConfigDefinition definition = new ConfigDefinition(section, key);

			if (config.TryGetEntry(definition, out ConfigEntry<bool> entry))
				return entry;

			log.Warn($"Could not find MarsarahTweaks config '{section} / {key}'.");
			return null;
		}

		private static void RefreshLightRequirements()
		{
			if (ZNetScene.instance == null)
				return;

			SilverSconce.RefreshSilverSconceRequirements();
			GreenStandingBrazier.RefreshGreenBrazierRequirements();
			SilverHangingBrazier.RefreshSilverHangingBrazierRequirements();
			ColoredDvergerLanterns.RefreshColoredDvergrLanternsRequirements();

			log.Info("Refreshed custom light requirements from MarsarahTweaks settings.");
		}

		private static void RefreshLanternIntensity()
		{
			if (ZNetScene.instance == null)
				return;

			ColoredDvergerLanterns.UpdateColoredDvergrLanternsIntensity();

			log.Info("Refreshed colored Dvergr lantern intensity from MarsarahTweaks settings.");
		}
	}
}