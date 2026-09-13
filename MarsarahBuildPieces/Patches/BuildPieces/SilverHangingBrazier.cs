using HarmonyLib;
using MarsarahBuildPieces.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class SilverHangingBrazier
	{
		private static readonly LogManager log = new LogManager("Silver Hanging Brazier", LogManager.LogLevel.Warning);

		private static bool initialized = false;
		private static GameObject SilverHangingBrazierPrefab;
		private static GameObject SilverHangingBrazierPrefabBlue;
		private static GameObject SilverHangingBrazierPrefabGreen;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateSilverHangingBrazier();
			}
		}

		private static void CreateSilverHangingBrazier()
		{
			if (MPrefabManager.GetPrefab("piece_brazierceiling02") != null && MPrefabManager.GetPrefab("piece_brazierceiling03") != null && MPrefabManager.GetPrefab("piece_brazierceiling04") != null) return;

			// Clone hanging brazier
			SilverHangingBrazierPrefab = CloneSilverHangingBrazierPrefab("piece_brazierceiling01", "piece_brazierceiling02");
			if (SilverHangingBrazierPrefab == null) return;
			SilverHangingBrazierPrefabBlue = CloneSilverHangingBrazierPrefab("piece_brazierceiling01", "piece_brazierceiling03");
			if (SilverHangingBrazierPrefabBlue == null) return;
			SilverHangingBrazierPrefabGreen = CloneSilverHangingBrazierPrefab("piece_brazierceiling01", "piece_brazierceiling04");
			if (SilverHangingBrazierPrefabGreen == null) return;

			// Apply hanging brazier specific data
			SetupSilverHangingBrazierDefaults(SilverHangingBrazierPrefab, "Silver Hanging Brazier", "forge");
			SetupSilverHangingBrazierDefaults(SilverHangingBrazierPrefabBlue, "Blue-burning Silver Hanging Brazier", "forge");
			SetupSilverHangingBrazierDefaults(SilverHangingBrazierPrefabGreen, "Green-burning Silver Hanging Brazier", "forge");

			// Increase silver hanging brazier size
			ScaleSilverHangingBrazier(SilverHangingBrazierPrefab);
			ScaleSilverHangingBrazier(SilverHangingBrazierPrefabBlue);
			ScaleSilverHangingBrazier(SilverHangingBrazierPrefabGreen);

			// Modify silver hanging brazier material
			ModifySilverHangingBrazierMaterial(SilverHangingBrazierPrefab, "SilverNecklace", "MaceSilver");
			ModifySilverHangingBrazierMaterial(SilverHangingBrazierPrefabBlue, "SilverNecklace", "MaceSilver");
			ModifySilverHangingBrazierMaterial(SilverHangingBrazierPrefabGreen, "SilverNecklace", "MaceSilver");

			// Modify hanging brazier fuel
			ModifySilverHangingBrazierFuel(SilverHangingBrazierPrefabBlue, "GreydwarfEye");
			ModifySilverHangingBrazierFuel(SilverHangingBrazierPrefabGreen, "Guck");

			// Modify silver hanging brazier blue and green lights
			CopyLightSettings("piece_brazierfloor02", SilverHangingBrazierPrefabBlue);
			ModifyLightSettings(SilverHangingBrazierPrefabGreen);

			// Modify silver hanging brazier Icon
			ModifySilverHangingBrazierIcon(SilverHangingBrazierPrefabBlue, new Color(0.3f, 0.85f, 1f));
			ModifySilverHangingBrazierIcon(SilverHangingBrazierPrefabGreen, new Color(0.2f, 1f, 0.4f));

			// Add silver hanging brazier to ZNetScene
			MPrefabManager.RegisterToZNetScene(SilverHangingBrazierPrefab);
			MPrefabManager.RegisterToZNetScene(SilverHangingBrazierPrefabBlue);
			MPrefabManager.RegisterToZNetScene(SilverHangingBrazierPrefabGreen);

			// Configure the Piece data and add to build menu
			BuildPieceController.ConfigurePiece(SilverHangingBrazierPrefab, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5),
				BuildPieceController.MakeRequirement("Coal", CompatibilityManager.PermanentLightsEnabled ? 5 : 2, false),
				BuildPieceController.MakeRequirement("Chain", 1)
			});
			BuildPieceController.ConfigurePiece(SilverHangingBrazierPrefabBlue, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5),
				BuildPieceController.MakeRequirement("GreydwarfEye", CompatibilityManager.PermanentLightsEnabled ? 5 : 2, false),
				BuildPieceController.MakeRequirement("Chain", 1)
			});
			BuildPieceController.ConfigurePiece(SilverHangingBrazierPrefabGreen, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5),
				BuildPieceController.MakeRequirement("Guck", CompatibilityManager.PermanentLightsEnabled ? 5 : 2, false),
				BuildPieceController.MakeRequirement("Chain", 1)
			});

			// Set active
			SilverHangingBrazierPrefab.SetActive(true);
			SilverHangingBrazierPrefabBlue.SetActive(true);
			SilverHangingBrazierPrefabGreen.SetActive(true);
			log.Info("Silver Hanging Braziers registered and ready.");
		}

		private static GameObject CloneSilverHangingBrazierPrefab(string sourcePrefabName, string newPrefabName)
		{
			GameObject prefab = MPrefabManager.ClonePrefab(sourcePrefabName, newPrefabName);
			if (prefab == null)
			{
				log.Error($"Cloning of {sourcePrefabName} failed.");
			}

			return prefab;
		}

		private static void SetupSilverHangingBrazierDefaults(GameObject prefab, string name, string craftingStation = null)
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
		}

		private static void ScaleSilverHangingBrazier(GameObject prefab)
		{
			// Set the local scale of the prefab's root
			Vector3 scale = Vector3.one * 1.1f;
			prefab.transform.localScale = scale;
		}

		private static void ModifySilverHangingBrazierMaterial(GameObject prefab, string metalPrefabName, string metalPrefabName2)
		{
			// Get the correct mesh renderer of the sconce
			MeshRenderer targetRenderer = null;

			Transform child = prefab.transform.Find("New");
			if (child != null && child.TryGetComponent<MeshRenderer>(out var namedRenderer))
			{
				targetRenderer = namedRenderer;
			}
			if (targetRenderer == null)
			{
				log.Error("Could not find target MeshRenderer for Silver Hanging Brazier.");
				return;
			}

			// Get the silver necklace prefab
			GameObject metalPrefab = MPrefabManager.GetPrefab(metalPrefabName);
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

			// Get the silver mace prefab
			GameObject metalPrefab2 = MPrefabManager.GetPrefab(metalPrefabName2);
			if (metalPrefab2 == null)
			{
				log.Error($"Could not find '{metalPrefabName2}' prefab.");
				return;
			}

			// Get the material from its MeshRenderer
			MeshRenderer metalRenderer2 = metalPrefab2.GetComponentInChildren<MeshRenderer>();
			if (metalRenderer2 == null || metalRenderer2.sharedMaterial == null)
			{
				log.Error($"'{metalPrefabName2}' has no MeshRenderer or material.");
				return;
			}

			// Replace the brazier's material
			Material silverMaterial = metalRenderer.sharedMaterial;
			Material silverMaterial2 = metalRenderer2.sharedMaterial;
			Material[] materials = targetRenderer.materials;

			if (materials.Length >= 2)
			{
				materials[0] = silverMaterial;
				materials[1] = silverMaterial2;
			}
			else if (materials.Length == 1)
			{
				materials[0] = silverMaterial; // fallback
			}
			targetRenderer.materials = materials;
		}

		private static void ModifySilverHangingBrazierFuel(GameObject prefab, string fuelItemName)
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
			fireplace.m_secPerFuel += 10000;
			log.Info($"Sec Per Fuel: {fireplace.m_secPerFuel}");
			log.Info($"Set fuel for {prefab.name} to {fuelItemName}");
		}

		private static void CopyLightSettings(string sourcePrefabName, GameObject targetPrefab)
		{
			GameObject sourcePrefab = MPrefabManager.GetPrefab(sourcePrefabName);
			if (sourcePrefab == null)
			{
				log.Error($"Source prefab '{sourcePrefabName}' not found.");
				return;
			}

			string[] variants = { "_enabled", "_enabled_high", "_enabled_low" };

			foreach (var variant in variants)
			{
				Transform sourceTransform = sourcePrefab.transform.Find(variant);
				if (sourceTransform == null)
				{
					log.Warn($"No '{variant}' found on source '{sourcePrefabName}', skipping.");
					continue;
				}

				// Remove old variant from target
				Transform existing = targetPrefab.transform.Find(variant);
				if (existing != null)
				{
					UnityEngine.Object.DestroyImmediate(existing.gameObject);
				}

				// Clone with all children
				GameObject clone = UnityEngine.Object.Instantiate(sourceTransform.gameObject, targetPrefab.transform);
				clone.name = variant;

				/*foreach (Transform child in clone.transform)
				{
					log.Info($"Child of {variant}: {child.name}");
				}*/

				// Copy transforms exactly
				clone.transform.localPosition = sourceTransform.localPosition;
				clone.transform.localRotation = sourceTransform.localRotation;
				clone.transform.localScale = sourceTransform.localScale;

				// Apply upward offset (adjust Y as needed)
				clone.transform.localPosition += new Vector3(0f, 0.46f, 0f); // This is the correct position for light to avoid weird shadow flickering below the sconce

				// Adjust coal child position separately
				foreach (Transform child in clone.transform)
				{
					switch (child.name)
					{
						// Lift coal meshes slightly to prevent clipping
						case "Quad (1)":
						case "Coal":
						case "Coal (1)":
						case "Coal (2)":
						case "Coal (3)":
						case "Coal (4)":
						case "Coal (5)":
							child.localPosition += new Vector3(0f, 0.04f, 0f); // Adjustment upwards to avoid clipping
							break;
					}
				}

				if (variant == "_enabled_high")
					clone.SetActive(true);
				else
					clone.SetActive(sourceTransform.gameObject.activeSelf);

				log.Info($"Copied full '{variant}' (with children) from {sourcePrefabName} to {targetPrefab.name}");
			}

			// Rebind Fireplace enabled objects
			Fireplace fp = targetPrefab.GetComponent<Fireplace>();
			if (fp == null)
			{
				log.Warn($"No Fireplace component found on {targetPrefab.name}");
				return;
			}

			Transform newEnabled = targetPrefab.transform.Find("_enabled");
			Transform newEnabledHigh = targetPrefab.transform.Find("_enabled_high");
			Transform newEnabledLow = targetPrefab.transform.Find("_enabled_low");

			if (newEnabled != null)
				fp.m_enabledObject = newEnabled.gameObject;
			else
				log.Warn($"'_enabled' missing on {targetPrefab.name} after copy.");

			if (newEnabledHigh != null)
				fp.m_enabledObjectHigh = newEnabledHigh.gameObject;
			else
				log.Warn($"'_enabled_high' missing on {targetPrefab.name} after copy.");

			if (newEnabledLow != null)
				fp.m_enabledObjectLow = newEnabledLow.gameObject;
			else
				log.Warn($"'_enabled_low' missing on {targetPrefab.name} after copy.");

			log.Info($"Rebound Fireplace enabled objects for {targetPrefab.name}.");
		}

		private static void ModifyLightSettings(GameObject prefab)
		{
			log.Info("Modifying light settings for green brazier...");

			// Find the _enabled_high child
			Transform enabledHigh = prefab.transform.Find("_enabled_high");
			if (enabledHigh == null)
			{
				log.Warn("Could not find _enabled_high in brazier prefab.");
				return;
			}

			// Modify Point Light color
			Transform pointLightT = enabledHigh.Find("Point light");
			if (pointLightT != null)
			{
				if (pointLightT.TryGetComponent<Light>(out var pointLight))
				{
					pointLight.color = Color.green; // new Color(0f, 0.8f, 0f, 0.8f); // slightly darker green
					log.Info("Point Light color set to green.");
				}
				else
				{
					log.Warn("Point Light missing Light component");
				}
			}
			else
			{
				log.Warn("Point Light not found.");
			}

			// Modify fx_Brazier_flames
			Transform flamesRoot = enabledHigh.Find("fx_Brazier_flames");
			if (flamesRoot != null)
			{
				foreach (Transform child in flamesRoot)
				{
					if (child.TryGetComponent<ParticleSystem>(out var ps))
					{
						var main = ps.main;

						if (child.name.Contains("low_flames"))
						{
							main.startColor = new Color(0.1f, 1f, 0.1f, 0.8f); // 0.3f, 1f, 0.3f, 0.8f
							log.Info($"Recolored flame particle system '{child.name}' to green.");
						}
						else if (child.name.Contains("flames (1)"))
						{
							main.startColor = new Color(0.3f, 1f, 0.3f, 1f); // 0.2f, 1f, 0.2f, 0.6f
							log.Info($"Recolored flame particle system '{child.name}' to green.");
						}
						else if (child.name.Equals("flare", StringComparison.OrdinalIgnoreCase))
						{
							main.startColor = new Color(0f, 0.8f, 0f, 0.1f); // dimmer translucent green
							log.Info("Recolored flare particle system to green.");
						}
					}
				}
			}
			else
			{
				log.Warn("fx_Brazier_flames not found.");
			}

			ModifyCoalMaterials(prefab);
		}

		private static void ModifyCoalMaterials(GameObject prefab)
		{
			log.Info("Replacing coal materials for green brazier...");

			// Locate the _enabled child
			Transform enabled = prefab.transform.Find("_enabled");
			if (enabled == null)
			{
				log.Warn("_enabled not found in brazier prefab.");
				return;
			}

			// Find all Coal children
			foreach (Transform child in enabled)
			{
				if (child.name.StartsWith("Coal"))
				{
					if (child.TryGetComponent<MeshRenderer>(out var meshRenderer))
					{
						var mats = meshRenderer.materials;
						for (int i = 0; i < mats.Length; i++)
						{
							if (mats[i].name.StartsWith("glowing_coal"))
							{
								// Duplicate and recolor
								Material newMat = UnityEngine.Object.Instantiate(mats[i]);
								newMat.name = "glowing_coal_green";

								if (newMat.HasProperty("_EmissionColor"))
								{
									newMat.SetColor("_EmissionColor", new Color(0f, 1f, 0.2f) * 1.5f);
									log.Info($"Set emission color for {child.name} to green.");
								}

								mats[i] = newMat;
							}
						}
						meshRenderer.materials = mats;
					}
				}
				else if (child.name.StartsWith("Quad"))
				{
					if (child.TryGetComponent<MeshRenderer>(out var meshRenderer))
					{
						var mats = meshRenderer.materials;
						for (int i = 0; i < mats.Length; i++)
						{
							if (mats[i].name.StartsWith("fireplace_ash_glowing"))
							{
								// Duplicate and recolor
								Material newMat = UnityEngine.Object.Instantiate(mats[i]);
								newMat.name = "fireplace_ash_glowing_green";

								if (newMat.HasProperty("_EmissionColor"))
								{
									newMat.SetColor("_EmissionColor", new Color(0f, 1f, 0.2f) * 1.5f);
									log.Info($"Set emission color for {child.name} to green.");
								}

								mats[i] = newMat;
							}
						}
						meshRenderer.materials = mats;
					}
				}
			}
		}

		private static Sprite ModifySilverHangingBrazierIcon(GameObject prefab, Color targetTint)
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

			//Rect atlasRect = new Rect(1368, 864, 64, 64); // 864, 848
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

				// Detect bright red/orange pixels
				bool isGlow = false;

				// Option 1: yellow/orange
				if (c.r > 0.6f && c.g > 0.3f && c.b < 0.4f)
					isGlow = true;

				// Option 2: deep red
				else if (c.r > 0.6f && c.g < 0.35f && c.b < 0.35f)
					isGlow = true;

				if (isGlow)
				{
					float intensity = (c.r + c.g + c.b) / 3f; // average brightness for scaling
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

			toggled &= BuildPieceController.TogglePiece(SilverHangingBrazierPrefab?.GetComponent<Piece>(), enabled, log);
			toggled &= BuildPieceController.TogglePiece(SilverHangingBrazierPrefabBlue?.GetComponent<Piece>(), enabled, log);
			toggled &= BuildPieceController.TogglePiece(SilverHangingBrazierPrefabGreen?.GetComponent<Piece>(), enabled, log);

			return toggled;
		}

		public static void RefreshSilverHangingBrazierRequirements()
		{
			if (SilverHangingBrazierPrefab == null || SilverHangingBrazierPrefabBlue == null || SilverHangingBrazierPrefabGreen == null)
				return;

			BuildPieceController.RefreshPieceRequirements(SilverHangingBrazierPrefab.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5 },
				{ "Coal", CompatibilityManager.PermanentLightsEnabled ? 5 : 2 }
			});

					BuildPieceController.RefreshPieceRequirements(SilverHangingBrazierPrefabBlue.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5 },
				{ "GreydwarfEye", CompatibilityManager.PermanentLightsEnabled ? 5 : 2 }
			});

					BuildPieceController.RefreshPieceRequirements(SilverHangingBrazierPrefabGreen.GetComponent<Piece>(), new Dictionary<string, int>
			{
				{ "Silver", CompatibilityManager.BuildPieceAmountsEnabled ? 3 : 5 },
				{ "Guck", CompatibilityManager.PermanentLightsEnabled ? 5 : 2 }
			});
		}
	}
}
