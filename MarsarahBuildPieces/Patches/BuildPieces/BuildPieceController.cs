using HarmonyLib;
using Jotunn.Configs;
using MarsarahBuildPieces.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class BuildPieceController
	{
		private static readonly LogManager log = new LogManager("Build Piece Controller", LogManager.LogLevel.Info);

		[HarmonyPatch(typeof(Player), "OnSpawned")]
		internal static class Player_OnSpawned_Patch
		{
			static void Postfix()
			{
				bool toggled = true;

				toggled &= SmartDropbox.ToggleVisibility();
				toggled &= PocketPortal.TogglePocketPortalVisibility();
				toggled &= GlacialStonePortal.TogglePortalVisibility();
				toggled &= MysticalLightWard.ToggleVisibility();
				toggled &= SilverSconce.ToggleVisibility();
				toggled &= ColoredDvergrLanterns.ToggleVisibility();
				toggled &= GreenStandingBrazier.ToggleVisibility();
				toggled &= SilverHangingBrazier.ToggleVisibility();
				toggled &= SmallSign.ToggleVisibility();

				if (toggled)
				{
					log.Info("Initial build piece visibility toggled.");
				}
				else
				{
					log.Info("Initial build piece visibility not ready.");
				}
			}
		}

		public static void ConfigurePiece(GameObject prefab, string category, RequirementConfig[] requirements, string toolName = "Hammer")
		{
			if (prefab == null)
			{
				log.Warn("Cannot configure null prefab.");
				return;
			}

			PieceConfig pieceConfig = new PieceConfig
			{
				PieceTable = toolName,
				Category = category,
				Requirements = requirements
			};

			MPrefabManager.AddToBuildMenu(prefab, pieceConfig);

			log.Info($"Registered prefab '{prefab.name}' in '{toolName}' build menu under '{category}'.");
		}

		public static RequirementConfig MakeRequirement(string item, int amount, bool recover = true)
		{
			return new RequirementConfig(item, amount, recover: recover);
		}

		public static void RefreshPieceRequirements(Piece piece, Dictionary<string, int> amounts)
		{
			if (piece == null)
			{
				log.Warn("Cannot configure a null piece.");
				return;
			}

			if (piece.m_resources == null)
			{
				log.Warn($"Resources for piece '{piece.name}' are null.");
				return;
			}

			for (int i = 0; i < piece.m_resources.Length; i++)
			{
				Piece.Requirement resource = piece.m_resources[i];
				if (resource?.m_resItem == null)
					continue;

				string name = resource.m_resItem.name;

				if (amounts.TryGetValue(name, out int value))
				{
					resource.m_amount = value;
					log.Info($"Updated resource {name}: {value} for piece '{piece.name}'.");
				}
			}
		}

		public static bool TogglePiece(Piece piece, bool enabled, LogManager specificLog, string toolName = "Hammer")
		{
			if (piece == null)
				return false;

			if (ObjectDB.instance == null)
			{
				specificLog.Info("ObjectDB is not ready yet.");
				return false;
			}

			GameObject toolPrefab = ObjectDB.instance.GetItemPrefab(toolName);
			if (toolPrefab == null)
			{
				specificLog.Warn($"Could not find tool prefab '{toolName}'.");
				return false;
			}

			ItemDrop toolItem = toolPrefab.GetComponent<ItemDrop>();
			PieceTable pieceTable = toolItem?.m_itemData?.m_shared?.m_buildPieces;

			if (pieceTable == null)
			{
				specificLog.Warn($"Could not find PieceTable for '{toolName}'.");
				return false;
			}

			piece.m_enabled = enabled;

			if (enabled)
			{
				if (!pieceTable.m_pieces.Contains(piece.gameObject))
					pieceTable.m_pieces.Add(piece.gameObject);
			}
			else
			{
				if (pieceTable.m_pieces.Contains(piece.gameObject))
					pieceTable.m_pieces.Remove(piece.gameObject);
			}

			return true;
		}
	}
}