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
		private static readonly HashSet<SmartDropboxBehavior> activeDropboxes = new HashSet<SmartDropboxBehavior>();
		private const string DestinationHandoffRequestRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationHandoffRequest";
		private const string DestinationHandoffResponseRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationHandoffResponse";
		private const string DestinationAccessRequestRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationAccessRequest";
		private const string DestinationAccessResponseRpcName = MarsarahBuildPieces.ModGUID + ".SmartDropboxDestinationAccessResponse";
		internal const float HandoffTimeoutSeconds = 5f;

		private static ZRoutedRpc registeredRoutedRpc;
		private static readonly int SmartDropboxPrefabHash = "smart_dropbox".GetStableHashCode();

		private const string MultiUserChestIgnoreKey = "MUC_Ignore";

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
			internal int TotalMoved;
			internal int ContainersUsed;
		}

		internal static bool ApplyMultiUserChestIgnoreFlag(ZNetView nview)
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner())
				return false;

			ZDO zdo = nview.GetZDO();
			if (zdo == null)
				return false;

			if (!zdo.GetBool(MultiUserChestIgnoreKey))
			{
				zdo.Set(MultiUserChestIgnoreKey, true);
				ZDOMan.instance?.ForceSendZDO(zdo.m_uid);

				log.Info($"Applied MultiUserChest ignore flag | ZDO={zdo.m_uid}");
			}

			return true;
		}

		private static readonly Dictionary<ZDOID, DistributionTransaction> activeDistributions = new Dictionary<ZDOID, DistributionTransaction>();

		internal static float SearchRadius => ConfigManager.SmartDropboxRadius.Value;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		private static class ZNetScene_Awake_Patch
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

		[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
		private static class InventoryGui_CloseContainer_Patch
		{
			static void Prefix(Container ___m_currentContainer, out SmartDropboxBehavior __state)
			{
				__state = GetBehavior(___m_currentContainer);
			}

			static void Postfix(SmartDropboxBehavior __state)
			{
				if (__state == null)
					return;

				__state.RequestServerHandoff();
			}
		}

		[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
		private static class InventoryGui_Hide_Patch
		{
			static void Prefix(Container ___m_currentContainer, out SmartDropboxBehavior __state)
			{
				__state = GetBehavior(___m_currentContainer);
			}

			static void Postfix(SmartDropboxBehavior __state)
			{
				if (__state == null)
					return;

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
			AddSmartDropboxEffect(SmartDropboxPrefab);
			ApplySmartDropboxMaterial(SmartDropboxPrefab, "piece_chest_barrel", "barrelplayer_mat");

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

			SetVisualState(SmartDropboxPrefab, enabled);

			foreach (SmartDropboxBehavior dropbox in activeDropboxes)
			{
				if (dropbox != null)
					SetVisualState(dropbox.gameObject, enabled);
			}

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

			rpc.Register<ZDOID, ZDOID>(DestinationHandoffRequestRpcName, RPC_RequestDestinationHandoff);
			rpc.Register<ZDOID, ZDOID, uint>(DestinationHandoffResponseRpcName, RPC_DestinationHandoffResponse);

			rpc.Register<ZDOID, ZDOID, long>(DestinationAccessRequestRpcName, RPC_RequestDestinationAccess);
			rpc.Register<ZDOID, ZDOID, bool, long>(DestinationAccessResponseRpcName, RPC_DestinationAccessResponse);

			registeredRoutedRpc = rpc;
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
			long playerId = Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;

			ZDOMan.instance.ForceSendZDO(zdo.m_uid);

			if (ZNet.instance.IsServer())
			{
				TryAcquireServerOwnership(ZDOMan.GetSessionID(), zdo, expectedRevision, playerId);
				return;
			}

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

			ZDOMan.instance.ForceSendZDO(zdo.m_uid);

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
					TryAcquireServerOwnership(sender, zdo, expectedRevision, playerId);
					yield break;
				}

				yield return new WaitForSecondsRealtime(0.05f);
			}

			ZDO timedOutZdo = ZDOMan.instance?.GetZDO(zdoId);
			uint finalRevision = timedOutZdo?.DataRevision ?? 0;

			log.Warn($"Timed out waiting for Smart Dropbox synchronization | ZDO={zdoId} | ServerRevision={finalRevision} | ExpectedRevision={expectedRevision}");
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

		private static int MoveMatchingItems(Inventory sourceInventory, Inventory destinationInventory)
		{
			int totalMoved = 0;
			List<ItemDrop.ItemData> sourceItems = new List<ItemDrop.ItemData>(sourceInventory.GetAllItems());

			foreach (ItemDrop.ItemData sourceItem in sourceItems)
			{
				if (!destinationInventory.ContainsItemByName(sourceItem.m_shared.m_name))
					continue;

				if (!HasCapacityForItem(destinationInventory, sourceItem))
					continue;

				int amountBefore = sourceItem.m_stack;
				bool fullyAdded = destinationInventory.AddItem(sourceItem);
				int moved = fullyAdded ? amountBefore : amountBefore - sourceItem.m_stack;

				if (fullyAdded)
					sourceInventory.RemoveItem(sourceItem);

				totalMoved += moved;
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

			int moved = MoveMatchingItems(sourceInventory, destinationInventory);

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

				return moved;
			}
			catch (Exception ex)
			{
				log.Error($"Failed to save Smart Dropbox transfer | Source={sourceId} | Destination={destination.m_uid} | {ex}");
				return 0;
			}
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

				foreach (ItemDrop.ItemData sourceItem in sourceItems)
				{
					if (!destinationInventory.ContainsItemByName(sourceItem.m_shared.m_name))
						continue;

					result.Add(candidate);
					break;
				}
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

				if (DestinationUsesWardCheck(destination))
				{
					long serverId = ZDOMan.GetSessionID();

					if (ZNet.instance != null && ZNet.instance.IsServer() && transaction.DepositingPeer == serverId)
					{
						bool allowed = PrivateArea.CheckAccess(destination.GetPosition(), 0f, flash: false);

						if (!allowed)
						{
							log.Info($"Destination skipped: ward denied access | Destination={destination.m_uid} | PlayerID={transaction.PlayerId}");
							transaction.Index++;
							continue;
						}

						RequestDestinationHandoff(source, destination);
						return;
					}

					ZRoutedRpc.instance.InvokeRoutedRPC(transaction.DepositingPeer, DestinationAccessRequestRpcName, sourceId, destination.m_uid, transaction.PlayerId);
					return;
				}

				RequestDestinationHandoff(source, destination);
				return;
			}

			if (transaction.TotalMoved > 0)
				log.Info($"Distribution completed | Source={sourceId} | Moved={transaction.TotalMoved} | Containers={transaction.ContainersUsed}");

			activeDistributions.Remove(sourceId);
		}

		private static bool DestinationStillMatchesSource(ZDO source, ZDO destination)
		{
			if (!TryReadInventory(source, out Inventory sourceInventory))
				return false;

			if (!TryReadInventory(destination, out Inventory destinationInventory))
				return false;

			foreach (ItemDrop.ItemData sourceItem in sourceInventory.GetAllItems())
			{
				if (destinationInventory.ContainsItemByName(sourceItem.m_shared.m_name))
					return true;
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

		private static void RequestDestinationHandoff(ZDO source, ZDO destination)
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

				TryAcquireDestinationOwnership(source.m_uid, destination, serverId, destination.DataRevision);
				return;
			}

			if (currentOwner == serverId)
			{
				TryAcquireDestinationOwnership(source.m_uid, destination, serverId, destination.DataRevision);
				return;
			}

			ZRoutedRpc.instance.InvokeRoutedRPC(currentOwner, DestinationHandoffRequestRpcName, source.m_uid, destination.m_uid);
		}

		private static void RPC_RequestDestinationHandoff(long sender, ZDOID sourceId, ZDOID destinationId)
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

				ZRoutedRpc.instance.InvokeRoutedRPC(sender, DestinationHandoffResponseRpcName, sourceId, destinationId, expectedRevision);
				return;
			}			

			ZDOMan.instance.ForceSendZDO(destinationId);

			ZRoutedRpc.instance.InvokeRoutedRPC(sender, DestinationHandoffResponseRpcName, sourceId, destinationId, expectedRevision);
		}

		private static void RPC_DestinationHandoffResponse(long sender, ZDOID sourceId, ZDOID destinationId, uint expectedRevision)
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

			if (destination.DataRevision >= expectedRevision)
			{
				TryAcquireDestinationOwnership(sourceId, destination, sender, expectedRevision);
				return;
			}

			if (ZNetScene.instance == null)
				return;

			ZNetScene.instance.StartCoroutine(WaitForDestinationHandoff(sourceId, destinationId, sender, expectedRevision));
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

			RequestDestinationHandoff(source, destination);
		}

		private static IEnumerator WaitForDestinationHandoff(ZDOID sourceId, ZDOID destinationId, long expectedOwner, uint expectedRevision)
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
					TryAcquireDestinationOwnership(sourceId, destination, expectedOwner, expectedRevision);
					yield break;
				}

				yield return new WaitForSecondsRealtime(0.05f);
			}

			log.Warn($"Timed out waiting for destination synchronization | Destination={destinationId} | ExpectedRevision={expectedRevision}");
		}

		private static void TryAcquireDestinationOwnership(ZDOID sourceId, ZDO destination, long expectedOwner, uint expectedRevision)
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

			if (destination.GetInt(ZDOVars.s_inUse) == 1)
			{
				log.Info($"Destination acquisition cancelled because container is in use | Destination={destination.m_uid}");
				AdvanceDistribution(sourceId);
				return;
			}

			long serverId = ZDOMan.GetSessionID();

			if (expectedOwner != serverId)
				destination.SetOwner(serverId);

			int moved = TransferItemsToDestination(sourceId, destination);

			if (moved > 0 && activeDistributions.TryGetValue(sourceId, out DistributionTransaction transaction))
			{
				transaction.TotalMoved += moved;
				transaction.ContainersUsed++;

				GameObject destinationPrefab = ZNetScene.instance?.GetPrefab(destination.GetPrefab());
				string prefabName = destinationPrefab != null ? destinationPrefab.name : destination.GetPrefab().ToString();
				Vector3 position = destination.GetPosition();

				log.Info($"Transferred items | Moved={moved} | Destination={destination.m_uid} | Prefab={prefabName} | Position={position.x:0.0}, {position.y:0.0}, {position.z:0.0}");
			}

			if (expectedOwner != serverId)
			{
				destination.SetOwner(expectedOwner);
				ZDOMan.instance.ForceSendZDO(destination.m_uid);
			}

			AdvanceDistribution(sourceId);
		}

		private static bool DestinationUsesWardCheck(ZDO destination)
		{
			if (destination == null || ZNetScene.instance == null)
				return false;

			GameObject prefab = ZNetScene.instance.GetPrefab(destination.GetPrefab());
			Container container = prefab?.GetComponent<Container>();

			return container != null && container.m_checkGuardStone;
		}

		private static bool HasCapacityForItem(Inventory inventory, ItemDrop.ItemData item)
		{
			if (inventory.GetEmptySlots() > 0)
				return true;

			foreach (ItemDrop.ItemData existingItem in inventory.GetAllItems())
			{
				if (existingItem.m_shared.m_name != item.m_shared.m_name)
					continue;

				if (existingItem.m_quality != item.m_quality)
					continue;

				if (existingItem.m_worldLevel != item.m_worldLevel)
					continue;

				if (existingItem.m_stack < existingItem.m_shared.m_maxStackSize)
					return true;
			}

			return false;
		}

		private static void AddSmartDropboxEffect(GameObject prefab)
		{
			GameObject wardPrefab = MPrefabManager.GetPrefab("guard_stone");
			if (wardPrefab == null)
			{
				log.Warn("Could not find guard_stone for Smart Dropbox visual effect.");
				return;
			}

			Transform sourceEffect = wardPrefab.transform.Find("WayEffect");
			if (sourceEffect == null)
			{
				log.Warn("Could not find WayEffect on guard_stone.");
				return;
			}

			GameObject effect = UnityEngine.Object.Instantiate(sourceEffect.gameObject, prefab.transform);
			effect.name = "SmartDropboxEffect";

			effect.transform.localPosition = new Vector3(0f, 0.65f, 0f);
			effect.transform.localRotation = Quaternion.identity;
			effect.transform.localScale = Vector3.one * 0.55f;

			foreach (Component component in effect.GetComponentsInChildren<Component>(true))
			{
				if (component != null && component.GetType().Name == "AudioSource")
					UnityEngine.Object.DestroyImmediate(component);
			}

			ConfigureSmartDropboxEffect(effect);

			effect.SetActive(ConfigManager.SmartDropboxEnabled.Value);
		}


		private static void ConfigureSmartDropboxEffect(GameObject effect)
		{
			if (effect == null)
				return;

			DisableEffectChild(effect.transform, "Point light");
			DisableEffectChild(effect.transform, "flare");
			DisableEffectChild(effect.transform, "pulse (1)");
			DisableEffectChild(effect.transform, "pulse (2)");

			// Optional: if the glow itself is still too bright, disable this too.
			// DisableEffectChild(effect.transform, "glow");
		}

		private static void DisableEffectChild(Transform root, string childName)
		{
			Transform child = root.Find(childName);
			if (child != null)
				child.gameObject.SetActive(false);
		}

		private static void ApplySmartDropboxMaterial(GameObject prefab, string sourcePrefabName, string sourceMaterialName)
		{
			if (prefab == null)
				return;

			GameObject sourcePrefab = MPrefabManager.GetPrefab(sourcePrefabName);
			if (sourcePrefab == null)
			{
				log.Warn($"Could not find source prefab '{sourcePrefabName}' for Smart Dropbox material.");
				return;
			}

			Material sourceMaterial = null;

			foreach (Renderer renderer in sourcePrefab.GetComponentsInChildren<Renderer>(true))
			{
				foreach (Material material in renderer.sharedMaterials)
				{
					if (material == null || material.name != sourceMaterialName)
						continue;

					sourceMaterial = material;
					break;
				}

				if (sourceMaterial != null)
					break;
			}

			if (sourceMaterial == null)
			{
				log.Warn($"Could not find material '{sourceMaterialName}' on prefab '{sourcePrefabName}'.");
				return;
			}

			Material smartMaterial = new Material(sourceMaterial)
			{
				name = "SmartDropbox_Material"
			};

			foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
			{
				Material[] materials = renderer.sharedMaterials;
				bool changed = false;

				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] == null || !materials[i].name.StartsWith("ironchest"))
						continue;

					materials[i] = smartMaterial;
					changed = true;
				}

				if (changed)
					renderer.sharedMaterials = materials;
			}
		}

		private static void SetVisualState(GameObject dropbox, bool enabled)
		{
			if (dropbox == null)
				return;

			Transform effect = dropbox.transform.Find("SmartDropboxEffect");
			if (effect != null)
				effect.gameObject.SetActive(enabled);
		}

		internal static void RegisterDropbox(SmartDropboxBehavior dropbox)
		{
			if (dropbox == null)
				return;

			activeDropboxes.Add(dropbox);
			SetVisualState(dropbox.gameObject, ConfigManager.SmartDropboxEnabled.Value);
		}

		internal static void UnregisterDropbox(SmartDropboxBehavior dropbox)
		{
			if (dropbox == null)
				return;

			activeDropboxes.Remove(dropbox);
		}
	}

	internal sealed class SmartDropboxBehavior : MonoBehaviour
	{
		private ZNetView nview;

		private void Start()
		{
			nview = SmartDropbox.GetContainerZNetView(GetComponent<Container>());

			StartCoroutine(ApplyMultiUserChestIgnoreFlag());

			SmartDropbox.HideRadiusMarker(gameObject);
			SmartDropbox.RegisterDropbox(this);
		}

		private void OnDestroy()
		{
			SmartDropbox.UnregisterDropbox(this);
		}

		internal void RequestServerHandoff()
		{
			if (nview == null)
				nview = SmartDropbox.GetContainerZNetView(GetComponent<Container>());

			SmartDropbox.RequestServerHandoff(nview);
		}

		private IEnumerator ApplyMultiUserChestIgnoreFlag()
		{
			float startTime = Time.realtimeSinceStartup;

			while (Time.realtimeSinceStartup - startTime < 5f)
			{
				if (SmartDropbox.ApplyMultiUserChestIgnoreFlag(nview))
					yield break;

				yield return new WaitForSecondsRealtime(0.1f);
			}
		}
	}
}