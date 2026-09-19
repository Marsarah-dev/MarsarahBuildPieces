using HarmonyLib;
using MarsarahBuildPieces.Managers;
using UnityEngine;

namespace MarsarahBuildPieces.Patches.BuildPieces
{
	internal static class SmallSign
	{
		private static readonly LogManager log = new LogManager("Small Sign", LogManager.LogLevel.Info);

		private static bool initialized = false;
		private static GameObject SmallSignPrefab;

		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			static void Postfix(ZNetScene __instance)
			{
				if (__instance == null || initialized)
					return;

				initialized = true;

				CreateSmallSign();
			}
		}

		private static void CreateSmallSign()
		{
			if (MPrefabManager.GetPrefab("sign_small") != null)
				return;

			SmallSignPrefab = MPrefabManager.ClonePrefab("sign", "sign_small");
			if (SmallSignPrefab == null)
				return;

			SetupSmallSign();
			ScaleSmallSign();
			ModifySmallSignIcon(SmallSignPrefab, 0.85f);

			MPrefabManager.RegisterToZNetScene(SmallSignPrefab);

			BuildPieceController.ConfigurePiece(SmallSignPrefab, "Furniture", new[]
			{
				BuildPieceController.MakeRequirement("Wood", 1),
				BuildPieceController.MakeRequirement("Coal", 1)
			});

			SmallSignPrefab.SetActive(true);

			log.Info("Small Sign registered and ready.");
		}

		private static void SetupSmallSign()
		{
			ZNetView znet = SmallSignPrefab.GetComponent<ZNetView>();
			if (znet != null)
			{
				znet.m_persistent = true;
				znet.m_distant = false;
				znet.m_type = ZDO.ObjectType.Default;
				znet.m_syncInitialScale = true;
			}

			Piece piece = SmallSignPrefab.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_enabled = true;
				piece.m_name = "Small Sign";
				piece.m_description = "When a normal sign is a bit too much, keep things tidy with a smaller one.";
			}
		}

		private static void ScaleSmallSign()
		{
			SmallSignPrefab.transform.localScale = Vector3.one * 0.75f;
		}

		private static Sprite ModifySmallSignIcon(GameObject prefab, float scaleFactor)
		{
			Piece piece = prefab.GetComponent<Piece>();
			if (piece == null)
			{
				log.Warn("Small Sign prefab has no Piece component.");
				return null;
			}

			Sprite originalIcon = piece.m_icon;
			if (originalIcon == null)
			{
				log.Warn("Small Sign original icon is null.");
				return null;
			}

			if (originalIcon.texture == null)
			{
				log.Warn("Small Sign original icon texture is null.");
				return null;
			}

			Rect atlasRect = originalIcon.textureRect;

			Texture2D sourceTex = new Texture2D((int)atlasRect.width, (int)atlasRect.height, TextureFormat.RGBA32, false);
			RenderTexture rt = RenderTexture.GetTemporary(originalIcon.texture.width, originalIcon.texture.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(originalIcon.texture, rt);
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = rt;

			sourceTex.ReadPixels(atlasRect, 0, 0);
			sourceTex.Apply();

			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(rt);

			sourceTex.filterMode = originalIcon.texture.filterMode;
			sourceTex.wrapMode = TextureWrapMode.Clamp;
			sourceTex.anisoLevel = originalIcon.texture.anisoLevel;

			int width = sourceTex.width;
			int height = sourceTex.height;

			Texture2D canvasTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
			canvasTex.filterMode = originalIcon.texture.filterMode;
			canvasTex.wrapMode = TextureWrapMode.Clamp;
			canvasTex.anisoLevel = originalIcon.texture.anisoLevel;

			Color[] clearPixels = new Color[width * height];
			for (int i = 0; i < clearPixels.Length; i++)
			{
				clearPixels[i] = new Color(0f, 0f, 0f, 0f);
			}
			canvasTex.SetPixels(clearPixels);

			int scaledWidth = Mathf.RoundToInt(width * scaleFactor);
			int scaledHeight = Mathf.RoundToInt(height * scaleFactor);

			int xOffset = (width - scaledWidth) / 2;
			int yOffset = (height - scaledHeight) / 2;

			for (int y = 0; y < scaledHeight; y++)
			{
				for (int x = 0; x < scaledWidth; x++)
				{
					int sourceX = Mathf.Clamp(Mathf.FloorToInt(x / scaleFactor), 0, width - 1);
					int sourceY = Mathf.Clamp(Mathf.FloorToInt(y / scaleFactor), 0, height - 1);

					Color pixel = sourceTex.GetPixel(sourceX, sourceY);
					canvasTex.SetPixel(x + xOffset, y + yOffset, pixel);
				}
			}

			canvasTex.Apply();

			Sprite newIcon = Sprite.Create(canvasTex, new Rect(0, 0, canvasTex.width, canvasTex.height), new Vector2(0.5f, 0.5f), originalIcon.pixelsPerUnit);
			piece.m_icon = newIcon;

			log.Info($"Modified icon for {prefab.name} using scale factor {scaleFactor}.");

			return newIcon;
		}

		public static bool ToggleVisibility()
		{
			bool enabled = ConfigManager.SmallSignEnabled.Value;

			return BuildPieceController.TogglePiece(SmallSignPrefab?.GetComponent<Piece>(), enabled, log);
		}
	}
}