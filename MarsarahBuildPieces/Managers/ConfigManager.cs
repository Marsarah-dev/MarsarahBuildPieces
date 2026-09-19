using BepInEx;
using BepInEx.Configuration;
using ServerSync;
using System.IO;
using MarsarahBuildPieces.Patches.BuildPieces;

namespace MarsarahBuildPieces.Managers
{
	public static class ConfigManager
	{
		private static readonly LogManager log = new LogManager("Config Manager", LogManager.LogLevel.Warning);

		private static ConfigFile Config;
		private static readonly ConfigSync configSync = new ConfigSync(MarsarahBuildPieces.ModGUID)
		{
			DisplayName = MarsarahBuildPieces.ModName,
			CurrentVersion = MarsarahBuildPieces.ModVersion,
			MinimumRequiredVersion = MarsarahBuildPieces.ModVersion
		};
		private static FileSystemWatcher watcher;

		private static string ConfigFileName => MarsarahBuildPieces.ModGUID + ".cfg";
		private static string ConfigFileFullPath => Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

		public static class ConfigSections
		{
			public const string Main = "1 - Main";
			public const string BuildPieces = "2 - Build Pieces (Synced with Server)";
		}

		public struct ConfigMetadata
		{
			public string Name;
			public string Description;
			public string Section;
			public int Order;

			public ConfigMetadata(string name, string description, string section, int order)
			{
				Name = name;
				Description = description;
				Section = section;
				Order = order;
			}
		}

		private sealed class ConfigurationManagerAttributes
		{
			public int? Order;
		}

		public static class Configs
		{
			public static readonly ConfigMetadata ServerConfig = new ConfigMetadata(
				"Lock Configuration",
				"If on, only server admins can change the configuration.",
				ConfigSections.Main,
				10);

			public static readonly ConfigMetadata SmartDropbox = new ConfigMetadata(
				"Smart Dropbox",
				"Adds a Smart Dropbox that automatically distributes deposited items to nearby storage chests that already contain the same item type after the Dropbox is closed. (Toggling mid-game requires reloading the build menu)",
				ConfigSections.BuildPieces,
				80);

			public static readonly ConfigMetadata SmartDropboxRadius = new ConfigMetadata(
				"Smart Dropbox Radius",
				"Sets the storage search radius of the Smart Dropbox in meters.",
				ConfigSections.BuildPieces,
				70);

			public static readonly ConfigMetadata PocketPortal = new ConfigMetadata(
				"Pocket Portal",
				"Adds a new portal that is built from a Portal Core that only takes one inventory slot, which can be crafted at a Workbench starting with the Mountain biome. Can only build one Pocket Portal per player. (Toggling mid-game requires reloading the build/crafting menu)",
				ConfigSections.BuildPieces,
				60);

			public static readonly ConfigMetadata GlacialStonePortal = new ConfigMetadata(
				"Glacial Stone Portal",
				"Enables the unused stone portal and adds it to the build menu. Works like a normal portal and is not to be confused with the Stone Portal from Ashlands. Unlocked at the Mountain biome. (Toggling mid-game requires reloading the build/crafting menu)",
				ConfigSections.BuildPieces,
				50);

			public static readonly ConfigMetadata BuildPiecesLighting = new ConfigMetadata(
				"Extra Lights",
				"Adds new light sources including the Silver Sconce, Green Standing Brazier, Silver Hanging Brazier, and Colored Dverger Lanterns, unlocked at the Mountain and Mistlands biomes. (Toggling mid-game requires reloading the build/crafting menu)",
				ConfigSections.BuildPieces,
				40);

			public static readonly ConfigMetadata MysticalLightWard = new ConfigMetadata(
				"Mystical Light Ward",
				"Adds a small ward starting with the Mountain biome. When built, it keeps fueled light sources within its radius permanently lit. (Toggling mid-game requires reloading the build/crafting menu)",
				ConfigSections.BuildPieces,
				30);

			public static readonly ConfigMetadata MysticalLightWardRadius = new ConfigMetadata(
				"Mystical Light Ward Radius",
				"Sets the effect radius of the Mystical Light Ward in meters.",
				ConfigSections.BuildPieces,
				20);

			public static readonly ConfigMetadata SmallSign = new ConfigMetadata(
				"Small Sign", 
				"Adds a smaller version of the wooden sign to the build menu. (Toggling mid-game requires reloading the build/crafting menu)", 
				ConfigSections.BuildPieces, 
				10);
		}

		public static ConfigEntry<bool> ServerConfigLocked;

		public static ConfigEntry<bool> SmartDropboxEnabled;
		public static ConfigEntry<int> SmartDropboxRadius;
		public static ConfigEntry<bool> PocketPortalEnabled;
		public static ConfigEntry<bool> GlacialStonePortalEnabled;
		public static ConfigEntry<bool> BuildPiecesLightingEnabled;
		public static ConfigEntry<bool> MysticalLightWardEnabled;
		public static ConfigEntry<int> MysticalLightWardRadius;
		public static ConfigEntry<bool> SmallSignEnabled;

		public static void Init(ConfigFile configFile)
		{
			Config = configFile;

			ServerConfigLocked = CreateConfig(Configs.ServerConfig, true);
			_ = configSync.AddLockingConfigEntry(ServerConfigLocked);

			SmartDropboxEnabled = CreateConfig(Configs.SmartDropbox, true);
			SmartDropboxRadius = CreateConfig(Configs.SmartDropboxRadius, 20, true, new AcceptableValueRange<int>(5, 50));
			PocketPortalEnabled = CreateConfig(Configs.PocketPortal, true);
			GlacialStonePortalEnabled = CreateConfig(Configs.GlacialStonePortal, true);
			BuildPiecesLightingEnabled = CreateConfig(Configs.BuildPiecesLighting, true);
			MysticalLightWardEnabled = CreateConfig(Configs.MysticalLightWard, true);
			MysticalLightWardRadius = CreateConfig(Configs.MysticalLightWardRadius, 32, true, new AcceptableValueRange<int>(5, 50));
			SmallSignEnabled = CreateConfig(Configs.SmallSign, true);

			SetupWatcher();

			log.Info("Build Pieces configuration initialized.");
		}

		private static ConfigEntry<T> CreateConfig<T>(ConfigMetadata metadata, T defaultValue, bool synchronizedSetting = true, AcceptableValueBase acceptableValues = null)
		{
			ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes
			{
				Order = metadata.Order
			};

			ConfigEntry<T> configEntry = Config.Bind(metadata.Section, metadata.Name, defaultValue, new ConfigDescription(metadata.Description, acceptableValues, attributes));

			SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
			syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

			configEntry.SettingChanged += (_, __) => OnConfigChanged(metadata.Name);

			return configEntry;
		}

		private static void SetupWatcher()
		{
			watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName)
			{
				IncludeSubdirectories = true,
				SynchronizingObject = ThreadingHelper.SynchronizingObject,
				EnableRaisingEvents = true
			};

			watcher.Changed += ReadConfigValues;
			watcher.Created += ReadConfigValues;
			watcher.Renamed += ReadConfigValues;
		}

		private static void ReadConfigValues(object sender, FileSystemEventArgs e)
		{
			if (!File.Exists(ConfigFileFullPath))
				return;

			try
			{
				Config.Reload();
			}
			catch
			{
				log.Error($"There was an issue loading {ConfigFileName}");
			}
		}

		private static void OnConfigChanged(string configName)
		{
			log.Info($"Config setting '{configName}' changed.");

			Config.Save();

			if (ObjectDB.instance == null || ZNetScene.instance == null || ZNet.instance == null)
				return;

			if (ZNet.instance.IsDedicated())
				return;

			switch (configName)
			{
				case var name when name == Configs.SmartDropbox.Name:
					SmartDropbox.ToggleVisibility();
					break;

				case var name when name == Configs.SmartDropboxRadius.Name:
					SmartDropbox.RefreshRadius();
					break;

				case var name when name == Configs.PocketPortal.Name:
					PocketPortal.TogglePocketPortalVisibility();
					PocketPortal.TogglePortalCoreVisibility();
					break;

				case var name when name == Configs.GlacialStonePortal.Name:
					GlacialStonePortal.TogglePortalVisibility();
					break;

				case var name when name == Configs.BuildPiecesLighting.Name:
					SilverSconce.ToggleVisibility();
					GreenStandingBrazier.ToggleVisibility();
					SilverHangingBrazier.ToggleVisibility();
					ColoredDvergrLanterns.ToggleVisibility();
					break;

				case var name when name == Configs.MysticalLightWard.Name:
					MysticalLightWard.ToggleVisibility();
					break;

				case var name when name == Configs.MysticalLightWardRadius.Name:
					MysticalLightWard.RefreshRadius();
					break;

				case var name when name == Configs.SmallSign.Name:
					SmallSign.ToggleVisibility();
					break;
			}
		}
	}
}