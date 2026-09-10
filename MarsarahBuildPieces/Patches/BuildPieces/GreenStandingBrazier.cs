using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal class GreenStandingBrazier
	{
		private static readonly LogManager log = new LogManager("Green Standing Brazier", LogManager.LogLevel.Warning);

		private static bool initialized = false;
		private static GameObject GreenBrazierPrefab;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateGreenBrazier();
			}
		}

		private static void CreateGreenBrazier()
		{
			if (MPrefabManager.GetPrefab("piece_brazierfloor03") != null) return;

			// Clone Standing Brazier
			GreenBrazierPrefab = CloneGreenBrazierPrefab();
			if (GreenBrazierPrefab == null) return;

			// Apply brazier-specific data
			SetupGreenBrazierDefaults();

			// Modify the brazier fuel
			ModifyBrazierFuel();

			// Modify coal material
			ModifyCoalMaterials();

			// Modify green brazier light
			ModifyLightSettings();

			// Modify green grazier Icon
			ModifyGreenBrazierIcon();

			// Add green brazier to ZNetScene
			MPrefabManager.RegisterToZNetScene(GreenBrazierPrefab);

			// Configure the Piece data and add to buiild menu
			BuildPieceController.ConfigurePiece(GreenBrazierPrefab, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("Bronze", 5),
				BuildPieceController.MakeRequirement("Guck", 2, false),
				BuildPieceController.MakeRequirement("WolfClaw", 3)
			});

			// Set active
			GreenBrazierPrefab.SetActive(true);
			log.Info("Green Brazier registered and ready.");
		}

		private static GameObject CloneGreenBrazierPrefab()
		{
			GameObject prefab = MPrefabManager.ClonePrefab("piece_brazierfloor01", "piece_brazierfloor03");
			if (prefab == null)
			{
				log.Error($"Cloning of 'piece_brazierfloor01' failed.");
			}

			return prefab;
		}

		private static void SetupGreenBrazierDefaults()
		{
			ZNetView znet = GreenBrazierPrefab.GetComponent<ZNetView>();
			if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Solid;
				znet.m_syncInitialScale = false;
			}

			Piece piece = GreenBrazierPrefab.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_enabled = true;
				piece.m_name = "Green Standing Brazier";
				piece.m_description = "";

				string craftingStation = "forge";
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

		private static void ModifyBrazierFuel()
		{
			Fireplace fireplace = GreenBrazierPrefab.GetComponent<Fireplace>();
			if (fireplace == null)
			{
				log.Error($"No Fireplace component found on {GreenBrazierPrefab.name}");
				return;
			}

			string fuelItemName = "Guck";
			GameObject fuelItem = MPrefabManager.GetPrefab(fuelItemName);
			if (fuelItem == null)
			{
				log.Error($"Could not find fuel item '{fuelItemName}' for {GreenBrazierPrefab.name}");
				return;
			}

			fireplace.m_fuelItem = fuelItem.GetComponent<ItemDrop>();
			log.Info($"Set fuel for {GreenBrazierPrefab.name} to {fuelItemName}");
		}

		private static void ModifyCoalMaterials()
		{
			log.Info("Replacing coal materials for green brazier...");

			// Locate the _enabled child
			Transform enabled = GreenBrazierPrefab.transform.Find("_enabled");
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

		private static void ModifyLightSettings()
		{
			log.Info("Modifying light settings for green brazier...");

			// Find the _enabled_high child
			Transform enabledHigh = GreenBrazierPrefab.transform.Find("_enabled_high");
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
							main.startColor = new Color(0f, 0.8f, 0f, 0.2f); // dimmer translucent green
							log.Info("Recolored flare particle system to green.");
						}
					}
				}
			}
			else
			{
				log.Warn("fx_Brazier_flames not found.");
			}
		}

		private static Sprite ModifyGreenBrazierIcon()
		{
			log.Info("Modifying icon for green brazier...");

			Piece greenBrazierPiece = GreenBrazierPrefab.GetComponent<Piece>();
			if (greenBrazierPiece == null)
			{
				log.Warn("Piece prefab is null.");
				return null;
			}

			Sprite originalIcon = greenBrazierPiece.m_icon;
			if (originalIcon == null)
			{
				log.Warn($"Piece '{greenBrazierPiece.name}' has no icon assigned.");
				return null;
			}
			if (originalIcon.texture == null)
			{
				log.Warn($"Original icon texture is null for piece '{greenBrazierPiece.name}'.");
				return null;
			}

			//Rect atlasRect = new Rect(576, 672, 64, 64); // 608, 656
			Rect atlasRect = originalIcon.textureRect;

			// Copy only the sub-region from the atlas
			Texture2D croppedTex = new Texture2D((int)atlasRect.width, (int)atlasRect.height, TextureFormat.RGBA32, false);
			RenderTexture rt = RenderTexture.GetTemporary(originalIcon.texture.width, originalIcon.texture.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(originalIcon.texture, rt);
			RenderTexture.active = rt;

			croppedTex.ReadPixels(atlasRect, 0, 0);
			croppedTex.Apply();

			Color targetTint = new Color(0.2f, 1f, 0.4f);
			Color[] pixels = croppedTex.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				Color c = pixels[i];

				// Detect red-glow pixels
				if (c.r > 0.5f && c.r > c.g + 0.1f && c.r > c.b + 0.1f)
				{
					// Convert red glow to green (use red intensity as green strength)
					pixels[i] = new Color(c.r * 0.2f, c.r * 1.0f, c.r * 0.2f, c.a);
					// R dimmed, G strong, B slightly boosted for glow depth
				}
				else
				{
					// Leave other pixels intact
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
			greenBrazierPiece.m_icon = newIcon;

			log.Info($"Icon changed for {GreenBrazierPrefab.name}");

			return newIcon;
		}

		public static bool ToggleVisibility()
		{
			bool enabled = ConfigManager.BuildPiecesLightingEnabled.Value;
			bool toggled = true;

			toggled &= BuildPieceController.TogglePiece(GreenBrazierPrefab?.GetComponent<Piece>(), enabled, log);

			return toggled;
		}
	}
}
