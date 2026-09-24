using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MarsarahBuildPieces.Patches.BuildPieces;
using System.Collections.Generic;

namespace MarsarahBuildPieces.Managers
{
	internal static class CompatibilityManager
	{
		private static readonly LogManager log = new LogManager("Compatibility Manager", LogManager.LogLevel.Warning);

		private const string TweaksGuid = "Marsarah.MarsarahTweaks";

		private static ConfigEntry<bool> tweaksBuildPieceAmounts;
		private static ConfigEntry<bool> tweaksPermanentLights;
		private static ConfigEntry<bool> tweaksBrighterLanterns;

		public static bool TweaksLoaded { get; private set; }

		public static bool BuildPieceAmountsEnabled => TweaksLoaded && tweaksBuildPieceAmounts?.Value == true;
		public static bool PermanentLightsEnabled => TweaksLoaded && tweaksPermanentLights?.Value == true;
		public static bool BrighterLanternsEnabled => TweaksLoaded && tweaksBrighterLanterns?.Value == true;

		private static readonly Dictionary<string, string> LoadedMods = new Dictionary<string, string>();

		public struct ConflictMod
		{
			public string Guid;
			public string Name;
			public bool Loaded;

			public ConflictMod(string guid)
			{
				Guid = guid;
				Name = null;
				Loaded = false;
			}
		}

		public static ConflictMod MultiUserChest = new ConflictMod("com.maxsch.valheim.MultiUserChest");

		public static void Initialize()
		{
			LoadedMods.Clear();

			foreach (var plugin in Chainloader.PluginInfos.Values)
				LoadedMods[plugin.Metadata.GUID] = plugin.Metadata.Name;

			UpdateConflictMod(ref MultiUserChest);

			InitializeMarsarahTweaksCompatibility();
		}

		private static void InitializeMarsarahTweaksCompatibility()
		{
			if (!Chainloader.PluginInfos.TryGetValue(TweaksGuid, out var pluginInfo) || pluginInfo.Instance == null)
				return;

			TweaksLoaded = true;

			ConfigFile tweaksConfig = pluginInfo.Instance.Config;

			tweaksBuildPieceAmounts = GetBoolConfig(tweaksConfig, "2 - Grind Reduction (Synced with Server)", "Cheaper Build Pieces Amounts");
			tweaksPermanentLights = GetBoolConfig(tweaksConfig, "5 - QOL (Synced with Server)", "Permanent Lights");
			tweaksBrighterLanterns = GetBoolConfig(tweaksConfig, "4 - Features (Synced with Server)", "Brighter Lanterns");

			if (tweaksBuildPieceAmounts != null)
				tweaksBuildPieceAmounts.SettingChanged += (_, __) => RefreshLightRequirements();

			if (tweaksPermanentLights != null)
			{
				HandleInitialPermanentLightsConflict();

				tweaksPermanentLights.SettingChanged += (_, __) => OnPermanentLightsChanged();
				ConfigManager.MysticalLightWardEnabled.SettingChanged += (_, __) => OnMysticalLightWardChanged();
			}

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
			ColoredDvergrLanterns.RefreshColoredDvergrLanternsRequirements();

			log.Info("Refreshed custom light requirements from MarsarahTweaks settings.");
		}

		private static void RefreshLanternIntensity()
		{
			if (ZNetScene.instance == null)
				return;

			ColoredDvergrLanterns.UpdateColoredDvergrLanternsIntensity();

			log.Info("Refreshed colored Dvergr lantern intensity from MarsarahTweaks settings.");
		}

		private static void HandleInitialPermanentLightsConflict()
		{
			if (!ConfigManager.MysticalLightWardEnabled.Value || tweaksPermanentLights?.Value != true)
				return;

			log.Warn("MarsarahTweaks Permanent Lights and Mystical Light Ward are both enabled. Disabling Permanent Lights.");

			tweaksPermanentLights.Value = false;
		}

		private static void OnPermanentLightsChanged()
		{
			if (tweaksPermanentLights.Value && ConfigManager.MysticalLightWardEnabled.Value)
			{
				log.Warn("MarsarahTweaks Permanent Lights enabled. Disabling Mystical Light Ward.");

				ConfigManager.MysticalLightWardEnabled.Value = false;
			}

			RefreshLightRequirements();
		}

		private static void OnMysticalLightWardChanged()
		{
			if (!ConfigManager.MysticalLightWardEnabled.Value || tweaksPermanentLights?.Value != true)
				return;

			log.Warn("Mystical Light Ward enabled. Disabling MarsarahTweaks Permanent Lights.");

			tweaksPermanentLights.Value = false;
		}

		private static void UpdateConflictMod(ref ConflictMod mod)
		{
			if (LoadedMods.TryGetValue(mod.Guid, out var name))
			{
				mod.Name = name;
				mod.Loaded = true;
				log.Info($"{name} detected (GUID: {mod.Guid})");
			}
			else
			{
				mod.Name = mod.Guid;
				mod.Loaded = false;
			}
		}

		public static void SetIfIncompatible<T>(ConflictMod mod, ConfigEntry<T> config, T forcedValue, string actionDescription, string additionalReason = "")
		{
			if (!mod.Loaded)
				return;

			if (EqualityComparer<T>.Default.Equals(config.Value, forcedValue))
				return;

			string before = config.Value?.ToString() ?? "null";
			string after = forcedValue?.ToString() ?? "null";

			log.Warn(
				$"{actionDescription} '{config.Definition.Key}' because '{mod.Name}' mod is loaded. " +
				$"(was: {before}, now: {after}) {additionalReason}"
			);

			config.Value = forcedValue;
		}

		public static void DisableIfIncompatible(ConflictMod mod, ConfigEntry<bool> config, string additionalReason = "")
		{
			SetIfIncompatible(
				mod,
				config,
				false,
				"Automatically disabling",
				additionalReason
			);
		}

		public static void UpdateIncompatibilities()
		{
			DisableIfIncompatible(
				MultiUserChest,
				ConfigManager.SmartDropboxEnabled,
				"Smart Dropbox is incompatible with MultiUserChest and can cause inventory desynchronization or item loss."
			);
		}
	}
}