
using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System;
using UnityEngine;
using System.Collections.Generic;
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

			// Apply portal effects and custom model.
			RecolorPortalEffects(PocketStonePortalPrefab);
			ApplyPocketStonePortalVisuals(teleport);
			ApplyPocketStonePortalModel(PocketStonePortalPrefab);

			// Register prefab and build piece.
			MPrefabManager.RegisterToZNetScene(PocketStonePortalPrefab);

			BuildPieceController.ConfigurePiece(PocketStonePortalPrefab, "Misc", new[]
			{
				BuildPieceController.MakeRequirement("StonePortalCore", 1)
			});

			PocketStonePortalPrefab.SetActive(true);

			log.Info($"Registered '{PortalPrefabName}' with metal transport enabled.");
		}
		private static void ApplyPocketStonePortalVisuals(TeleportWorld teleport)
		{
			GameObject vanillaWoodPortal = MPrefabManager.GetPrefab("portal_wood");
			if (vanillaWoodPortal == null)
			{
				log.Error("Vanilla wooden portal prefab not found.");
				return;
			}

			// Get the original wooden portal's target-found effect.
			Transform sourceEffect = vanillaWoodPortal.transform.Find("_target_found_red");
			if (sourceEffect == null)
			{
				log.Warn("Could not find '_target_found_red' in vanilla wooden portal.");
				return;
			}

			// Clone the complete effect hierarchy, including its child components.
			GameObject newEffect = UnityEngine.Object.Instantiate(sourceEffect.gameObject, PocketStonePortalPrefab.transform, false);
			newEffect.name = "_target_found_red";

			EffectFade effectFade = newEffect.GetComponent<EffectFade>();
			if (effectFade == null)
			{
				log.Error("Cloned target-found effect has no EffectFade component.");
				UnityEngine.Object.DestroyImmediate(newEffect);
				return;
			}

			// Recolor only the cloned effect's particles and lights.
			RecolorPortalEffects(newEffect);

			// Remove the original stone portal's target-found effect.
			Transform oldEffect = PocketStonePortalPrefab.transform.Find("_target_found");
			if (oldEffect != null)
				UnityEngine.Object.DestroyImmediate(oldEffect.gameObject);

			// Assign the new effect to our portal.
			teleport.m_target_found = effectFade;
			teleport.m_colorTargetfound = PortalEmission;

			log.Info("Applied wooden portal target-found effect hierarchy to Pocket Stone Portal.");
		}

		private static void ApplyPocketStonePortalModel(GameObject prefab)
		{
			GameObject visualPrefab = MAssetBundleManager.LoadPrefab("PocketStonePortalVisual");
			if (visualPrefab == null)
			{
				log.Error("Could not load Pocket Stone Portal visual prefab.");
				return;
			}

			Transform originalVisuals = prefab.transform.Find("New");
			if (originalVisuals == null)
			{
				log.Error("Could not find original Pocket Stone Portal visual hierarchy.");
				return;
			}

			Shader pieceShader = null;

			// Get the shader directly from the original vanilla portal.
			foreach (MeshRenderer originalRenderer in originalVisuals.GetComponentsInChildren<MeshRenderer>(true))
			{
				Material originalMaterial = originalRenderer.sharedMaterial;

				if (originalMaterial == null || originalMaterial.shader == null)
					continue;

				pieceShader = originalMaterial.shader;
				log.Info($"Using original portal shader: {pieceShader.name}");
				break;
			}

			if (pieceShader == null)
			{
				log.Error("Could not resolve the original portal's runtime shader. Keeping vanilla model.");
				return;
			}

			MeshFilter[] sourceMeshes = visualPrefab.GetComponentsInChildren<MeshFilter>(true);
			if (sourceMeshes.Length == 0)
			{
				log.Error("Pocket Stone Portal visual prefab contains no meshes.");
				return;
			}

			// Validate the source prefab before modifying the original portal.
			foreach (MeshFilter sourceMesh in sourceMeshes)
			{
				MeshRenderer sourceRenderer = sourceMesh.GetComponent<MeshRenderer>();

				if (sourceMesh.sharedMesh == null || sourceRenderer == null)
				{
					log.Error($"Invalid visual mesh or renderer: '{sourceMesh.name}'.");
					return;
				}

				foreach (Material material in sourceRenderer.sharedMaterials)
				{
					if (material == null)
					{
						log.Error($"Missing material on visual mesh '{sourceMesh.name}'.");
						return;
					}
				}
			}

			// Capture existing geometry before creating new visual objects.
			MeshFilter[] originalMeshes = originalVisuals.GetComponentsInChildren<MeshFilter>(true);

			GameObject visualRoot = new GameObject("PocketStonePortalVisual");
			visualRoot.transform.SetParent(originalVisuals, false);
			visualRoot.transform.localPosition = new Vector3(0f, -0.07f, 0f);

			Dictionary<Material, Material> runtimeMaterials = new Dictionary<Material, Material>();

			int added = 0;

			foreach (MeshFilter sourceMesh in sourceMeshes)
			{
				MeshRenderer sourceRenderer = sourceMesh.GetComponent<MeshRenderer>();

				GameObject part = new GameObject(sourceMesh.name);
				part.transform.SetParent(visualRoot.transform, false);

				part.transform.localPosition = sourceMesh.transform.localPosition;
				part.transform.localRotation = sourceMesh.transform.localRotation;
				part.transform.localScale = sourceMesh.transform.localScale;

				MeshFilter meshFilter = part.AddComponent<MeshFilter>();
				meshFilter.sharedMesh = sourceMesh.sharedMesh;

				MeshRenderer renderer = part.AddComponent<MeshRenderer>();
				Material[] sourceMaterials = sourceRenderer.sharedMaterials;
				Material[] materials = new Material[sourceMaterials.Length];

				for (int i = 0; i < sourceMaterials.Length; i++)
				{
					Material sourceMaterial = sourceMaterials[i];

					if (!runtimeMaterials.TryGetValue(sourceMaterial, out Material runtimeMaterial))
					{
						runtimeMaterial = new Material(pieceShader)
						{
							name = $"PocketStonePortal_{sourceMaterial.name}"
						};

						runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
						runtimeMaterial.shader = pieceShader;

						if (runtimeMaterial.HasProperty("_BumpMap") && runtimeMaterial.GetTexture("_BumpMap") != null)
							runtimeMaterial.EnableKeyword("_NORMALMAP");

						if (runtimeMaterial.HasProperty("_EmissionColor") && runtimeMaterial.GetColor("_EmissionColor").maxColorComponent > 0f)
							runtimeMaterial.EnableKeyword("_EMISSION");

						runtimeMaterials.Add(sourceMaterial, runtimeMaterial);
					}

					materials[i] = runtimeMaterial;
				}

				renderer.sharedMaterials = materials;

				added++;
				log.Info($"Added Pocket Stone Portal visual part '{part.name}'.");
			}

			// Clear only original visual meshes, just as Smart Dropbox does.
			foreach (MeshFilter originalMesh in originalMeshes)
				originalMesh.sharedMesh = null;

			// Match collision to the centered model.
			FitPocketStonePortalCollider(prefab, visualRoot.transform);

			log.Info($"Applied Pocket Stone Portal custom visual | Meshes={added} | Materials={runtimeMaterials.Count}");
		}

		private static void FitPocketStonePortalCollider(GameObject prefab, Transform visualRoot)
		{
			Transform baseTransform = visualRoot.Find("OriginalAshlandsBase");
			Renderer baseRenderer = baseTransform != null ? baseTransform.GetComponent<Renderer>() : null;

			if (baseRenderer == null)
			{
				log.Error("Cannot fit Pocket Stone Portal collider: base renderer not found.");
				return;
			}

			// Renderer bounds already include the mesh hierarchy's transforms.
			Bounds worldBounds = baseRenderer.bounds;

			// Convert the top-center of the base into portal-root coordinates.
			Vector3 worldTopCenter = new Vector3(worldBounds.center.x, worldBounds.max.y, worldBounds.center.z);
			Vector3 localTopCenter = prefab.transform.InverseTransformPoint(worldTopCenter);

			Vector3 rootScale = prefab.transform.lossyScale;

			float width = worldBounds.size.x / Mathf.Abs(rootScale.x);
			float depth = worldBounds.size.z / Mathf.Abs(rootScale.z);

			// Disable original solid colliders, preserving teleport triggers.
			int disabled = 0;

			foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
			{
				if (collider.isTrigger)
					continue;

				collider.enabled = false;
				disabled++;
			}

			// Create one shallow collider directly on the portal root.
			BoxCollider baseCollider = prefab.AddComponent<BoxCollider>();

			baseCollider.center = new Vector3(localTopCenter.x, localTopCenter.y - 0.05f, localTopCenter.z);
			baseCollider.size = new Vector3(width, 0.10f, depth);
			baseCollider.isTrigger = false;

			log.Info($"Pocket Stone Portal base collider | Center={baseCollider.center} | Size={baseCollider.size} | Original disabled={disabled}");
		}

		private static void RecolorPortalEffects(GameObject root)
		{
			// Recolor particle effects only.
			foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
			{
				var main = particle.main;
				main.startColor = PortalRed;
			}

			// Recolor effect lighting.
			foreach (Light light in root.GetComponentsInChildren<Light>(true))
			{
				light.color = PortalRed;
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
