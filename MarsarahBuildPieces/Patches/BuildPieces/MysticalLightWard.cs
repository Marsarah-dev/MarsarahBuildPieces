using HarmonyLib;
using MarsarahBuildPieces.Managers;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class MysticalLightWard
	{
		private static readonly LogManager log = new LogManager("Mystical Light Ward", LogManager.LogLevel.Info);

		private static bool initialized;
		private static GameObject MysticalWardPrefab;

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
			SetupMysticalWardDefaults(MysticalWardPrefab);
			ScaleMysticalWard(MysticalWardPrefab);

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

			return BuildPieceController.TogglePiece(MysticalWardPrefab?.GetComponent<Piece>(), enabled, log);
		}
	}
}