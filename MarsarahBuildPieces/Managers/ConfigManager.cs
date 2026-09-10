using BepInEx;
using BepInEx.Configuration;
using ServerSync;
using System.IO;

namespace MarsarahBuildPieces.Managers
{
	public static class ConfigManager
	{
		private static readonly LogManager log = new LogManager("Config Manager", LogManager.LogLevel.Info);

		private static ConfigFile Config;
		private static readonly ConfigSync configSync = new ConfigSync(MarsarahBuildPieces.ModGUID)
		{
			DisplayName = MarsarahBuildPieces.ModName,
			CurrentVersion = MarsarahBuildPieces.ModVersion,
			MinimumRequiredVersion = MarsarahBuildPieces.ModVersion
		};

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

			public ConfigMetadata(string name, string description)
			{
				Name = name;
				Description = description;
			}
		}

		public static class Configs
		{
			public static readonly ConfigMetadata ServerConfig = new ConfigMetadata("Lock Configuration", "If on, only server admins can change the configuration.");

			public static readonly ConfigMetadata PocketPortal = new ConfigMetadata(
				"01 - Pocket Portal",
				"Adds a new portal that is built from a Portal Core that only takes one inventory slot, which can be crafted at a Workbench starting with the Mountain biome. Can only build one Pocket Portal per player. (Toggling mid-game requires reloading the build/crafting menu)");

			public static readonly ConfigMetadata GlacialStonePortal = new ConfigMetadata(
				"02 - Glacial Stone Portal",
				"Enables the unused stone portal and adds it to the build menu. Works like a normal portal and is not to be confused with the Stone Portal from Ashlands. Unlocked at the Mountain biome. (Toggling mid-game requires reloading the build/crafting menu)");

			public static readonly ConfigMetadata MysticalLightWard = new ConfigMetadata(
				"03 - Mystical Light Ward",
				"Adds a new ward starting with the Mountain biome. When built, all light sources in its area will be automatically refueled when reaching 0 fuel. (Toggling mid-game requires reloading the build/crafting menu)");

			public static readonly ConfigMetadata BuildPiecesLighting = new ConfigMetadata(
				"04 - Extra Lights",
				"Adds new light sources including the Silver Sconce, Silver Table Torch, Green Standing Brazier, Silver Hanging Brazier, and Colored Dverger Lanterns, unlocked at the Mountain and Mistlands biomes. (Toggling mid-game requires reloading the build/crafting menu)");
		}

		public static ConfigEntry<bool> ServerConfigLocked;

		public static ConfigEntry<bool> PocketPortalEnabled;
		public static ConfigEntry<bool> GlacialStonePortalEnabled;
		public static ConfigEntry<bool> MysticalLightWardEnabled;
		public static ConfigEntry<bool> BuildPiecesLightingEnabled;

		public static void Init(ConfigFile configFile)
		{
			Config = configFile;

			ServerConfigLocked = CreateConfig(ConfigSections.Main, Configs.ServerConfig.Name, true, Configs.ServerConfig.Description);
			_ = configSync.AddLockingConfigEntry(ServerConfigLocked);

			PocketPortalEnabled = CreateConfig(ConfigSections.BuildPieces, Configs.PocketPortal.Name, true, Configs.PocketPortal.Description);
			GlacialStonePortalEnabled = CreateConfig(ConfigSections.BuildPieces, Configs.GlacialStonePortal.Name, true, Configs.GlacialStonePortal.Description);
			MysticalLightWardEnabled = CreateConfig(ConfigSections.BuildPieces, Configs.MysticalLightWard.Name, true, Configs.MysticalLightWard.Description);
			BuildPiecesLightingEnabled = CreateConfig(ConfigSections.BuildPieces, Configs.BuildPiecesLighting.Name, true, Configs.BuildPiecesLighting.Description);

			SetupWatcher();

			log.Info("Build Pieces configuration initialized.");
		}

		private static ConfigEntry<T> CreateConfig<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true)
		{
			ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, new ConfigDescription(description));

			SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
			syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

			configEntry.SettingChanged += (_, __) => OnConfigChanged(name);

			return configEntry;
		}

		private static void SetupWatcher()
		{
			FileSystemWatcher watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName)
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

			// Piece-specific refresh logic will be added as each feature is ported.
		}
	}
}