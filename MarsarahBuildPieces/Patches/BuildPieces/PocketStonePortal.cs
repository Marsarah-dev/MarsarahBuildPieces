
using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System;
using UnityEngine;
using static EffectList;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	public static class PocketStonePortal
	{
		private static readonly LogManager log = new LogManager("Pocket Stone Portal", LogManager.LogLevel.Info);

		public const string PortalPrefabName = "pocket_portal_stone";
		private const string ConnectedEffectPrefabName = "fx_pocket_stone_portal_connected";

		private static readonly Color PortalRed = new Color(1f, 0.12f, 0.025f, 1f);
		private static readonly Color PortalEmission = new Color(3f, 0.3f, 0.06f, 1f);

		private static bool initialized;
		private static Recipe StonePortalCoreRecipe;
		private static GameObject PocketStonePortalPrefab;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateStonePortalCore();
				CreatePocketStonePortal();
			}
		}

		// Stone Portal Core

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

		// Pocket Stone Portal

		private static void CreatePocketStonePortal()
		{
			if (MPrefabManager.GetPrefab(PortalPrefabName) != null)
			{
				log.Warn($"Prefab '{PortalPrefabName}' already exists. Skipping creation.");
				return;
			}

			PocketStonePortalPrefab = MPrefabManager.ClonePrefab("portal", PortalPrefabName);
			if (PocketStonePortalPrefab == null)
			{
				log.Error("Failed to clone the unused 'portal' prefab.");
				return;
			}

			ZNetView znet = PocketStonePortalPrefab.GetComponent<ZNetView>();
			if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Solid;
				znet.m_syncInitialScale = false;
			}
			else
			{
				log.Warn("Pocket Stone Portal prefab has no ZNetView.");
			}

			Piece piece = PocketStonePortalPrefab.GetComponent<Piece>();
			TeleportWorld teleport = PocketStonePortalPrefab.GetComponent<TeleportWorld>();

			if (piece == null || teleport == null)
			{
				log.Error("Pocket Stone Portal prefab is missing Piece or TeleportWorld.");
				return;
			}

			piece.m_enabled = true;
			piece.m_name = "Pocket Stone Portal";
			piece.m_description = "An Ashlands-infused portable stone portal that can transport metals. Only one pocket portal of either type may be placed per player.";
			piece.m_craftingStation = null;

			// Allow transporting metals and other normally restricted items.
			teleport.m_allowAllItems = true;

			// Apply red target-found color.
			teleport.m_colorTargetfound = PortalEmission;

			// Apply visual and sound changes.
			RecolorPortalEffects(PocketStonePortalPrefab);
			ApplyConnectedEffects(teleport);

			// Register prefab and build piece.
			MPrefabManager.RegisterToZNetScene(PocketStonePortalPrefab);

			BuildPieceController.ConfigurePiece(PocketStonePortalPrefab, "Misc", new[]
			{
				BuildPieceController.MakeRequirement("StonePortalCore", 1)
			});

			PocketStonePortalPrefab.SetActive(true);

			log.Info($"Registered '{PortalPrefabName}' with metal transport enabled.");
		}

		private static void ApplyConnectedEffects(TeleportWorld teleport)
		{
			GameObject connectedEffect = MPrefabManager.ClonePrefab("fx_portal_connected", ConnectedEffectPrefabName);

			if (connectedEffect == null)
			{
				log.Error("Failed to clone 'fx_portal_connected' for the Pocket Stone Portal.");
				return;
			}

			RecolorPortalEffects(connectedEffect);
			MPrefabManager.RegisterToZNetScene(connectedEffect);

			teleport.m_connected = new EffectList
			{
				m_effectPrefabs = new[]
				{
					new EffectData
					{
						m_prefab = connectedEffect,
						m_enabled = true
					}
				}
			};

			log.Info("Pocket Stone Portal connected effects set to red-tinted vanilla portal effects.");
		}

		private static void RecolorPortalEffects(GameObject root)
		{
			// Recolor particle systems.
			foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
			{
				var main = particle.main;
				main.startColor = PortalRed;
			}

			// Recolor lights.
			foreach (Light light in root.GetComponentsInChildren<Light>(true))
			{
				light.color = PortalRed;
			}

			// Recolor portal-related materials without modifying shared vanilla materials.
			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
			{
				bool isParticle = renderer is ParticleSystemRenderer;

				Material[] originalMaterials = renderer.sharedMaterials;
				Material[] replacementMaterials = new Material[originalMaterials.Length];

				bool updated = false;

				for (int i = 0; i < originalMaterials.Length; i++)
				{
					Material original = originalMaterials[i];

					if (original == null)
					{
						replacementMaterials[i] = null;
						continue;
					}

					bool isPortalMaterial = original.name.IndexOf("portal", StringComparison.OrdinalIgnoreCase) >= 0;

					if (!isParticle && !isPortalMaterial)
					{
						replacementMaterials[i] = original;
						continue;
					}

					Material material = new Material(original);

					if (material.HasProperty("_Color"))
						material.SetColor("_Color", PortalRed);

					if (material.HasProperty("_EmissionColor"))
					{
						material.EnableKeyword("_EMISSION");
						material.SetColor("_EmissionColor", PortalEmission);
					}

					replacementMaterials[i] = material;
					updated = true;
				}

				if (updated)
					renderer.sharedMaterials = replacementMaterials;
			}
		}

		public static bool TogglePortalVisibility()
		{
			if (ConfigManager.PocketStonePortalEnabled == null)
				return false;

			return BuildPieceController.TogglePiece(PocketStonePortalPrefab?.GetComponent<Piece>(), ConfigManager.PocketStonePortalEnabled.Value, log);
		}

		// Early registration for portal hash recognition.
		[HarmonyPatch(typeof(Game), "Awake")]
		public static class EarlyPortalPrefabRegister
		{
			static void Prefix(Game __instance)
			{
				int hash = PortalPrefabName.GetStableHashCode();

				if (!__instance.PortalPrefabHash.Contains(hash))
				{
					__instance.PortalPrefabHash.Add(hash);
					log.Info($"Registered {PortalPrefabName} prefab hash early: {hash}");
				}
			}
		}

		// Register the portal in the game's portal connection system.
		[HarmonyPatch(typeof(Game), nameof(Game.ConnectPortals))]
		public static class Game_ConnectPortals_Patch
		{
			static void Prefix(Game __instance)
			{
				if (PocketStonePortalPrefab == null)
					return;

				if (!__instance.m_portalPrefabs.Contains(PocketStonePortalPrefab))
				{
					__instance.m_portalPrefabs.Add(PocketStonePortalPrefab);
					log.Info($"Registered '{PortalPrefabName}' in Game.m_portalPrefabs.");
				}

				int hash = PortalPrefabName.GetStableHashCode();

				if (!__instance.PortalPrefabHash.Contains(hash))
					__instance.PortalPrefabHash.Add(hash);
			}
		}
	}
}
