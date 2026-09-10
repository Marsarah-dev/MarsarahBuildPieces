using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal class SilverSconce
	{
		private static readonly LogManager log = new LogManager("Silver Sconce", LogManager.LogLevel.Warning);

		private static bool initialized = false;
		private static GameObject SilverSconcePrefab;
		private static GameObject SilverSconcePrefabBlue;
		private static GameObject SilverSconcePrefabGreen;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateSilverSconce();
			}
		}

		private static void CreateSilverSconce()
		{
			if (MPrefabManager.GetPrefab("piece_walltorch_silver") != null && MPrefabManager.GetPrefab("piece_walltorch_silver_blue") != null && MPrefabManager.GetPrefab("piece_walltorch_silver_green") != null) return;

			// Clone Bronze Sconce
			SilverSconcePrefab = CloneBronzeSconcePrefab("piece_walltorch", "piece_walltorch_silver");
			if (SilverSconcePrefab == null) return;
			SilverSconcePrefabBlue = CloneBronzeSconcePrefab("piece_walltorch", "piece_walltorch_silver_blue");
			if (SilverSconcePrefabBlue == null) return;
			SilverSconcePrefabGreen = CloneBronzeSconcePrefab("piece_walltorch", "piece_walltorch_silver_green");
			if (SilverSconcePrefabGreen == null) return;

			// Apply sconce-specific data
			SetupSilverSconceDefaults(SilverSconcePrefab, "Silver Sconce", "forge");
			SetupSilverSconceDefaults(SilverSconcePrefabBlue, "Blue-burning Silver Sconce", "forge");
			SetupSilverSconceDefaults(SilverSconcePrefabGreen, "Green-burning Silver Sconce", "forge");

			// Increase silver sconce size
			ScaleSilverSconce(SilverSconcePrefab);
			ScaleSilverSconce(SilverSconcePrefabBlue);
			ScaleSilverSconce(SilverSconcePrefabGreen);

			// Modify Silver Sconce material
			ModifySilverSconceMaterial(SilverSconcePrefab, "SilverNecklace", "yggashoot_log");
			ModifySilverSconceMaterial(SilverSconcePrefabBlue, "SilverNecklace", "yggashoot_log");
			ModifySilverSconceMaterial(SilverSconcePrefabGreen, "SilverNecklace", "yggashoot_log");

			// Modify the sconce fuel
			ModifySilverSconceFuel(SilverSconcePrefabBlue, "GreydwarfEye");
			ModifySilverSconceFuel(SilverSconcePrefabGreen, "Guck");

			// Modify silver sconce blue and green lights
			ModifyLightSettings("piece_groundtorch_blue", SilverSconcePrefabBlue);
			ModifyLightSettings("piece_groundtorch_green", SilverSconcePrefabGreen);

			// Modify Silver Sconce Icon
			ModifySilverSconceIcon(SilverSconcePrefabBlue, new Color(0.4f, 0.7f, 1f));
			ModifySilverSconceIcon(SilverSconcePrefabGreen, new Color(0.2f, 1f, 0.4f));

			// Add silver sconce to ZNetScene
			MPrefabManager.RegisterToZNetScene(SilverSconcePrefab);
			MPrefabManager.RegisterToZNetScene(SilverSconcePrefabBlue);
			MPrefabManager.RegisterToZNetScene(SilverSconcePrefabGreen);

			// Configure the Piece data and add to build menu
			BuildPieceController.ConfigurePiece(SilverSconcePrefab, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("ElderBark", 2),
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2),
				BuildPieceController.MakeRequirement("Resin", CompatibilityManager.PermanentLightsEnabled ? 6 : 2, false)
			});

			BuildPieceController.ConfigurePiece(SilverSconcePrefabBlue, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("ElderBark", 2),
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2),
				BuildPieceController.MakeRequirement("GreydwarfEye", CompatibilityManager.PermanentLightsEnabled ? 6 : 2, false)
			});

			BuildPieceController.ConfigurePiece(SilverSconcePrefabGreen, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("ElderBark", 2),
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2),
				BuildPieceController.MakeRequirement("Guck", CompatibilityManager.PermanentLightsEnabled ? 6 : 2, false)
			});

			// Set active
			SilverSconcePrefab.SetActive(true);
			SilverSconcePrefabBlue.SetActive(true);
			SilverSconcePrefabGreen.SetActive(true);
			log.Info("Silver Sconces registered and ready.");
		}

		private static GameObject CloneBronzeSconcePrefab(string sourcePrefabName, string newPrefabName)
		{
			GameObject prefab = MPrefabManager.ClonePrefab(sourcePrefabName, newPrefabName);
			if (prefab == null)
			{
				log.Error($"Cloning of {sourcePrefabName} failed.");
			}

			return prefab;
		}

		private static void SetupSilverSconceDefaults(GameObject prefab, string name, string craftingStation = null)
		{
			ZNetView znet = prefab.GetComponent<ZNetView>();
			if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Default;
				znet.m_syncInitialScale = true;
			}

			Piece piece = prefab.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_enabled = true;
				piece.m_name = name;
				piece.m_description = "";

				GameObject craftingStationPrefab = MPrefabManager.GetPrefab(craftingStation);
				if (craftingStationPrefab != null)
				{
					CraftingStation newCraftingStation = craftingStationPrefab.GetComponent<CraftingStation>();
					if (newCraftingStation != null)
					{
						piece.m_craftingStation = newCraftingStation;
						log.Info($"Crafting station set to {newCraftingStation.name} for piece {piece.name}");
					}
				}
			}

			Fireplace fireplace = prefab.GetComponent<Fireplace>();
			if (fireplace != null)
			{
				fireplace.m_name = name;
				log.Info($"Name set for Fireplace component");
			}
		}

		private static void ScaleSilverSconce(GameObject prefab)
		{
			// Set the local scale of the prefab's root
			Vector3 scale = Vector3.one * 1.1f;
			prefab.transform.localScale = scale;
		}

		private static void ModifySilverSconceMaterial(GameObject prefab, string metalPrefabName, string woodPrefabName)
		{
			// Get the mesh renderer of the sconce
			MeshRenderer meshRenderer = prefab.GetComponentInChildren<MeshRenderer>();
			if (meshRenderer == null)
			{
				log.Error("Could not get MeshRenderer component.");
				return;
			}

			// Get the silver necklace prefab
			GameObject metalPrefab = MPrefabManager.GetPrefab(metalPrefabName); // SilverNecklace
			if (metalPrefab == null)
			{
				log.Error($"Could not find '{metalPrefabName}' prefab.");
				return;
			}

			// Get the material from its MeshRenderer
			MeshRenderer metalRenderer = metalPrefab.GetComponentInChildren<MeshRenderer>();
			if (metalRenderer == null || metalRenderer.sharedMaterial == null)
			{
				log.Error($"'{metalPrefabName}' has no MeshRenderer or material.");
				return;
			}

			// Get the prefab for the wood part
			GameObject woodPrefab = MPrefabManager.GetPrefab(woodPrefabName); // yggashoot_log, StaffShield
			if (woodPrefab == null)
			{
				log.Error($"Could not find '{woodPrefabName}' prefab.");
				return;
			}

			// Get the material from its MeshRenderer
			MeshRenderer woodRenderer = woodPrefab.GetComponentInChildren<MeshRenderer>();
			if (woodRenderer == null || woodRenderer.sharedMaterial == null)
			{
				log.Error($"'{woodPrefabName}' has no MeshRenderer or material.");
				return;
			}

			// Replace the sconce's material
			Material silverMaterial = metalRenderer.sharedMaterial;
			Material woodMaterial = woodRenderer.sharedMaterial;
			Material[] materials = meshRenderer.materials;

			if (materials.Length >= 2)
			{
				materials[0] = woodMaterial;
				materials[1] = silverMaterial;
			}
			else if (materials.Length == 1)
			{
				materials[0] = woodMaterial; // fallback
			}
			meshRenderer.materials = materials;
		}

		private static void ModifySilverSconceFuel(GameObject prefab, string fuelItemName)
		{
			Fireplace fireplace = prefab.GetComponent<Fireplace>();
			if (fireplace == null)
			{
				log.Error($"No Fireplace component found on {prefab.name}");
				return;
			}

			GameObject fuelItem = MPrefabManager.GetPrefab(fuelItemName);
			if (fuelItem == null)
			{
				log.Error($"Could not find fuel item '{fuelItemName}' for {prefab.name}");
				return;
			}

			fireplace.m_fuelItem = fuelItem.GetComponent<ItemDrop>();
			fireplace.m_secPerFuel += 5000;
			log.Info($"Sec Per Fuel: {fireplace.m_secPerFuel}");
			log.Info($"Set fuel for {prefab.name} to {fuelItemName}");
		}

		private static void ModifyLightSettings(string sourcePrefabName, GameObject targetPrefab)
		{
			GameObject sourcePrefab = MPrefabManager.GetPrefab(sourcePrefabName);
			if (sourcePrefab == null)
			{
				log.Error($"Source prefab '{sourcePrefabName}' not found.");
				return;
			}

			Transform sourceEnabled = sourcePrefab.transform.Find("_enabled");
			if (sourceEnabled == null)
			{
				log.Error($"Could not find '_enabled' child on {sourcePrefabName}.");
				return;
			}

			// Destroy old enabled
			Transform existingEnabled = targetPrefab.transform.Find("_enabled");
			if (existingEnabled != null)
			{
				UnityEngine.Object.DestroyImmediate(existingEnabled.gameObject);
			}

			// Deep clone
			GameObject newEnabled = UnityEngine.Object.Instantiate(sourceEnabled.gameObject, targetPrefab.transform);
			newEnabled.name = "_enabled";
			newEnabled.transform.localPosition = new Vector3(0.11f, 0.5f, 0f);
			newEnabled.transform.localRotation = Quaternion.identity;
			newEnabled.transform.localScale = Vector3.one;

			// Make sure it's visible and rebind it to Fireplace
			newEnabled.SetActive(true);

			Fireplace fp = targetPrefab.GetComponent<Fireplace>();
			if (fp != null)
			{
				fp.m_enabledObject = newEnabled;
				log.Info($"Rebound Fireplace.m_enabledObject for {targetPrefab.name}");
			}
			else
			{
				log.Warn($"No Fireplace component found on {targetPrefab.name}");
			}

			log.Info($"Replaced '_enabled' from {sourcePrefabName} to {targetPrefab.name}");
		}

		private static Sprite ModifySilverSconceIcon(GameObject prefab, Color targetTint, bool isBlue = false)
		{
			Piece sconcePiece = prefab.GetComponent<Piece>();
			if (sconcePiece == null)
			{
				log.Warn("Piece prefab is null.");
				return null;
			}

			Sprite originalIcon = sconcePiece.m_icon;
			if (originalIcon == null)
			{
				log.Warn($"Piece '{sconcePiece.name}' has no icon assigned.");
				return null;
			}
			if (originalIcon.texture == null)
			{
				log.Warn($"Original icon texture is null for piece '{sconcePiece.name}'.");
				return null;
			}

			//Rect atlasRect = new Rect(864, 848, 64, 64); // 872, 848
			Rect atlasRect = originalIcon.textureRect;

			// Copy only the sub-region from the atlas
			Texture2D croppedTex = new Texture2D((int)atlasRect.width, (int)atlasRect.height, TextureFormat.RGBA32, false);
			RenderTexture rt = RenderTexture.GetTemporary(originalIcon.texture.width, originalIcon.texture.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(originalIcon.texture, rt);
			RenderTexture.active = rt;

			croppedTex.ReadPixels(atlasRect, 0, 0);
			croppedTex.Apply();

			Color[] pixels = croppedTex.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				Color c = pixels[i];

				// Detect yellow/orange glow
				if (c.r > 0.6f && c.g > 0.5f && c.b < 0.4f && Mathf.Abs(c.r - c.g) < 0.25f)
				{
					float intensity = (c.r + c.g) * 0.5f;

					// Special tweak for blue icons
					if (isBlue)
					{
						intensity *= 1.3f; // brighten glow for blue
					}

					pixels[i] = new Color(
						targetTint.r * intensity,
						targetTint.g * intensity,
						targetTint.b * intensity,
						c.a
					);
				}
				else
				{
					pixels[i] = c;
				}
			}
			croppedTex.SetPixels(pixels);
			croppedTex.Apply();

			RenderTexture.ReleaseTemporary(rt);
			RenderTexture.active = null;

			// Create new Sprite from cropped region
			Sprite newIcon = Sprite.Create(
				croppedTex,
				new Rect(0, 0, croppedTex.width, croppedTex.height),
				new Vector2(0.5f, 0.5f),
				originalIcon.pixelsPerUnit
			);

			// Replace the icon on the Piece
			sconcePiece.m_icon = newIcon;

			log.Info($"Icon changed for {prefab.name}");

			return newIcon;
		}

		public static bool ToggleVisibility()
		{
			bool enabled = ConfigManager.BuildPiecesLightingEnabled.Value;
			bool toggled = true;

			toggled &= BuildPieceController.TogglePiece(SilverSconcePrefab?.GetComponent<Piece>(), enabled, log);
			toggled &= BuildPieceController.TogglePiece(SilverSconcePrefabBlue?.GetComponent<Piece>(), enabled, log);
			toggled &= BuildPieceController.TogglePiece(SilverSconcePrefabGreen?.GetComponent<Piece>(), enabled, log);

			return toggled;
		}

		public static void RefreshSilverSconceRequirements()
		{
			if (SilverSconcePrefab == null || SilverSconcePrefabBlue == null || SilverSconcePrefabGreen == null)
				return;

			BuildPieceController.RefreshPieceRequirements(SilverSconcePrefab.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2 },
				{ "Resin", CompatibilityManager.PermanentLightsEnabled ? 6 : 2 }
			});

			BuildPieceController.RefreshPieceRequirements(SilverSconcePrefabBlue.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2 },
				{ "GreydwarfEye", CompatibilityManager.PermanentLightsEnabled ? 6 : 2 }
			});

			BuildPieceController.RefreshPieceRequirements(SilverSconcePrefabGreen.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 1 : 2 },
				{ "Guck", CompatibilityManager.PermanentLightsEnabled ? 6 : 2 }
			});
		}
	}
}
