using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace MarsarahBuildPieces.Managers
{
	internal static class MPrefabManager
	{
		private static readonly LogManager log = new LogManager("M Prefab Manager", LogManager.LogLevel.Warning);

		public static GameObject GetPrefab(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				log.Error("GetPrefab: Given prefab name is null or empty.");
				return null;
			}

			return PrefabManager.Instance.GetPrefab(name);
		}

		public static GameObject ClonePrefab(string nameOfOriginal, string nameOfClone)
		{
			if (string.IsNullOrWhiteSpace(nameOfOriginal) || string.IsNullOrWhiteSpace(nameOfClone))
			{
				log.Warn("Given strings for cloning are null or empty. Cannot clone prefab.");
				return null;
			}

			if (GetPrefab(nameOfClone) != null)
			{
				log.Warn($"A prefab named {nameOfClone} already exists in ZNetScene. Skipping clone.");
				return null;
			}

			GameObject originalPrefab = GetPrefab(nameOfOriginal);
			if (originalPrefab == null)
			{
				log.Error($"Original prefab {nameOfOriginal} not found.");
				return null;
			}

			return ClonePrefab(originalPrefab, nameOfClone);
		}

		public static GameObject ClonePrefab(GameObject originalPrefab, string nameOfClone)
		{
			if (originalPrefab == null || string.IsNullOrWhiteSpace(nameOfClone))
			{
				log.Warn("Null original or empty clone name.");
				return null;
			}

			if (GetPrefab(nameOfClone) != null)
			{
				log.Warn($"A prefab named {nameOfClone} already exists in ZNetScene. Skipping clone.");
				return null;
			}

			return PrefabManager.Instance.CreateClonedPrefab(nameOfClone, originalPrefab);
		}

		public static void RegisterToZNetScene(GameObject prefab)
		{
			if (prefab == null)
			{
				log.Error("Tried to register null prefab.");
				return;
			}

			ZNetScene znetScene = ZNetScene.instance;
			if (znetScene == null)
			{
				CustomPrefab customPrefab = new CustomPrefab(prefab, fixReference: true);
				PrefabManager.Instance.AddPrefab(customPrefab);

				log.Info($"Queued prefab '{prefab.name}' for registration.");
				return;
			}

			if (znetScene.GetPrefab(prefab.name) != null)
			{
				log.Warn($"Prefab '{prefab.name}' already registered in ZNetScene.");
				return;
			}

			PrefabManager.Instance.RegisterToZNetScene(prefab);

			if (GetPrefab(prefab.name) == null)
			{
				log.Error($"Failed to register prefab '{prefab.name}'.");
			}
			else
			{
				log.Info($"Registered prefab '{prefab.name}' to ZNetScene.");
			}
		}

		public static void RegisterItem(GameObject prefab)
		{
			if (prefab == null)
			{
				log.Error("Tried to register null item prefab.");
				return;
			}

			CustomItem customItem = new CustomItem(prefab, fixReference: true);
			ItemManager.Instance.AddItem(customItem);

			log.Info($"Registered item '{prefab.name}'.");
		}

		public static Recipe RegisterRecipe(RecipeConfig recipe)
		{
			if (recipe == null)
			{
				log.Error("Tried to register null recipe.");
				return null;
			}

			CustomRecipe customRecipe = new CustomRecipe(recipe);
			ItemManager.Instance.AddRecipe(customRecipe);

			log.Info($"Registered recipe for '{recipe.Item}'.");

			return customRecipe.Recipe;
		}

		public static void AddToBuildMenu(GameObject prefab, PieceConfig pieceConfig)
		{
			if (prefab == null)
			{
				log.Warn("Given prefab is null. Cannot add to build menu.");
				return;
			}

			if (pieceConfig == null)
			{
				log.Warn("Given piece config is null. Cannot add to build menu.");
				return;
			}

			CustomPiece customPiece = new CustomPiece(prefab, fixReference: true, pieceConfig);
			PieceManager.Instance.AddPiece(customPiece);

			log.Info($"Added piece '{prefab.name}' to the build menu.");
		}
	}
}