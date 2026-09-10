using HarmonyLib;
using MarsarahBuildPieces.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class GlacialStonePortal
	{
		private static readonly LogManager log = new LogManager("Glacial Stone Portal", LogManager.LogLevel.Warning);

		private static bool initialized;
		private static GameObject GlacialPortalPrefab;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateGlacialPortal();
			}
		}

		private static void CreateGlacialPortal()
		{
			if (MPrefabManager.GetPrefab("portal_glacial") != null)
				return;

			GlacialPortalPrefab = ClonePortalPrefab("portal", "portal_glacial");
			if (GlacialPortalPrefab == null)
				return;

			SetupGlacialPortalDefaults(GlacialPortalPrefab, "Glacial Stone Portal", "piece_stonecutter");

			MPrefabManager.RegisterToZNetScene(GlacialPortalPrefab);

			ConfigureGlacialPortalPieceData(GlacialPortalPrefab, "Stone", "FreezeGland", "SurtlingCore");

			TogglePortalVisibility();

			GlacialPortalPrefab.SetActive(true);

			log.Info("Glacial Portal registered and ready.");
		}

		private static GameObject ClonePortalPrefab(string sourcePrefabName, string newPrefabName)
		{
			GameObject prefab = MPrefabManager.ClonePrefab(sourcePrefabName, newPrefabName);

			if (prefab == null)
				log.Error($"Cloning of {sourcePrefabName} failed.");

			return prefab;
		}

		private static void SetupGlacialPortalDefaults(GameObject prefab, string name, string craftingStation = null)
		{
			ZNetView znet = prefab.GetComponent<ZNetView>();
			if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Solid;
				znet.m_syncInitialScale = false;
			}

			Piece piece = prefab.GetComponent<Piece>();
			if (piece == null)
				return;

			piece.m_enabled = true;
			piece.m_name = name;
			piece.m_description = "";

			GameObject craftingStationPrefab = MPrefabManager.GetPrefab(craftingStation);
			if (craftingStationPrefab == null)
				return;

			CraftingStation newCraftingStation = craftingStationPrefab.GetComponent<CraftingStation>();
			if (newCraftingStation == null)
				return;

			piece.m_craftingStation = newCraftingStation;

			log.Info($"Crafting station set to {newCraftingStation.name} for piece {piece.name}.");
		}

		private static void ConfigureGlacialPortalPieceData(GameObject prefab, string resource1, string resource2, string resource3)
		{
			List<Jotunn.Configs.RequirementConfig> requirements = new List<Jotunn.Configs.RequirementConfig>();

			if (!string.IsNullOrEmpty(resource1))
				requirements.Add(BuildPieceController.MakeRequirement(resource1, 6));

			if (!string.IsNullOrEmpty(resource2))
				requirements.Add(BuildPieceController.MakeRequirement(resource2, 2));

			if (!string.IsNullOrEmpty(resource3))
				requirements.Add(BuildPieceController.MakeRequirement(resource3, 2));

			BuildPieceController.ConfigurePiece(prefab, "Misc", requirements.ToArray());
		}

		public static bool TogglePortalVisibility()
		{
			bool enabled = ConfigManager.GlacialStonePortalEnabled.Value;

			return BuildPieceController.TogglePiece(GlacialPortalPrefab?.GetComponent<Piece>(), enabled, log);
		}

		[HarmonyPatch(typeof(Game), "Awake")]
		public static class EarlyPortalPrefabRegister
		{
			static void Prefix(Game __instance)
			{
				int hashGlacial = "portal_glacial".GetStableHashCode();

				if (!__instance.PortalPrefabHash.Contains(hashGlacial))
				{
					__instance.PortalPrefabHash.Add(hashGlacial);

					log.Info($"Registered portal_glacial prefab hash early: {hashGlacial}");
				}
			}
		}

		[HarmonyPatch(typeof(Game), nameof(Game.ConnectPortals))]
		public static class Game_ConnectPortals_Patch
		{
			static void Prefix(Game __instance)
			{
				if (GlacialPortalPrefab == null)
				{
					log.Info("ConnectPortals patch: Glacial Portal prefab not ready yet.");
					return;
				}

				if (!__instance.m_portalPrefabs.Contains(GlacialPortalPrefab))
				{
					__instance.m_portalPrefabs.Add(GlacialPortalPrefab);

					int hashGlacial = "portal_glacial".GetStableHashCode();
					if (!__instance.PortalPrefabHash.Contains(hashGlacial))
						__instance.PortalPrefabHash.Add(hashGlacial);

					log.Info("Registered 'portal_glacial' in Game.m_portalPrefabs via ConnectPortals.");
				}
			}
		}
	}
}