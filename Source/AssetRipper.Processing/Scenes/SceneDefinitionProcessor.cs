﻿using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Editor;
using AssetRipper.SourceGenerated.Classes.ClassID_141;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_29;
using AssetRipper.SourceGenerated.Classes.ClassID_3;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.AssetInfo;
using System.Diagnostics;

namespace AssetRipper.Processing.Scenes
{
	public sealed class SceneDefinitionProcessor : IAssetProcessor
	{
		public void Process(GameData gameData)
		{
			Logger.Info(LogCategory.Processing, "Creating Scene Definitions");
			IBuildSettings? buildSettings = null;
			HashSet<AssetCollection> sceneCollections = new();
			Dictionary<AssetCollection, string> scenePaths = new();
			Dictionary<AssetCollection, UnityGuid> sceneGuids = new();
			List<IAssetBundle> sceneAssetBundles = new();

			//Find the relevant assets in this single pass over all the assets.
			foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
			{
				foreach (IUnityObjectBase asset in collection)
				{
					if (asset is ILevelGameManager)
					{
						sceneCollections.Add(collection);
						if (asset is IOcclusionCullingSettings sceneSettings && sceneSettings.Has_SceneGUID())
						{
							sceneGuids[collection] = sceneSettings.SceneGUID;
						}
					}
					else if (asset is IBuildSettings buildSettings1)
					{
						buildSettings = buildSettings1;
					}
					else if (asset is IAssetBundle assetBundle && assetBundle.IsStreamedSceneAssetBundle)
					{
						sceneAssetBundles.Add(assetBundle);
					}
				}
			}

			//Currently, these paths are treated as lower precedent than paths defined in asset bundles, but they should never conflict.
			foreach (AssetCollection sceneCollection in sceneCollections)
			{
				if (SceneHelpers.TryGetScenePath(sceneCollection, buildSettings, out string? scenePath))
				{
					scenePaths[sceneCollection] = scenePath;
				}
			}

			//Extract scene paths from asset bundles.
			foreach (IAssetBundle assetBundleAsset in sceneAssetBundles)
			{
				Bundle bundle = assetBundleAsset.Collection.Bundle;
				if (bundle is not SerializedBundle)
				{
					Logger.Log(LogType.Warning, LogCategory.Processing, $"Scene name recovery is not supported for bundles of type {bundle.GetType().Name}");
				}
				else
				{
					int startingIndex = 0;
					foreach (AccessPairBase<Utf8String, IAssetInfo> pair in assetBundleAsset.Container)
					{
						Debug.Assert(pair.Value.Asset.IsNull(), "Scene pointer is not null");

						string path = Path.ChangeExtension(pair.Key.String, null);
						path = OriginalPathHelper.EnsurePathNotRooted(path);
						path = OriginalPathHelper.EnsureStartsWithAssets(path);
						int index = IndexOf(bundle.Collections, sceneCollections, startingIndex);
						if (index < 0)
						{
							throw new Exception($"Scene collection not found in {bundle.Name} at or after index {startingIndex}");
						}

						AssetCollection sceneCollection = bundle.Collections[index];
						sceneCollections.Add(sceneCollection);//Just to be safe
						scenePaths[sceneCollection] = path;
						startingIndex = index + 1;
					}
				}
			}

			//Make the scene definitions
			foreach (AssetCollection sceneCollection in sceneCollections)
			{
				SceneDefinition sceneDefinition;
				UnityGuid guid = sceneGuids.TryGetValue(sceneCollection, out UnityGuid sceneGuid) ? sceneGuid : default;
				if (scenePaths.TryGetValue(sceneCollection, out string? path))
				{
					sceneDefinition = SceneDefinition.FromPath(path, guid);
				}
				else
				{
					sceneDefinition = SceneDefinition.FromName(sceneCollection.Name, guid);
				}
				sceneDefinition.AddCollection(sceneCollection);
			}
		}

		private static int IndexOf(IReadOnlyList<AssetCollection> list, HashSet<AssetCollection> containingSet, int startingIndex)
		{
			for (int i = startingIndex; i < list.Count; i++)
			{
				if (containingSet.Contains(list[i]))
				{
					return i;
				}
			}
			return -1;
		}
	}
}
