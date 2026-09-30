using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MarsarahBuildPieces.Managers
{
	internal static class MAssetBundleManager
	{
		private const string MarsaBundleResourceName = "MarsarahBuildPieces.Assets.marsarahbuildpieces_assets";

		private static readonly LogManager log = new LogManager("M Asset Bundle Manager", LogManager.LogLevel.Info);
		private static AssetBundle marsaBundle;

		internal static GameObject LoadPrefab(string prefabName)
		{
			AssetBundle bundle = LoadMarsaBundle();
			if (bundle == null)
				return null;

			foreach (string assetName in bundle.GetAllAssetNames())
			{
				if (!assetName.EndsWith("/" + prefabName + ".prefab", StringComparison.OrdinalIgnoreCase))
					continue;

				GameObject prefab = bundle.LoadAsset<GameObject>(assetName);

				if (prefab == null)
				{
					log.Error($"Failed to load prefab '{prefabName}' from marsabundle.");
					return null;
				}

				log.Info($"Loaded prefab '{prefabName}' from marsabundle.");

				return prefab;
			}

			log.Error($"Prefab '{prefabName}' was not found in marsabundle.");

			return null;
		}

		private static AssetBundle LoadMarsaBundle()
		{
			if (marsaBundle != null)
				return marsaBundle;

			Assembly assembly = Assembly.GetExecutingAssembly();

			using (Stream stream = assembly.GetManifestResourceStream(MarsaBundleResourceName))
			{
				if (stream == null)
				{
					log.Error($"Embedded asset bundle '{MarsaBundleResourceName}' was not found.");
					return null;
				}

				using (BinaryReader reader = new BinaryReader(stream))
				{
					byte[] data = reader.ReadBytes((int)stream.Length);
					marsaBundle = AssetBundle.LoadFromMemory(data);
				}
			}

			if (marsaBundle == null)
			{
				log.Error("Failed to load embedded marsabundle.");
				return null;
			}

			log.Info("Loaded embedded marsabundle.");

			return marsaBundle;
		}

		internal static Texture2D LoadTexture(string textureName)
		{
			AssetBundle bundle = LoadMarsaBundle();
			if (bundle == null)
				return null;

			foreach (string assetName in bundle.GetAllAssetNames())
			{
				if (!assetName.EndsWith("/" + textureName, StringComparison.OrdinalIgnoreCase))
					continue;

				Texture2D texture = bundle.LoadAsset<Texture2D>(assetName);
				if (texture == null)
				{
					log.Error($"Failed to load texture '{textureName}' from marsabundle.");
					return null;
				}

				log.Info($"Loaded texture '{textureName}' from marsabundle.");

				return texture;
			}

			log.Error($"Texture '{textureName}' was not found in marsabundle.");

			return null;
		}

		internal static Sprite LoadEmbeddedSprite(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
			{
				log.Error("Cannot load embedded sprite with an empty file name.");
				return null;
			}

			string resourceName = $"MarsarahBuildPieces.Assets.{fileName}";
			Assembly assembly = Assembly.GetExecutingAssembly();

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					log.Error($"Embedded image '{resourceName}' was not found.");
					return null;
				}

				byte[] data;

				using (BinaryReader reader = new BinaryReader(stream))
					data = reader.ReadBytes((int)stream.Length);

				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

				if (!LoadImage(texture, data))
				{
					log.Error($"Failed to load embedded image '{resourceName}'.");
					UnityEngine.Object.Destroy(texture);
					return null;
				}

				texture.name = Path.GetFileNameWithoutExtension(fileName);
				texture.wrapMode = TextureWrapMode.Clamp;

				Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
				sprite.name = texture.name;

				log.Info($"Loaded embedded sprite '{fileName}'.");

				return sprite;
			}
		}

		private static bool LoadImage(Texture2D texture, byte[] data)
		{
			Type imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
			if (imageConversionType == null)
			{
				log.Error("Could not resolve UnityEngine.ImageConversion at runtime.");
				return false;
			}

			MethodInfo loadImageMethod = imageConversionType.GetMethod(
				"LoadImage",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
				null
			);

			if (loadImageMethod == null)
			{
				log.Error("Could not resolve UnityEngine.ImageConversion.LoadImage.");
				return false;
			}

			object result = loadImageMethod.Invoke(null, new object[] { texture, data, false });

			return result is bool loaded && loaded;
		}
	}
}