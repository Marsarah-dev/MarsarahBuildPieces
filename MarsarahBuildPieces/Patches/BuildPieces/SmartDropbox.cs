using HarmonyLib;
using MarsarahBuildPieces.Managers;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class SmartDropbox
	{
		private static readonly LogManager log = new LogManager("Smart Dropbox", LogManager.LogLevel.Info);

		private static bool initialized;
		private static GameObject SmartDropboxPrefab;

		internal static float SearchRadius => ConfigManager.SmartDropboxRadius.Value;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateSmartDropbox();
			}
		}

		[HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
		private static class Player_SetupPlacementGhost_Patch
		{
			static void Postfix(GameObject ___m_placementGhost)
			{
				if (___m_placementGhost == null || !___m_placementGhost.name.StartsWith("smart_dropbox"))
					return;

				SetupPlacementRadius(___m_placementGhost);
			}
		}

		[HarmonyPatch(typeof(Container), "RPC_RequestOpen")]
		private static class Container_RPC_RequestOpen_Patch
		{
			static void Prefix(Container __instance)
			{
				GetBehavior(__instance)?.LogState("Open request received");
			}

			static void Postfix(Container __instance)
			{
				GetBehavior(__instance)?.LogState("Open request completed");
			}
		}

		[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show), typeof(Container), typeof(int))]
		private static class InventoryGui_Show_Patch
		{
			static void Postfix(Container container)
			{
				GetBehavior(container)?.LogState("GUI opened");
			}
		}

		[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
		private static class InventoryGui_CloseContainer_Patch
		{
			static void Prefix(Container ___m_currentContainer, out SmartDropboxBehavior __state)
			{
				__state = GetBehavior(___m_currentContainer);
				__state?.LogState("Container close started");
			}

			static void Postfix(SmartDropboxBehavior __state)
			{
				__state?.LogState("Container close completed");
			}
		}

		[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
		private static class InventoryGui_Hide_Patch
		{
			static void Prefix(Container ___m_currentContainer, out SmartDropboxBehavior __state)
			{
				__state = GetBehavior(___m_currentContainer);
				__state?.LogState("GUI hide started");
			}

			static void Postfix(SmartDropboxBehavior __state)
			{
				__state?.LogState("GUI hide completed");
			}
		}

		private static void CreateSmartDropbox()
		{
			if (MPrefabManager.GetPrefab("smart_dropbox") != null)
				return;

			SmartDropboxPrefab = MPrefabManager.ClonePrefab("piece_chest", "smart_dropbox");
			if (SmartDropboxPrefab == null)
			{
				log.Error("Failed to clone piece_chest.");
				return;
			}

			SetupSmartDropboxDefaults(SmartDropboxPrefab);
			AddRadiusMarker(SmartDropboxPrefab);

			if (SmartDropboxPrefab.GetComponent<SmartDropboxBehavior>() == null)
				SmartDropboxPrefab.AddComponent<SmartDropboxBehavior>();

			SetRadiusMarker(SmartDropboxPrefab);
			HideRadiusMarker(SmartDropboxPrefab);

			MPrefabManager.RegisterToZNetScene(SmartDropboxPrefab);

			BuildPieceController.ConfigurePiece(SmartDropboxPrefab, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("FineWood", 10),
				BuildPieceController.MakeRequirement("Iron", 2),
				BuildPieceController.MakeRequirement("Thunderstone", 1)
			});

			ToggleVisibility();

			SmartDropboxPrefab.SetActive(true);

			log.Info("Smart Dropbox registered and ready.");
		}

		private static void SetupSmartDropboxDefaults(GameObject prefab)
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
				log.Error("Smart Dropbox has no Piece component.");
				return;
			}

			piece.m_enabled = true;
			piece.m_name = "Smart Dropbox";
			piece.m_description = "Distributes deposited items to nearby storage that already contains the same item.";

			Container container = prefab.GetComponent<Container>();
			if (container == null)
			{
				log.Error("Smart Dropbox has no Container component.");
				return;
			}

			container.m_name = "Smart Dropbox";
		}

		private static void AddRadiusMarker(GameObject prefab)
		{
			GameObject wardPrefab = MPrefabManager.GetPrefab("guard_stone");
			if (wardPrefab == null)
			{
				log.Warn("Could not find guard_stone for Smart Dropbox radius marker.");
				return;
			}

			CircleProjector sourceProjector = null;

			foreach (CircleProjector projector in wardPrefab.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				sourceProjector = projector;
				break;
			}

			if (sourceProjector == null)
			{
				log.Warn("Could not find AreaMarker on guard_stone.");
				return;
			}

			GameObject marker = Object.Instantiate(sourceProjector.gameObject, prefab.transform);
			marker.name = "AreaMarker";
			marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);
			marker.transform.localRotation = Quaternion.identity;
			marker.transform.localScale = Vector3.one;
			marker.SetActive(false);

			log.Info("Added radius marker to Smart Dropbox.");
		}

		private static void SetRadiusMarker(GameObject prefab)
		{
			foreach (CircleProjector projector in prefab.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				projector.m_radius = SearchRadius;
				projector.m_nrOfSegments = Mathf.CeilToInt(Mathf.Max(5f, 4f * SearchRadius));

				return;
			}

			log.Warn("Could not find AreaMarker on Smart Dropbox.");
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
		}

		private static void SetupPlacementRadius(GameObject placementGhost)
		{
			SetRadiusMarker(placementGhost);

			foreach (CircleProjector projector in placementGhost.GetComponentsInChildren<CircleProjector>(true))
			{
				if (projector.gameObject.name != "AreaMarker")
					continue;

				projector.gameObject.SetActive(true);
				projector.enabled = true;
				return;
			}

			log.Warn("Could not find AreaMarker on Smart Dropbox placement ghost.");
		}

		public static void RefreshRadius()
		{
			if (SmartDropboxPrefab == null)
				return;

			SetRadiusMarker(SmartDropboxPrefab);

			log.Info($"Smart Dropbox search radius updated to {SearchRadius:0}m.");
		}

		public static bool ToggleVisibility()
		{
			bool enabled = ConfigManager.SmartDropboxEnabled.Value;

			return BuildPieceController.TogglePiece(SmartDropboxPrefab?.GetComponent<Piece>(), enabled, log);
		}

		private static SmartDropboxBehavior GetBehavior(Container container)
		{
			if (container == null)
				return null;

			return container.GetComponent<SmartDropboxBehavior>() ?? container.GetComponentInParent<SmartDropboxBehavior>();
		}
	}

	internal sealed class SmartDropboxBehavior : MonoBehaviour
	{
		private static readonly LogManager log = new LogManager("Smart Dropbox State", LogManager.LogLevel.Info);

		private ZNetView nview;
		private Container container;
		private Inventory inventory;

		private void Start()
		{
			nview = GetComponent<ZNetView>();
			container = GetComponent<Container>();

			if (nview == null || container == null || nview.GetZDO() == null)
				return;

			inventory = container.GetInventory();

			if (inventory != null)
				inventory.m_onChanged += OnInventoryChanged;

			SmartDropbox.HideRadiusMarker(gameObject);

			LogState("Started");
		}

		private void OnDestroy()
		{
			if (inventory != null)
				inventory.m_onChanged -= OnInventoryChanged;
		}

		private void OnInventoryChanged()
		{
			LogState("Inventory changed");
		}

		internal void LogState(string stage)
		{
			if (nview == null)
				nview = GetComponent<ZNetView>();

			if (container == null)
				container = GetComponent<Container>();

			if (nview == null || nview.GetZDO() == null)
			{
				log.Info($"{stage} | ZDO unavailable.");
				return;
			}

			ZDO zdo = nview.GetZDO();

			long sessionId = ZDOMan.instance != null ? ZDOMan.GetSessionID() : 0L;
			bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
			int stackCount = container?.GetInventory()?.NrOfItems() ?? -1;
			bool inUse = container != null && container.IsInUse();

			log.Info($"{stage} | Session={sessionId} | Server={isServer} | ZDO={zdo.m_uid} | Owner={zdo.GetOwner()} | LocalOwner={nview.IsOwner()} | Revision={zdo.DataRevision} | InUse={inUse} | Stacks={stackCount}");
		}
	}
}