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
	}
}