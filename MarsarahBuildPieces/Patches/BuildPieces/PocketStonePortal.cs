
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

			GameObject corePrefab = MPrefabManager.ClonePrefab("MoltenCore", "StonePortalCore");
			if (corePrefab == null)
			{
				log.Error("Failed to clone MoltenCore for StonePortalCore.");
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

			Sprite icon = MAssetBundleManager.LoadEmbeddedSprite("Stone_Portal_Core.png");

			if (icon != null)
			{
				itemDrop.m_itemData.m_shared.m_icons = new[] { icon };
				log.Info("Stone Portal Core custom icon assigned.");
			}

			MPrefabManager.RegisterToZNetScene(corePrefab);
			MPrefabManager.RegisterItem(corePrefab);

			CreateStonePortalCoreRecipe();

			log.Info("Stone Portal Core registered and ready.");
		}

		private static void ModifyStonePortalCoreColors(GameObject corePrefab)
		{
			// Match the colors tested in Unity.
			Color baseColor = new Color32(128, 0, 0, 255);    // #800000
			Color emissionColor = new Color(1f, 0f, 0f, 1f);  // #FF0000, HDR intensity 1
			Color lightColor = new Color(1f, 0f, 0f, 1f);

			Dictionary<Material, Material> runtimeMaterials = new Dictionary<Material, Material>();

			int rendererCount = 0;
			int lightCount = 0;

			// Apply to both the dropped world model and the attached model.
			foreach (Renderer renderer in corePrefab.GetComponentsInChildren<Renderer>(true))
			{
				if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
					continue;

				Material[] materials = renderer.sharedMaterials;

				for (int i = 0; i < materials.Length; i++)
				{
					Material originalMaterial = materials[i];

					if (originalMaterial == null)
						continue;

					if (!runtimeMaterials.TryGetValue(originalMaterial, out Material material))
					{
						material = new Material(originalMaterial)
						{
							name = $"StonePortalCore_{originalMaterial.name}"
						};

						// Deep red surface.
						if (material.HasProperty("_Color"))
							material.SetColor("_Color", baseColor);

						// Saturated red glow without excessive HDR brightness.
						if (material.HasProperty("_EmissionColor"))
						{
							material.SetColor("_EmissionColor", emissionColor);
							material.EnableKeyword("_EMISSION");
						}

						runtimeMaterials.Add(originalMaterial, material);

						log.Info($"Stone Portal Core material created | Name={material.name} | Shader={material.shader.name}");
					}

					materials[i] = material;
				}

				renderer.sharedMaterials = materials;
				rendererCount++;
			}

			// Match any associated lighting to the new red material.
			foreach (Light light in corePrefab.GetComponentsInChildren<Light>(true))
			{
				light.color = lightColor;
				lightCount++;
			}

			if (rendererCount == 0)
				log.Warn("Stone Portal Core has no compatible mesh renderers.");

			log.Info($"Stone Portal Core recolored | Renderers={rendererCount} | Materials={runtimeMaterials.Count} | Lights={lightCount}");
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
					new RequirementConfig("FlametalNew", 2),
					new RequirementConfig("Grausten", 10)
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
			piece.m_name = "Stone Pocket Portal";
			piece.m_description = "An Ashlands-infused portable stone portal that can transport metals.";
			piece.m_craftingStation = null;

			Sprite icon = MAssetBundleManager.LoadEmbeddedSprite("Pocket_Stone_Portal.png");

			if (icon != null)
				piece.m_icon = icon;

			// Allow transporting metals and other normally restricted items.
			teleport.m_allowAllItems = true;

			// Apply red target-found color.
			teleport.m_colorTargetfound = PortalEmission;

			// Apply portal effects and custom model.
			RecolorPortalEffects(PocketStonePortalPrefab);
			ConfigurePocketStonePortalEffects(teleport);
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
			visualRoot.transform.localPosition = new Vector3(0f, 0f, 0f);

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

			// Bind the custom Ashlands arch to Valheim's built-in portal emission system.
			TeleportWorld teleport = prefab.GetComponent<TeleportWorld>();
			MeshRenderer archRenderer = visualRoot.transform.Find("AshlandsStoneArch")?.GetComponent<MeshRenderer>();

			if (teleport != null && archRenderer != null)
			{
				teleport.m_model = archRenderer;
				archRenderer.material.EnableKeyword("_EMISSION");

				log.Info($"Pocket Stone Portal emission renderer assigned | Model={archRenderer.name} | Shader={archRenderer.material.shader.name}");
			}
			else
			{
				log.Warn("Could not assign Pocket Stone Portal emission renderer.");
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

			Transform oldColliderRoot = prefab.transform.Find("collider");
			Transform oldVisualRoot = prefab.transform.Find("New");
			Transform teleportRoot = prefab.transform.Find("TELEPORT");

			foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
			{
				if (collider.isTrigger)
					continue;

				// Never disable collision components belonging to the teleport system.
				if (teleportRoot != null && collider.transform.IsChildOf(teleportRoot))
					continue;

				if (collider.GetComponent<TeleportWorldTrigger>() != null ||
					collider.GetComponentInParent<TeleportWorldTrigger>() != null)
					continue;

				// Only disable the old physical collision and visual geometry collision.
				bool oldPhysicalCollider = oldColliderRoot != null &&
					(collider.transform == oldColliderRoot || collider.transform.IsChildOf(oldColliderRoot));

				bool oldVisualCollider = oldVisualRoot != null &&
					(collider.transform == oldVisualRoot || collider.transform.IsChildOf(oldVisualRoot));

				if (!oldPhysicalCollider && !oldVisualCollider)
					continue;

				collider.enabled = false;
				disabled++;

				log.Info($"Disabled original portal collider: {collider.name}");
			}

			// Create one shallow collider directly on the portal root.
			BoxCollider baseCollider = prefab.AddComponent<BoxCollider>();

			baseCollider.center = new Vector3(localTopCenter.x, localTopCenter.y - 0.05f, localTopCenter.z);
			baseCollider.size = new Vector3(width, 0.10f, depth);
			baseCollider.isTrigger = false;

			// Create an interaction collider matching the actual Ashlands stone arch.
			Transform archTransform = visualRoot.Find("AshlandsStoneArch");
			MeshFilter archMesh = archTransform != null ? archTransform.GetComponent<MeshFilter>() : null;

			if (archMesh == null || archMesh.sharedMesh == null)
			{
				log.Warn("Could not create Pocket Stone Portal interaction collider: AshlandsStoneArch mesh not found.");
			}
			else
			{
				int interactionLayer = LayerMask.NameToLayer("piece_nonsolid");

				if (interactionLayer < 0)
				{
					log.Error("Could not find the piece_nonsolid layer for Pocket Stone Portal interaction.");
					return;
				}

				GameObject interactionObject = new GameObject("PocketStonePortalInteraction");
				interactionObject.transform.SetParent(archTransform, false);
				interactionObject.layer = interactionLayer;

				MeshCollider interactionCollider = interactionObject.AddComponent<MeshCollider>();
				interactionCollider.sharedMesh = archMesh.sharedMesh;
				interactionCollider.convex = false;
				interactionCollider.isTrigger = false;

				log.Info($"Pocket Stone Portal mesh interaction collider created | Mesh={archMesh.sharedMesh.name} | Layer={LayerMask.LayerToName(interactionLayer)}");
			}

			log.Info($"Pocket Stone Portal base collider | Center={baseCollider.center} | Size={baseCollider.size} | Original disabled={disabled}");

			LogPortalTriggerSetup(prefab);
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

		private static void ConfigurePocketStonePortalEffects(TeleportWorld teleport)
		{
			// Get the standard vanilla wooden portal.
			GameObject woodPortal = MPrefabManager.GetPrefab("portal_wood");
			TeleportWorld woodTeleport = woodPortal != null ? woodPortal.GetComponent<TeleportWorld>() : null;

			if (woodTeleport == null)
			{
				log.Warn("Could not find vanilla wooden portal TeleportWorld.");
				return;
			}

			// Match the normal portal's activation distance.
			float originalRange = teleport.m_activationRange;
			teleport.m_activationRange = woodTeleport.m_activationRange;

			log.Info($"Pocket Stone Portal activation range | Original={originalRange:F2}m | New={teleport.m_activationRange:F2}m");

			// Restore the standard vanilla portal connection burst and sound.
			if (woodTeleport.m_connected == null ||
				woodTeleport.m_connected.m_effectPrefabs == null ||
				woodTeleport.m_connected.m_effectPrefabs.Length == 0)
			{
				log.Warn("Could not find vanilla wooden portal connection effects.");
				return;
			}

			// Create our own effect list while reusing vanilla effect prefabs.
			teleport.m_connected = new EffectList
			{
				m_effectPrefabs = (EffectData[])woodTeleport.m_connected.m_effectPrefabs.Clone()
			};

			log.Info($"Pocket Stone Portal connected VFX/SFX restored | Effects={teleport.m_connected.m_effectPrefabs.Length}");
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

		private static void LogPortalTriggerSetup(GameObject prefab)
		{
			TeleportWorldTrigger[] triggers = prefab.GetComponentsInChildren<TeleportWorldTrigger>(true);

			log.Info($"Pocket Stone Portal teleport trigger count: {triggers.Length}");

			foreach (TeleportWorldTrigger trigger in triggers)
			{
				Vector3 localPosition = prefab.transform.InverseTransformPoint(trigger.transform.position);

				log.Info($"Teleport trigger | Object={trigger.name} | Enabled={trigger.enabled} | Active={trigger.gameObject.activeSelf} | Position={localPosition}");

				foreach (Collider collider in trigger.GetComponentsInChildren<Collider>(true))
				{
					log.Info($"Teleport collider | Object={collider.name} | Enabled={collider.enabled} | IsTrigger={collider.isTrigger} | Bounds={collider.bounds}");
				}
			}
		}
	}
}
