using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	public static class PocketStonePortal
	{
		private static readonly LogManager log = new LogManager("Pocket Stone Portal", LogManager.LogLevel.Info);

		private static bool initialized;
		private static Recipe StonePortalCoreRecipe;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;
				CreateStonePortalCore();
			}
		}

		private static void CreateStonePortalCore()
		{
			if (MPrefabManager.GetPrefab("StonePortalCore") != null)
			{
				log.Info("StonePortalCore prefab already exists. Skipping creation.");
				return;
			}

			GameObject corePrefab = MPrefabManager.ClonePrefab("SurtlingCore", "StonePortalCore");
			if (corePrefab == null)
			{
				log.Error("Failed to clone SurtlingCore for StonePortalCore.");
				return;
			}

			ItemDrop itemDrop = corePrefab.GetComponent<ItemDrop>();
			if (itemDrop == null || itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null)
			{
				log.Error("StonePortalCore is missing valid ItemDrop data.");
				return;
			}

			ModifyStonePortalCoreColors(corePrefab);

			itemDrop.m_itemData.m_shared.m_name = "Stone Portal Core";
			itemDrop.m_itemData.m_shared.m_description = "An Ashlands-infused Portal Core used to build a portable stone portal.";
			itemDrop.m_itemData.m_shared.m_maxStackSize = 1;
			itemDrop.m_itemData.m_shared.m_weight = 10f;

			// SurtlingCore already has a red inventory icon, so retain it.
			MPrefabManager.RegisterToZNetScene(corePrefab);
			MPrefabManager.RegisterItem(corePrefab);

			CreateStonePortalCoreRecipe();
			log.Info("Stone Portal Core registered and ready.");
		}

		private static void ModifyStonePortalCoreColors(GameObject corePrefab)
		{
			Transform coreTransform = corePrefab.transform.Find("attach/core");
			Renderer coreRenderer = coreTransform != null ? coreTransform.GetComponent<Renderer>() : null;

			if (coreRenderer != null && coreRenderer.sharedMaterial != null)
			{
				Material coreMaterial = new Material(coreRenderer.sharedMaterial);
				coreMaterial.EnableKeyword("_EMISSION");
				coreMaterial.SetColor("_EmissionColor", new Color(3f, 0.25f, 0.05f));
				coreRenderer.sharedMaterial = coreMaterial;
			}
			else
			{
				log.Warn("StonePortalCore: missing renderer or material at 'attach/core'.");
			}

			Transform lightTransform = corePrefab.transform.Find("attach/Point light");
			Light pointLight = lightTransform != null ? lightTransform.GetComponent<Light>() : null;

			if (pointLight != null)
			{
				pointLight.color = new Color(1f, 0.2f, 0.05f);
				pointLight.intensity = 2.5f;
				pointLight.range = 3f;
			}
			else
			{
				log.Warn("StonePortalCore: missing light at 'attach/Point light'.");
			}
		}

		private static void CreateStonePortalCoreRecipe()
		{
			RecipeConfig recipeConfig = new RecipeConfig
			{
				Item = "StonePortalCore",
				Amount = 1,
				CraftingStation = "blackforge",
				MinStationLevel = 1,
				Requirements = new[]
				{
					new RequirementConfig("PortalCore", 1),
					new RequirementConfig("MoltenCore", 2),
					new RequirementConfig("FlametalNew", 5)
				}
			};

			StonePortalCoreRecipe = MPrefabManager.RegisterRecipe(recipeConfig);
			if (StonePortalCoreRecipe == null)
			{
				log.Error("Failed to register Stone Portal Core recipe.");
				return;
			}

			ToggleStonePortalCoreVisibility();
		}

		public static void ToggleStonePortalCoreVisibility()
		{
			if (StonePortalCoreRecipe == null)
			{
				log.Warn("Stone Portal Core recipe does not exist. No toggle made.");
				return;
			}

			if (ConfigManager.PocketStonePortalEnabled == null)
			{
				log.Warn("Pocket Stone Portal config is not initialized. No toggle made.");
				return;
			}

			StonePortalCoreRecipe.m_enabled = ConfigManager.PocketStonePortalEnabled.Value;
			log.Info($"Stone Portal Core recipe enabled: {StonePortalCoreRecipe.m_enabled}.");
		}
	}
}
