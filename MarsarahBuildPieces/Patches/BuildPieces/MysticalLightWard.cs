using HarmonyLib;
using MarsarahBuildPieces.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class MysticalLightWard
	{
		private static readonly LogManager log = new LogManager("Mystical Light Ward", LogManager.LogLevel.Warning);

		private static bool initialized;
		private static GameObject MysticalWardPrefab;
		private static readonly HashSet<MysticalLightWardArea> activeWards = new HashSet<MysticalLightWardArea>();
		private static readonly Color MysticalGlowColor = new Color(0.25f, 0.55f, 1f, 1f);

		internal static float EffectRadius => ConfigManager.MysticalLightWardRadius.Value;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateMysticalWard();
			}
		}

		[HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
		private static class Fireplace_UpdateFireplace_Patch
		{
			// TODO: Remove this prefix when done testing light fuel
			/*private static bool burnSpeedLogged;

			private static void Prefix(Fireplace __instance)
			{
				__instance.m_secPerFuel = 3f;

				if (!burnSpeedLogged)
				{
					log.Info("Temporary testing: fireplace fuel burn time set to 3 seconds per fuel.");
					burnSpeedLogged = true;
				}
			}*/

			private static void Postfix(Fireplace __instance, ref ZNetView ___m_nview)
			{
				if (___m_nview == null || !___m_nview.IsOwner())
					return;

				if (!IsInsideActiveWard(__instance.transform.position))
					return;

				ZDO zdo = ___m_nview.GetZDO();
				if (zdo == null)
					return;

				zdo.Set("fuel", __instance.m_maxFuel);
			}
		}

		[HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
		private static class Player_SetupPlacementGhost_Patch
		{
			static void Postfix(GameObject ___m_placementGhost)
			{
				if (___m_placementGhost == null || !___m_placementGhost.name.StartsWith("mystical_ward"))
					return;

				SetupPlacementRadius(___m_placementGhost);
			}
		}

		private static void CreateMysticalWard()
		{
			if (MPrefabManager.GetPrefab("mystical_ward") != null)
				return;

			MysticalWardPrefab = MPrefabManager.ClonePrefab("guard_stone", "mystical_ward");
			if (MysticalWardPrefab == null)
			{
				log.Error("Failed to clone guard_stone.");
				return;
			}

			RemoveWardBehavior(MysticalWardPrefab);
			MysticalWardPrefab.AddComponent<MysticalLightWardArea>();
			SetupMysticalWardDefaults(MysticalWardPrefab);
			ScaleMysticalWard(MysticalWardPrefab);
			ApplyMysticalGlow(MysticalWardPrefab);

			SetRadiusMarker(MysticalWardPrefab);
			HideRadiusMarker(MysticalWardPrefab);

			MPrefabManager.RegisterToZNetScene(MysticalWardPrefab);

			BuildPieceController.ConfigurePiece(MysticalWardPrefab, "Misc", new[]
			{
				BuildPieceController.MakeRequirement("FineWood", 2),
				BuildPieceController.MakeRequirement("Silver", 1),
				BuildPieceController.MakeRequirement("Obsidian", 2)
			});

			ToggleVisibility();

			MysticalWardPrefab.SetActive(true);

			log.Info("Mystical Light Ward registered and ready.");
		}

		private static void RemoveWardBehavior(GameObject prefab)
		{
			PrivateArea privateArea = prefab.GetComponent<PrivateArea>();
			if (privateArea == null)
			{
				log.Warn("PrivateArea component was not found on the cloned ward.");
				return;
			}

			Object.DestroyImmediate(privateArea);

			log.Info("Removed normal Ward behavior from Mystical Light Ward.");
		}

		private static void SetupMysticalWardDefaults(GameObject prefab)
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
			if (piece == null)
			{
				log.Error("Mystical Light Ward has no Piece component.");
				return;
			}

			piece.m_enabled = true;
			piece.m_name = "Mystical Light Ward";
			piece.m_description = "Keeps fueled light sources burning within its area.";

			GameObject workbenchPrefab = MPrefabManager.GetPrefab("piece_workbench");
			if (workbenchPrefab != null)
				piece.m_craftingStation = workbenchPrefab.GetComponent<CraftingStation>();
		}

		private static void ScaleMysticalWard(GameObject prefab)
		{
			prefab.transform.localScale = Vector3.one * 0.6f;
		}

		public static bool ToggleVisibility()
		{
			bool enabled = ConfigManager.MysticalLightWardEnabled.Value;

			SetVisualState(MysticalWardPrefab, enabled);

			foreach (MysticalLightWardArea ward in activeWards)
			{
				if (ward != null)
					SetVisualState(ward.gameObject, enabled);
			}

			return BuildPieceController.TogglePiece(MysticalWardPrefab?.GetComponent<Piece>(), enabled, log);
		}

		internal static void RegisterWard(MysticalLightWardArea ward)
		{
			if (ward == null)
				return;

			activeWards.Add(ward);

			SetVisualState(ward.gameObject, ConfigManager.MysticalLightWardEnabled.Value);

			log.Info($"Registered Mystical Light Ward area at {ward.transform.position}.");
		}

		internal static void UnregisterWard(MysticalLightWardArea ward)
		{
			if (ward == null)
				return;

			activeWards.Remove(ward);
		}

		public static bool IsInsideActiveWard(Vector3 position)
		{
			if (!ConfigManager.MysticalLightWardEnabled.Value)
				return false;

			float radiusSquared = EffectRadius * EffectRadius;

			foreach (MysticalLightWardArea ward in activeWards)
			{
				if (ward == null)
					continue;

				if ((ward.transform.position - position).sqrMagnitude <= radiusSquared)
					return true;
			}

			return false;
		}

		private static void ApplyMysticalGlow(GameObject prefab)
		{
			foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
			{
				Material[] materials = renderer.sharedMaterials;
				bool changed = false;

				for (int i = 0; i < materials.Length; i++)
				{
					Material material = materials[i];
					if (material == null || !material.name.StartsWith("Guardstone_OdenGlow_mat"))
						continue;

					Material blueMaterial = new Material(material)
					{
						name = "MysticalWard_BlueGlow"
					};

					if (blueMaterial.HasProperty("_Color"))
						blueMaterial.SetColor("_Color", MysticalGlowColor);

					if (blueMaterial.HasProperty("_EmissionColor"))
						blueMaterial.SetColor("_EmissionColor", MysticalGlowColor * 2f);

					materials[i] = blueMaterial;
					changed = true;

					log.Info($"Changed glow material on renderer '{renderer.name}' to mystical blue.");
				}

				if (changed)
					renderer.sharedMaterials = materials;
			}
		}

		private static void SetVisualState(GameObject ward, bool enabled)
		{
			if (ward == null)
				return;

			foreach (Renderer renderer in ward.GetComponentsInChildren<Renderer>(true))
			{
				foreach (Material material in renderer.sharedMaterials)
				{
					if (material == null || !material.name.StartsWith("MysticalWard_BlueGlow"))
						continue;

					if (material.HasProperty("_EmissionColor"))
						material.SetColor("_EmissionColor", enabled ? MysticalGlowColor * 2f : Color.black);
				}

				if (renderer.name == "glow" ||
					renderer.name == "sparcs" ||
					renderer.name.StartsWith("pulse") ||
					renderer.name == "flare")
				{
					renderer.enabled = enabled;
				}
			}

			foreach (ParticleSystem particles in ward.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.EmissionModule emission = particles.emission;
				emission.enabled = enabled;

				if (enabled)
				{
					particles.Play(true);
				}
				else
				{
					particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
			}

			foreach (Light light in ward.GetComponentsInChildren<Light>(true))
			{
				light.enabled = enabled;
			}
		}

		private static void SetRadiusMarker(GameObject prefab)
		{
			foreach (CircleProjector projector in prefab.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				projector.m_radius = EffectRadius;
				projector.m_nrOfSegments = Mathf.CeilToInt(Mathf.Max(5f, 4f * EffectRadius));

				return;
			}

			log.Warn("Could not find AreaMarker on Mystical Light Ward.");
		}

		public static void RefreshRadius()
		{
			if (MysticalWardPrefab == null)
				return;

			SetRadiusMarker(MysticalWardPrefab);

			log.Info($"Mystical Light Ward radius updated to {EffectRadius:0}m.");
		}

		internal static void HideRadiusMarker(GameObject prefab)
		{
			foreach (CircleProjector projector in prefab.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				projector.gameObject.SetActive(false);
				return;
			}

			log.Warn("Could not find AreaMarker on Mystical Light Ward.");
		}

		private static void SetupPlacementRadius(GameObject placementGhost)
		{
			foreach (CircleProjector projector in placementGhost.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				projector.gameObject.SetActive(true);
				projector.enabled = true;
				return;
			}

			log.Warn("Could not find AreaMarker on Mystical Light Ward placement ghost.");
		}
	}

	internal class MysticalLightWardArea : MonoBehaviour, Hoverable
	{
		private ZNetView nview;

		private void Start()
		{
			nview = GetComponent<ZNetView>();

			if (nview == null || nview.GetZDO() == null)
				return;

			MysticalLightWard.HideRadiusMarker(gameObject);
			MysticalLightWard.RegisterWard(this);
		}

		private void OnDestroy()
		{
			MysticalLightWard.UnregisterWard(this);
		}

		public string GetHoverName()
		{
			return "Mystical Light Ward";
		}

		public string GetHoverText()
		{
			if (!ConfigManager.MysticalLightWardEnabled.Value)
				return "Mystical Light Ward\nThe mystical energy of this ward is dormant.";

			return $"Mystical Light Ward\nKeeps fueled light sources permanently lit within {MysticalLightWard.EffectRadius:0}m.";
		}

		public float GetHoverOffset()
		{
			return 0f;
		}
	}
}