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
		private const string DestinationHandoffRequestRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationHandoffRequest";
		private const string DestinationHandoffResponseRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationHandoffResponse";
		private const string DestinationAccessRequestRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationAccessRequest";
		private const string DestinationAccessResponseRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationAccessResponse";
		internal const float HandoffTimeoutSeconds = 5f;

		private static ZRoutedRpc registeredRoutedRpc;
		private static readonly int SmartDropboxPrefabHash = "smart_dropbox".GetStableHashCode();

		private static readonly HashSet<int> SupportedStoragePrefabs = new HashSet<int>
		{
			"piece_chest_wood".GetStableHashCode(),
			"piece_chest".GetStableHashCode(),
			"piece_chest_blackmetal".GetStableHashCode(),
			"piece_chest_barrel".GetStableHashCode()
		};

		private sealed class DistributionTransaction
		{
			internal long DepositingPeer;
			internal long PlayerId;
			internal readonly List<ZDOID> Destinations = new List<ZDOID>();
			internal int Index;
		}

		private static readonly Dictionary<ZDOID, DistributionTransaction> activeDistributions = new Dictionary<ZDOID, DistributionTransaction>();

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

			rpc.Register<ZDOID, uint, long>(HandoffRpcName, RPC_RequestServerHandoff);

			rpc.Register<ZDOID, ZDOID, long>(DestinationHandoffRequestRpcName, RPC_RequestDestinationHandoff);
			rpc.Register<ZDOID, ZDOID, uint, long>(DestinationHandoffResponseRpcName, RPC_DestinationHandoffResponse);

			rpc.Register<ZDOID, ZDOID, long>(DestinationAccessRequestRpcName, RPC_RequestDestinationAccess);
			rpc.Register<ZDOID, ZDOID, bool, long>(DestinationAccessResponseRpcName, RPC_DestinationAccessResponse);

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

			long playerId = Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;
			ZRoutedRpc.instance.InvokeRoutedRPC(HandoffRpcName, zdo.m_uid, expectedRevision, playerId);
		}

		private static void RPC_RequestServerHandoff(long sender, ZDOID zdoId, uint expectedRevision, long playerId)
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
				TryAcquireServerOwnership(sender, zdo, expectedRevision, playerId);
				return;
			}

			if (ZNetScene.instance == null)
			{
				log.Warn($"Cannot wait for Smart Dropbox handoff: ZNetScene is unavailable for {zdoId}.");
				return;
			}

			log.Info($"Waiting for Smart Dropbox synchronization | ZDO={zdoId} | ServerRevision={zdo.DataRevision} | ExpectedRevision={expectedRevision}");

			ZNetScene.instance.StartCoroutine(WaitForServerHandoff(sender, zdoId, expectedRevision, playerId));
		}

		internal static void TryAcquireServerOwnership(long sender, ZDO zdo, uint expectedRevision, long playerId)
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
			LogDestinationCandidates(zdo);
			LogDestinationMatches(zdo);
			StartDistributionTransaction(zdo, sender, playerId);
		}

		private static IEnumerator WaitForServerHandoff(long sender, ZDOID zdoId, uint expectedRevision, long playerId)
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

					TryAcquireServerOwnership(sender, zdo, expectedRevision, playerId);
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

		private static List<ZDO> FindDestinationCandidates(ZDO source)
		{
			List<ZDO> result = new List<ZDO>();

			if (source == null || ZDOMan.instance == null || ZNetScene.instance == null)
				return result;

			Vector3 sourcePosition = source.GetPosition();
			Vector2s sourceSector = ZoneSystem.GetZone(sourcePosition);

			int sectorRange = Mathf.Max(1, Mathf.CeilToInt(SearchRadius / ZoneSystem.c_ZoneSize));

			List<ZDO> nearby = new List<ZDO>();
			SimulationDistance simulationDistance = new SimulationDistance(sectorRange, 0, classic: true);

			ZDOMan.instance.FindSectorObjects(sourceSector, simulationDistance, nearby);

			float radiusSquared = SearchRadius * SearchRadius;

			foreach (ZDO candidate in nearby)
			{
				if (candidate == null || candidate.m_uid == source.m_uid)
					continue;

				if (!SupportedStoragePrefabs.Contains(candidate.GetPrefab()))
					continue;

				Vector3 offset = candidate.GetPosition() - sourcePosition;

				if (offset.sqrMagnitude > radiusSquared)
					continue;

				GameObject prefab = ZNetScene.instance.GetPrefab(candidate.GetPrefab());
				if (prefab == null || prefab.GetComponent<Container>() == null)
					continue;

				result.Add(candidate);
			}

			result.Sort((a, b) =>
			{
				float distanceA = (a.GetPosition() - sourcePosition).sqrMagnitude;
				float distanceB = (b.GetPosition() - sourcePosition).sqrMagnitude;

				return distanceA.CompareTo(distanceB);
			});

			return result;
		}

		private static void LogDestinationCandidates(ZDO source)
		{
			List<ZDO> candidates = FindDestinationCandidates(source);

			log.Info($"Destination discovery | Source={source.m_uid} | Radius={SearchRadius:0}m | Candidates={candidates.Count}");

			foreach (ZDO candidate in candidates)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(candidate.GetPrefab());
				string prefabName = prefab != null ? prefab.name : candidate.GetPrefab().ToString();

				float distance = Vector3.Distance(source.GetPosition(), candidate.GetPosition());
				byte[] itemData = candidate.GetByteArray(ZDOVars.s_items);
				bool inUse = candidate.GetInt(ZDOVars.s_inUse) == 1;

				log.Info($"Destination candidate | ZDO={candidate.m_uid} | Prefab={prefabName} | Distance={distance:0.0}m | Owner={candidate.GetOwner()} | InUse={inUse} | ItemDataLength={itemData?.Length ?? 0}");
			}
		}

		private static bool TryReadInventory(ZDO zdo, out Inventory inventory)
		{
			inventory = null;

			if (zdo == null || ZNetScene.instance == null || ObjectDB.instance == null)
				return false;

			GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
			if (prefab == null)
				return false;

			Container prefabContainer = prefab.GetComponent<Container>();
			if (prefabContainer == null)
				return false;

			inventory = new Inventory("Smart Dropbox Server Read", prefabContainer.m_bkg, prefabContainer.m_width, prefabContainer.m_height);

			byte[] data = zdo.GetByteArray(ZDOVars.s_items);

			if (data == null || data.Length == 0)
				return true;

			try
			{
				inventory.Load(new ZPackage(data));
				return true;
			}
			catch (Exception ex)
			{
				log.Error($"Failed to read container inventory | ZDO={zdo.m_uid} | Prefab={prefab.name} | {ex}");
				inventory = null;
				return false;
			}
		}

		private static int MoveMatchingItems(Inventory sourceInventory, Inventory destinationInventory, ZDOID destinationId)
		{
			int totalMoved = 0;
			List<ItemDrop.ItemData> sourceItems = new List<ItemDrop.ItemData>(sourceInventory.GetAllItems());

			foreach (ItemDrop.ItemData sourceItem in sourceItems)
			{
				if (!destinationInventory.ContainsItemByName(sourceItem.m_shared.m_name))
					continue;

				int amountBefore = sourceItem.m_stack;
				bool fullyAdded = destinationInventory.AddItem(sourceItem);

				int moved = fullyAdded ? amountBefore : amountBefore - sourceItem.m_stack;

				if (fullyAdded)
					sourceInventory.RemoveItem(sourceItem);

				if (moved <= 0)
					continue;

				totalMoved += moved;

				string itemName = sourceItem.m_dropPrefab != null ? sourceItem.m_dropPrefab.name : sourceItem.m_shared.m_name;

				log.Info($"Item transferred | Item={itemName} | Amount={moved} | Destination={destinationId}");
			}

			return totalMoved;
		}

		private static byte[] SerializeInventory(Inventory inventory)
		{
			ZPackage package = new ZPackage();
			inventory.Save(package);
			return package.GetArray();
		}

		private static int TransferItemsToDestination(ZDOID sourceId, ZDO destination)
		{
			ZDO source = ZDOMan.instance?.GetZDO(sourceId);

			if (source == null || destination == null)
				return 0;

			if (!TryReadInventory(source, out Inventory sourceInventory))
				return 0;

			if (!TryReadInventory(destination, out Inventory destinationInventory))
				return 0;

			int moved = MoveMatchingItems(sourceInventory, destinationInventory, destination.m_uid);

			if (moved <= 0)
			{
				log.Info($"Destination had no available space for matching items | Destination={destination.m_uid}");
				return 0;
			}

			try
			{
				byte[] sourceData = SerializeInventory(sourceInventory);
				byte[] destinationData = SerializeInventory(destinationInventory);

				source.Set(ZDOVars.s_items, sourceData);
				destination.Set(ZDOVars.s_items, destinationData);

				ZDOMan.instance.ForceSendZDO(source.m_uid);
				ZDOMan.instance.ForceSendZDO(destination.m_uid);

				log.Info($"Inventory transfer saved | Source={source.m_uid} | Destination={destination.m_uid} | Moved={moved}");

				return moved;
			}
			catch (Exception ex)
			{
				log.Error($"Failed to save Smart Dropbox transfer | Source={sourceId} | Destination={destination.m_uid} | {ex}");
				return 0;
			}
		}

		private static bool IsSameItemType(ItemDrop.ItemData sourceItem, ItemDrop.ItemData destinationItem)
		{
			if (sourceItem == null || destinationItem == null)
				return false;

			if (sourceItem.m_dropPrefab != null && destinationItem.m_dropPrefab != null)
				return sourceItem.m_dropPrefab.name == destinationItem.m_dropPrefab.name;

			return sourceItem.m_shared.m_name == destinationItem.m_shared.m_name;
		}

		private static List<ZDO> FindMatchingDestinations(ZDO source)
		{
			List<ZDO> result = new List<ZDO>();

			if (!TryReadInventory(source, out Inventory sourceInventory))
				return result;

			List<ItemDrop.ItemData> sourceItems = sourceInventory.GetAllItems();

			foreach (ZDO candidate in FindDestinationCandidates(source))
			{
				if (!TryReadInventory(candidate, out Inventory destinationInventory))
					continue;

				bool hasMatch = false;

				foreach (ItemDrop.ItemData sourceItem in sourceItems)
				{
					foreach (ItemDrop.ItemData destinationItem in destinationInventory.GetAllItems())
					{
						if (!IsSameItemType(sourceItem, destinationItem))
							continue;

						hasMatch = true;
						break;
					}

					if (hasMatch)
						break;
				}

				if (hasMatch)
					result.Add(candidate);
			}

			return result;
		}

		private static void StartDistributionTransaction(ZDO source, long depositingPeer, long playerId)
		{
			if (source == null)
				return;

			List<ZDO> destinations = FindMatchingDestinations(source);

			if (destinations.Count == 0)
			{
				log.Info($"Distribution finished: no matching destinations | Source={source.m_uid}");
				return;
			}

			DistributionTransaction transaction = new DistributionTransaction
			{
				DepositingPeer = depositingPeer,
				PlayerId = playerId
			};

			foreach (ZDO destination in destinations)
				transaction.Destinations.Add(destination.m_uid);

			activeDistributions[source.m_uid] = transaction;

			log.Info($"Distribution sequence started | Source={source.m_uid} | Destinations={transaction.Destinations.Count}");

			ProcessCurrentDestination(source.m_uid);
		}

		private static void ProcessCurrentDestination(ZDOID sourceId)
		{
			if (!activeDistributions.TryGetValue(sourceId, out DistributionTransaction transaction))
				return;

			while (transaction.Index < transaction.Destinations.Count)
			{
				ZDO source = ZDOMan.instance?.GetZDO(sourceId);
				ZDO destination = ZDOMan.instance?.GetZDO(transaction.Destinations[transaction.Index]);

				if (source == null)
				{
					activeDistributions.Remove(sourceId);
					return;
				}

				if (destination == null)
				{
					transaction.Index++;
					continue;
				}

				if (!DestinationStillMatchesSource(source, destination))
				{
					log.Info($"Destination skipped: no remaining matching source items | Destination={destination.m_uid}");
					transaction.Index++;
					continue;
				}

				if (!CheckDestinationPrivacy(destination, transaction.PlayerId))
				{
					log.Info($"Destination skipped: player lacks container access | Destination={destination.m_uid} | PlayerID={transaction.PlayerId}");
					transaction.Index++;
					continue;
				}

				if (DestinationUsesWardCheck(destination))
				{
					log.Info($"Requesting ward access check | Source={sourceId} | Destination={destination.m_uid} | PlayerID={transaction.PlayerId}");

					ZRoutedRpc.instance.InvokeRoutedRPC(transaction.DepositingPeer, DestinationAccessRequestRpcName, sourceId, destination.m_uid, transaction.PlayerId);
					return;
				}

				RequestDestinationHandoff(source, destination, transaction.PlayerId);
				return;
			}

			activeDistributions.Remove(sourceId);

			log.Info($"Distribution destination sequence completed | Source={sourceId}");
		}

		private static bool DestinationStillMatchesSource(ZDO source, ZDO destination)
		{
			if (!TryReadInventory(source, out Inventory sourceInventory))
				return false;

			if (!TryReadInventory(destination, out Inventory destinationInventory))
				return false;

			foreach (ItemDrop.ItemData sourceItem in sourceInventory.GetAllItems())
			{
				foreach (ItemDrop.ItemData destinationItem in destinationInventory.GetAllItems())
				{
					if (IsSameItemType(sourceItem, destinationItem))
						return true;
				}
			}

			return false;
		}

		private static void AdvanceDistribution(ZDOID sourceId)
		{
			if (!activeDistributions.TryGetValue(sourceId, out DistributionTransaction transaction))
				return;

			transaction.Index++;

			ProcessCurrentDestination(sourceId);
		}

		private static void RequestDestinationHandoff(ZDO source, ZDO destination, long playerId)
		{
			if (source == null || destination == null || ZDOMan.instance == null || ZRoutedRpc.instance == null)
				return;

			if (destination.GetInt(ZDOVars.s_inUse) == 1)
			{
				log.Info($"Destination skipped because it is in use | ZDO={destination.m_uid}");
				AdvanceDistribution(source.m_uid);
				return;
			}

			long serverId = ZDOMan.GetSessionID();
			long currentOwner = destination.GetOwner();

			if (!destination.HasOwner())
			{
				destination.SetOwner(serverId);

				log.Info($"Unowned destination acquired by server | Source={source.m_uid} | Destination={destination.m_uid} | PlayerID={playerId}");

				TryAcquireDestinationOwnership(source.m_uid, destination, serverId, destination.DataRevision, playerId);
				return;
			}

			if (currentOwner == serverId)
			{
				log.Info($"Destination already owned by server | Source={source.m_uid} | Destination={destination.m_uid} | PlayerID={playerId}");

				TryAcquireDestinationOwnership(source.m_uid, destination, serverId, destination.DataRevision, playerId);
				return;
			}

			log.Info($"Destination handoff requested | Source={source.m_uid} | Destination={destination.m_uid} | Owner={currentOwner} | Revision={destination.DataRevision}");

			ZRoutedRpc.instance.InvokeRoutedRPC(currentOwner, DestinationHandoffRequestRpcName, source.m_uid, destination.m_uid, playerId);
		}

		private static void RPC_RequestDestinationHandoff(long sender, ZDOID sourceId, ZDOID destinationId, long playerId)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer() || ZDOMan.instance == null || ZRoutedRpc.instance == null)
				return;

			ZDO destination = ZDOMan.instance.GetZDO(destinationId);
			if (destination == null)
				return;

			long localId = ZDOMan.GetSessionID();

			if (destination.GetOwner() != localId)
				return;

			uint expectedRevision = destination.DataRevision;

			if (destination.GetInt(ZDOVars.s_inUse) == 1)
			{
				ZDOMan.instance.ForceSendZDO(destinationId);

				log.Info($"Destination handoff refused because container is in use | ZDO={destinationId}");

				ZRoutedRpc.instance.InvokeRoutedRPC(sender, DestinationHandoffResponseRpcName, sourceId, destinationId, expectedRevision, playerId);
				return;
			}			

			ZDOMan.instance.ForceSendZDO(destinationId);

			log.Info($"Destination flushed by owner | Source={sourceId} | Destination={destinationId} | Revision={expectedRevision}");

			ZRoutedRpc.instance.InvokeRoutedRPC(sender, DestinationHandoffResponseRpcName, sourceId, destinationId, expectedRevision, playerId);
		}

		private static void RPC_DestinationHandoffResponse(long sender, ZDOID sourceId, ZDOID destinationId, uint expectedRevision, long playerId)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
				return;

			ZDO source = ZDOMan.instance.GetZDO(sourceId);
			ZDO destination = ZDOMan.instance.GetZDO(destinationId);

			if (source == null || destination == null)
				return;

			if (source.GetPrefab() != SmartDropboxPrefabHash)
				return;

			if (!SupportedStoragePrefabs.Contains(destination.GetPrefab()))
				return;

			if (destination.GetOwner() != sender)
			{
				log.Info($"Destination owner changed before response | Destination={destinationId} | Sender={sender} | Owner={destination.GetOwner()}");
				return;
			}

			log.Info($"Destination handoff response | Destination={destinationId} | ServerRevision={destination.DataRevision} | ExpectedRevision={expectedRevision} | Owner={sender}");

			if (destination.DataRevision >= expectedRevision)
			{
				TryAcquireDestinationOwnership(sourceId, destination, sender, expectedRevision, playerId);
				return;
			}

			if (ZNetScene.instance == null)
				return;

			ZNetScene.instance.StartCoroutine(WaitForDestinationHandoff(sourceId, destinationId, sender, expectedRevision, playerId));
		}

		private static void RPC_RequestDestinationAccess(long sender, ZDOID sourceId, ZDOID destinationId, long playerId)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer() || ZDOMan.instance == null || ZRoutedRpc.instance == null)
				return;

			long localPlayerId = Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;

			if (localPlayerId != playerId)
			{
				log.Warn($"Ward access check rejected because player ID does not match | Expected={playerId} | Local={localPlayerId}");
				return;
			}

			ZDO destination = ZDOMan.instance.GetZDO(destinationId);
			if (destination == null)
				return;

			bool allowed = PrivateArea.CheckAccess(destination.GetPosition(), 0f, flash: false);

			log.Info($"Ward access checked | Destination={destinationId} | PlayerID={playerId} | Allowed={allowed}");

			ZRoutedRpc.instance.InvokeRoutedRPC(sender, DestinationAccessResponseRpcName, sourceId, destinationId, allowed, playerId);
		}

		private static void RPC_DestinationAccessResponse(long sender, ZDOID sourceId, ZDOID destinationId, bool allowed, long playerId)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
				return;

			if (!activeDistributions.TryGetValue(sourceId, out DistributionTransaction transaction))
				return;

			if (sender != transaction.DepositingPeer || playerId != transaction.PlayerId)
			{
				log.Warn($"Destination access response rejected | Source={sourceId} | Sender={sender}");
				return;
			}

			ZDO source = ZDOMan.instance.GetZDO(sourceId);
			ZDO destination = ZDOMan.instance.GetZDO(destinationId);

			if (source == null || destination == null)
				return;

			if (source.GetPrefab() != SmartDropboxPrefabHash)
				return;

			if (!SupportedStoragePrefabs.Contains(destination.GetPrefab()))
				return;

			if (!allowed)
			{
				log.Info($"Destination skipped: ward denied access | Destination={destinationId} | PlayerID={playerId}");
				AdvanceDistribution(sourceId);
				return;
			}

			if (!CheckDestinationPrivacy(destination, playerId))
			{
				log.Info($"Destination skipped: container privacy changed during access check | Destination={destinationId} | PlayerID={playerId}");
				AdvanceDistribution(sourceId);
				return;
			}

			log.Info($"Destination access granted | Destination={destinationId} | PlayerID={playerId}");

			RequestDestinationHandoff(source, destination, playerId);
		}

		private static IEnumerator WaitForDestinationHandoff(ZDOID sourceId, ZDOID destinationId, long expectedOwner, uint expectedRevision, long playerId)
		{
			float startTime = Time.realtimeSinceStartup;

			while (Time.realtimeSinceStartup - startTime < HandoffTimeoutSeconds)
			{
				if (ZDOMan.instance == null)
					yield break;

				ZDO destination = ZDOMan.instance.GetZDO(destinationId);
				if (destination == null)
					yield break;

				if (destination.GetOwner() != expectedOwner)
				{
					log.Info($"Destination ownership changed while synchronizing | Destination={destinationId} | ExpectedOwner={expectedOwner} | Owner={destination.GetOwner()}");
					yield break;
				}

				if (destination.DataRevision >= expectedRevision)
				{
					TryAcquireDestinationOwnership(sourceId, destination, expectedOwner, expectedRevision, playerId);
					yield break;
				}

				yield return new WaitForSecondsRealtime(0.05f);
			}

			log.Warn($"Timed out waiting for destination synchronization | Destination={destinationId} | ExpectedRevision={expectedRevision}");
		}

		private static void TryAcquireDestinationOwnership(ZDOID sourceId, ZDO destination, long expectedOwner, uint expectedRevision, long playerId)
		{
			if (destination == null || ZDOMan.instance == null)
			{
				AdvanceDistribution(sourceId);
				return;
			}

			if (destination.DataRevision < expectedRevision)
			{
				log.Info($"Destination revision fell behind before acquisition | Destination={destination.m_uid} | Revision={destination.DataRevision} | Expected={expectedRevision}");
				AdvanceDistribution(sourceId);
				return;
			}

			if (destination.GetOwner() != expectedOwner)
			{
				log.Info($"Destination ownership changed before acquisition | Destination={destination.m_uid} | ExpectedOwner={expectedOwner} | Owner={destination.GetOwner()}");
				AdvanceDistribution(sourceId);
				return;
			}

			if (!CheckDestinationPrivacy(destination, playerId))
			{
				log.Info($"Destination acquisition cancelled because access changed | Destination={destination.m_uid} | PlayerID={playerId}");
				AdvanceDistribution(sourceId);
				return;
			}

			if (destination.GetInt(ZDOVars.s_inUse) == 1)
			{
				log.Info($"Destination acquisition cancelled because container is in use | Destination={destination.m_uid}");
				AdvanceDistribution(sourceId);
				return;
			}

			long serverId = ZDOMan.GetSessionID();

			if (expectedOwner != serverId)
				destination.SetOwner(serverId);

			log.Info($"Destination ownership acquired | Source={sourceId} | Destination={destination.m_uid} | Revision={destination.DataRevision} | Owner={destination.GetOwner()} | PlayerID={playerId}");

			int moved = TransferItemsToDestination(sourceId, destination);

			if (expectedOwner != serverId)
			{
				destination.SetOwner(expectedOwner);
				ZDOMan.instance.ForceSendZDO(destination.m_uid);
			}

			log.Info($"Destination processing completed | Destination={destination.m_uid} | Moved={moved} | Owner={destination.GetOwner()}");

			AdvanceDistribution(sourceId);
		}

		private static void LogDestinationMatches(ZDO source)
		{
			if (!TryReadInventory(source, out Inventory sourceInventory))
			{
				log.Warn($"Could not read Smart Dropbox inventory for item matching | ZDO={source.m_uid}");
				return;
			}

			List<ItemDrop.ItemData> sourceItems = sourceInventory.GetAllItems();
			List<ZDO> candidates = FindDestinationCandidates(source);

			if (sourceItems.Count == 0)
			{
				log.Info($"Item matching skipped: Smart Dropbox is empty | ZDO={source.m_uid}");
				return;
			}

			foreach (ItemDrop.ItemData sourceItem in sourceItems)
			{
				string itemName = sourceItem.m_dropPrefab != null ? sourceItem.m_dropPrefab.name : sourceItem.m_shared.m_name;
				int matchingDestinations = 0;

				foreach (ZDO candidate in candidates)
				{
					if (!TryReadInventory(candidate, out Inventory destinationInventory))
						continue;

					int matchingStacks = 0;
					int existingAmount = 0;

					foreach (ItemDrop.ItemData destinationItem in destinationInventory.GetAllItems())
					{
						if (!IsSameItemType(sourceItem, destinationItem))
							continue;

						matchingStacks++;
						existingAmount += destinationItem.m_stack;
					}

					if (matchingStacks == 0)
						continue;

					matchingDestinations++;

					GameObject prefab = ZNetScene.instance.GetPrefab(candidate.GetPrefab());
					string prefabName = prefab != null ? prefab.name : candidate.GetPrefab().ToString();
					float distance = Vector3.Distance(source.GetPosition(), candidate.GetPosition());

					log.Info($"Item match | Item={itemName} | Destination={candidate.m_uid} | Prefab={prefabName} | Distance={distance:0.0}m | MatchingStacks={matchingStacks} | ExistingAmount={existingAmount}");
				}

				log.Info($"Item match summary | Item={itemName} | SourceAmount={sourceItem.m_stack} | MatchingDestinations={matchingDestinations}");
			}
		}

		private static bool CheckDestinationPrivacy(ZDO destination, long playerId)
		{
			if (destination == null || ZNetScene.instance == null)
				return false;

			GameObject prefab = ZNetScene.instance.GetPrefab(destination.GetPrefab());
			Container container = prefab?.GetComponent<Container>();

			if (container == null)
				return false;

			switch (container.m_privacy)
			{
				case Container.PrivacySetting.Public:
					return true;

				case Container.PrivacySetting.Private:
					return destination.GetLong(ZDOVars.s_creator, 0L) == playerId;

				case Container.PrivacySetting.Group:
				default:
					return false;
			}
		}

		private static bool DestinationUsesWardCheck(ZDO destination)
		{
			if (destination == null || ZNetScene.instance == null)
				return false;

			GameObject prefab = ZNetScene.instance.GetPrefab(destination.GetPrefab());
			Container container = prefab?.GetComponent<Container>();

			return container != null && container.m_checkGuardStone;
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