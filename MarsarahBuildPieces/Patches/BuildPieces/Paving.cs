
using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class Paving
	{
		private static readonly LogManager log = new LogManager("Paving", LogManager.LogLevel.Info);

		private const string SourcePrefabName = "paved_road_v2";
		private const string PavingPrefabName = "MarsarahBuildPieces_Paving";
		private const string HoePrefabName = "Hoe";

		private static GameObject PavingPrefab;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		private static class ZNetScene_Awake_Patch
		{
			private static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || PavingPrefab != null)
					return;

				CreatePaving(__instance);
			}
		}

		private static void CreatePaving(ZNetScene znScene)
		{
			GameObject hoePrefab = znScene.GetPrefab(HoePrefabName);
			ItemDrop hoeItem = hoePrefab?.GetComponent<ItemDrop>();
			PieceTable hoeTable = hoeItem?.m_itemData?.m_shared?.m_buildPieces;

			if (hoeTable == null)
			{
				log.Error("Could not find Hoe PieceTable.");
				return;
			}

			GameObject sourcePrefab = hoeTable.m_pieces.Find(prefab => prefab != null && prefab.name == SourcePrefabName);

			if (sourcePrefab == null)
			{
				log.Error($"Could not find '{SourcePrefabName}' in the Hoe PieceTable.");
				return;
			}

			PavingPrefab = MPrefabManager.ClonePrefab(sourcePrefab, PavingPrefabName);

			if (PavingPrefab == null)
			{
				log.Error("Failed to clone Paved Road.");
				return;
			}

			// The clone must remain active inside Jotunn's inactive prefab container.
			PavingPrefab.SetActive(true);

			Piece piece = PavingPrefab.GetComponent<Piece>();
			TerrainOp terrainOp = PavingPrefab.GetComponent<TerrainOp>();

			if (piece == null || terrainOp == null)
			{
				log.Error("Paving prefab is missing Piece or TerrainOp.");
				UnityEngine.Object.Destroy(PavingPrefab);
				PavingPrefab = null;
				return;
			}

			if (PavingPrefab.activeInHierarchy)
			{
				log.Error("Paving prefab is unexpectedly active in the hierarchy.");
				UnityEngine.Object.Destroy(PavingPrefab);
				PavingPrefab = null;
				return;
			}

			if (terrainOp.m_settings.m_paintRadius <= 0f)
			{
				log.Error("Paving has an invalid paint radius.");
				UnityEngine.Object.Destroy(PavingPrefab);
				PavingPrefab = null;
				return;
			}

			piece.m_name = "Paving";
			piece.m_description = "Pave the ground without leveling it.";

			terrainOp.m_settings.m_level = false;
			terrainOp.m_settings.m_raise = false;
			terrainOp.m_settings.m_smooth = false;
			terrainOp.m_settings.m_paintCleared = true;

			log.Info($"Paving terrain settings: Level={terrainOp.m_settings.m_level}, Raise={terrainOp.m_settings.m_raise}, Smooth={terrainOp.m_settings.m_smooth}, Paint={terrainOp.m_settings.m_paintCleared}, PaintType={terrainOp.m_settings.m_paintType}, Radius={terrainOp.m_settings.m_paintRadius}.");

			MPrefabManager.RegisterToZNetScene(PavingPrefab);

			// Empty requirements preserve those cloned from vanilla Paved Road.
			BuildPieceController.ConfigurePiece(PavingPrefab, "Misc", Array.Empty<RequirementConfig>(), HoePrefabName);

			log.Info($"Paving prefab registered. ActiveSelf={PavingPrefab.activeSelf}, ActiveInHierarchy={PavingPrefab.activeInHierarchy}.");
		}

		public static bool ToggleVisibility()
		{
			return BuildPieceController.TogglePiece(PavingPrefab?.GetComponent<Piece>(), ConfigManager.PavingEnabled.Value, log, HoePrefabName);
		}
	}
}
