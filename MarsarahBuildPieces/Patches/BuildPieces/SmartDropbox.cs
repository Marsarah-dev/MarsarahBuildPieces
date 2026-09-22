using HarmonyLib;
using MarsarahBuildPieces.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class SmartDropbox
	{
		private static readonly LogManager log = new LogManager("Smart Dropbox", LogManager.LogLevel.Info);

		private static bool initialized;
		private static GameObject SmartDropboxPrefab;

		private const string HandoffRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxHandoff";
		internal const float HandoffTimeoutSeconds = 5f;

		private static ZRoutedRpc registeredRoutedRpc;
		private static readonly int SmartDropboxPrefabHash = "smart_dropbox".GetStableHashCode();

		internal static float SearchRadius => ConfigManager.SmartDropboxRadius.Value;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null)
					return;

				RegisterHandoffRpc();

				if (initialized)
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
				if (__state == null)
					return;

				__state.LogState("Container close completed");
				__state.RequestServerHandoff();
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
				if (__state == null)
					return;

				__state.LogState("GUI hide completed");
				__state.RequestServerHandoff();
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
			/*if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Default;
				znet.m_syncInitialScale = true;
			}*/

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

			GameObject marker = UnityEngine.Object.Instantiate(sourceProjector.gameObject, prefab.transform);
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

		private static void RegisterHandoffRpc()
		{
			ZRoutedRpc rpc = ZRoutedRpc.instance;

			if (rpc == null || registeredRoutedRpc == rpc)
				return;

			rpc.Register<ZDOID, uint>(HandoffRpcName, RPC_RequestServerHandoff);

			registeredRoutedRpc = rpc;

			log.Info("Smart Dropbox handoff RPC registered.");
		}

		internal static void RequestServerHandoff(ZNetView nview)
		{
			if (!ConfigManager.SmartDropboxEnabled.Value)
				return;

			if (nview == null || ZNet.instance == null || ZDOMan.instance == null || ZRoutedRpc.instance == null)
				return;

			ZDO zdo = nview.GetZDO();
			if (zdo == null)
				return;

			if (!nview.IsOwner())
			{
				log.Warn($"Cannot request Smart Dropbox handoff for {zdo.m_uid}: local peer is not the owner.");
				return;
			}

			uint expectedRevision = zdo.DataRevision;

			ZDOMan.instance.ForceSendZDO(zdo.m_uid);

			log.Info($"Handoff requested | ZDO={zdo.m_uid} | ExpectedRevision={expectedRevision} | Owner={zdo.GetOwner()}");

			ZRoutedRpc.instance.InvokeRoutedRPC(HandoffRpcName, zdo.m_uid, expectedRevision);
		}

		private static void RPC_RequestServerHandoff(long sender, ZDOID zdoId, uint expectedRevision)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
				return;

			if (ZDOMan.instance == null)
				return;

			ZDO zdo = ZDOMan.instance.GetZDO(zdoId);
			if (zdo == null)
			{
				log.Info($"Handoff request rejected: ZDO {zdoId} was not found.");
				return;
			}

			if (zdo.GetPrefab() != SmartDropboxPrefabHash)
			{
				log.Info($"Handoff request rejected: ZDO {zdoId} is not a Smart Dropbox.");
				return;
			}

			long serverId = ZDOMan.GetSessionID();
			long currentOwner = zdo.GetOwner();

			if (currentOwner != sender && currentOwner != serverId)
			{
				log.Info($"Handoff request rejected | ZDO={zdoId} | Sender={sender} | Owner={currentOwner}");
				return;
			}

			log.Info($"Handoff request received | Sender={sender} | ZDO={zdoId} | ServerRevision={zdo.DataRevision} | ExpectedRevision={expectedRevision} | Owner={currentOwner}");

			if (zdo.DataRevision >= expectedRevision)
			{
				TryAcquireServerOwnership(sender, zdo, expectedRevision);
				return;
			}

			if (ZNetScene.instance == null)
			{
				log.Warn($"Cannot wait for Smart Dropbox handoff: ZNetScene is unavailable for {zdoId}.");
				return;
			}

			log.Info($"Waiting for Smart Dropbox synchronization | ZDO={zdoId} | ServerRevision={zdo.DataRevision} | ExpectedRevision={expectedRevision}");

			ZNetScene.instance.StartCoroutine(WaitForServerHandoff(sender, zdoId, expectedRevision));
		}

		internal static void TryAcquireServerOwnership(long sender, ZDO zdo, uint expectedRevision)
		{
			if (zdo == null || ZDOMan.instance == null)
				return;

			if (zdo.DataRevision < expectedRevision)
				return;

			long serverId = ZDOMan.GetSessionID();
			long currentOwner = zdo.GetOwner();

			if (currentOwner != sender && currentOwner != serverId)
			{
				log.Info($"Smart Dropbox ownership changed while waiting | ZDO={zdo.m_uid} | Sender={sender} | Owner={currentOwner}");
				return;
			}

			if (zdo.GetInt(ZDOVars.s_inUse) == 1)
			{
				log.Info($"Smart Dropbox handoff cancelled because container is in use | ZDO={zdo.m_uid}");
				return;
			}

			if (currentOwner != serverId)
				zdo.SetOwner(serverId);

			byte[] itemData = zdo.GetByteArray(ZDOVars.s_items);
			log.Info($"(Try Acquire Server Ownership) Source synchronized | ZDO={zdo.m_uid} | Revision={zdo.DataRevision} | ExpectedRevision={expectedRevision} | ItemDataLength={itemData?.Length ?? 0}");

			ZDOMan.instance.ForceSendZDO(zdo.m_uid);

			log.Info($"Server ownership acquired | ZDO={zdo.m_uid} | Persistent={zdo.Persistent} | Revision={zdo.DataRevision} | ExpectedRevision={expectedRevision} | Owner={zdo.GetOwner()}");

			LogServerSourceInventory(zdo);
		}

		private static IEnumerator WaitForServerHandoff(long sender, ZDOID zdoId, uint expectedRevision)
		{
			float startTime = Time.realtimeSinceStartup;

			while (Time.realtimeSinceStartup - startTime < HandoffTimeoutSeconds)
			{
				if (ZDOMan.instance == null)
					yield break;

				ZDO zdo = ZDOMan.instance.GetZDO(zdoId);
				if (zdo == null)
				{
					log.Warn($"Smart Dropbox disappeared while waiting for synchronization | ZDO={zdoId}");
					yield break;
				}

				if (zdo.DataRevision >= expectedRevision)
				{
					byte[] itemData = zdo.GetByteArray(ZDOVars.s_items);

					log.Info($"(WaitForServerHandoff) Source synchronized | ZDO={zdoId} | Revision={zdo.DataRevision} | ExpectedRevision={expectedRevision} | ItemDataLength={itemData?.Length ?? 0}");

					TryAcquireServerOwnership(sender, zdo, expectedRevision);
					yield break;
				}

				yield return new WaitForSecondsRealtime(0.05f);
			}

			ZDO timedOutZdo = ZDOMan.instance?.GetZDO(zdoId);
			uint finalRevision = timedOutZdo?.DataRevision ?? 0;

			log.Warn($"Timed out waiting for Smart Dropbox synchronization | ZDO={zdoId} | ServerRevision={finalRevision} | ExpectedRevision={expectedRevision}");
		}

		private static void LogServerSourceInventory(ZDO zdo)
		{
			if (zdo == null || ZNet.instance == null || !ZNet.instance.IsServer())
				return;

			if (ObjectDB.instance == null)
			{
				log.Warn($"Cannot read Smart Dropbox inventory: ObjectDB is unavailable | ZDO={zdo.m_uid}");
				return;
			}

			if (SmartDropboxPrefab == null)
			{
				log.Warn($"Cannot read Smart Dropbox inventory: prefab is unavailable | ZDO={zdo.m_uid}");
				return;
			}

			Container prefabContainer = SmartDropboxPrefab.GetComponent<Container>();
			if (prefabContainer == null)
			{
				log.Warn($"Cannot read Smart Dropbox inventory: prefab Container is unavailable | ZDO={zdo.m_uid}");
				return;
			}

			byte[] data = zdo.GetByteArray(ZDOVars.s_items);

			if (data == null || data.Length == 0)
			{
				log.Info($"Source inventory read | ZDO={zdo.m_uid} | Stacks=0");
				return;
			}

			try
			{
				Inventory temporaryInventory = new Inventory(
					"Smart Dropbox Server Read",
					prefabContainer.m_bkg,
					prefabContainer.m_width,
					prefabContainer.m_height);

				ZPackage package = new ZPackage(data);
				temporaryInventory.Load(package);

				List<ItemDrop.ItemData> items = temporaryInventory.GetAllItems();

				log.Info($"Source inventory read | ZDO={zdo.m_uid} | Revision={zdo.DataRevision} | Stacks={items.Count} | Size={prefabContainer.m_width}x{prefabContainer.m_height}");

				foreach (ItemDrop.ItemData item in items)
				{
					string itemName = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;

					log.Info($"Source stack | Item={itemName} | Amount={item.m_stack} | Grid={item.m_gridPos.x},{item.m_gridPos.y}");
				}
			}
			catch (Exception ex)
			{
				log.Error($"Failed to read Smart Dropbox source inventory | ZDO={zdo.m_uid} | {ex}");
			}
		}

		internal static ZNetView GetContainerZNetView(Container container)
		{
			if (container == null)
				return null;

			if (container.m_rootObjectOverride != null)
				return container.m_rootObjectOverride.GetComponent<ZNetView>();

			return container.GetComponent<ZNetView>();
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
			container = GetComponent<Container>();
			nview = SmartDropbox.GetContainerZNetView(container);

			if (nview == null || container == null || nview.GetZDO() == null)
				return;

			inventory = container.GetInventory();

			if (inventory != null)
				inventory.m_onChanged += OnInventoryChanged;

			SmartDropbox.HideRadiusMarker(gameObject);

			ZNetView localView = GetComponent<ZNetView>();
			ZNetView containerView = SmartDropbox.GetContainerZNetView(container);

			string localZdo = localView?.GetZDO()?.m_uid.ToString() ?? "none";
			string containerZdo = containerView?.GetZDO()?.m_uid.ToString() ?? "none";

			log.Info($"ZNetView binding | RootOverride={container.m_rootObjectOverride != null} | LocalZDO={localZdo} | ContainerZDO={containerZdo}");

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
			if (container == null)
				container = GetComponent<Container>();

			if (nview == null)
				nview = SmartDropbox.GetContainerZNetView(container);

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
			byte[] itemData = zdo.GetByteArray(ZDOVars.s_items);
			int itemDataLength = itemData?.Length ?? 0;

			log.Info($"{stage} | Session={sessionId} | Server={isServer} | ZDO={zdo.m_uid} | Owner={zdo.GetOwner()} | LocalOwner={nview.IsOwner()} | Persistent={zdo.Persistent} | Revision={zdo.DataRevision} | InUse={inUse} | Stacks={stackCount} | ItemDataLength={itemDataLength}");
		}

		internal void RequestServerHandoff()
		{
			SmartDropbox.RequestServerHandoff(nview);
		}
	}
}