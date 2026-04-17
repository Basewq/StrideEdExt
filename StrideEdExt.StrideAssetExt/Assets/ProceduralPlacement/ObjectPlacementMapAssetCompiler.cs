using Stride.Assets.Entities;
using Stride.Assets.Models;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Extensions;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement;

[AssetCompiler(typeof(ObjectPlacementMapAsset), typeof(AssetCompilationContext))]
public class ObjectPlacementMapAssetCompiler : AssetCompilerBase
{
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        // We depend on the following Assets to ensure if ObjectPlacementMapAsset is the only thing that is referencing the model asset, then it
        // will actually be included in the build, otherwise Stride may think the model isn't being used.
        yield return new BuildDependencyInfo(typeof(ModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(ProceduralModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(PrefabModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(PrefabAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(TerrainMapAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
    }

    public override IEnumerable<ObjectUrl> GetInputFiles(AssetItem assetItem)
    {
        var asset = (ObjectPlacementMapAsset)assetItem.Asset;
        if (asset.SpawnerDataList is not null)
        {
            foreach (var spawnerData in asset.SpawnerDataList)
            {
                var spawnAssetDefinitionList = spawnerData.SpawnAssetDefinitionList;
                foreach (var spawnAssetDef in spawnAssetDefinitionList)
                {
                    if (!string.IsNullOrEmpty(spawnAssetDef.AssetUrl))
                    {
                        yield return new ObjectUrl(UrlType.Content, spawnAssetDef.AssetUrl);
                    }
                }
            }
        }
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var asset = (ObjectPlacementMapAsset)assetItem.Asset;
        result.BuildSteps = new AssetBuildStep(assetItem);

        var assetFilePath = assetItem.FullPath;
        result.BuildSteps.Add(new ObjectPlacementMapAssetCommand(targetUrlInStorage, asset, assetItem.Package, assetFilePath));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Serialization", "STRDIAG010:Invalid Constructor", Justification = "Not required")]
    private class ObjectPlacementMapAssetCommand : AssetCommand<ObjectPlacementMapAsset>
    {
        private UFile _assetFullFilePath;

        public ObjectPlacementMapAssetCommand(string url, ObjectPlacementMapAsset parameters, IAssetFinder assetFinder, UFile assetFilePath)
            : base(url, parameters, assetFinder)
        {
            Version = 1;

            _assetFullFilePath = assetFilePath;
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            // Converts the 'asset' object into the real 'definition' object which will be serialised.
            var logger = commandContext.Logger;
            var objPlacementMapAsset = Parameters;
            lock (objPlacementMapAsset)     // Lock to prevent ObjectPlacementMapAssetViewModel from modifying it
            {
                objPlacementMapAsset.OnAssetLoaded(_assetFullFilePath, logger);

                var assetFullFolderPath = new UFile(objPlacementMapAsset.OriginalAssetFullFilePath!).GetFullDirectory();
                var resourceRelativeFolderPath = new UDirectory(objPlacementMapAsset.OriginalResourceRelativeFolderPath!);
                var intermediateFilesFullFolderPath = UPath.Combine(assetFullFolderPath, resourceRelativeFolderPath).ToOSPath();

                // Get the assigned Terrain Map Asset
                TerrainMapAsset? terrainMapAsset = null;
                var terrainMapTextureSize = objPlacementMapAsset.TerrainMapTextureSize;
                var chunkIndexToPos = Vector2.Zero;
                var posToChunkIndex = Vector2.Zero;
                if (objPlacementMapAsset.TerrainMap is not null)
                {
                    terrainMapAsset = AssetFinder.FindAssetFromProxyObject(objPlacementMapAsset.TerrainMap)?.Asset as TerrainMapAsset;
                    if (terrainMapAsset is not null)
                    {
                        terrainMapTextureSize = terrainMapAsset.HeightmapTextureSize.ToSize2();

                        var quadPerChunk = terrainMapAsset.QuadsPerMesh * terrainMapAsset.MeshPerChunk.GetSingleAxisLength();
                        chunkIndexToPos = terrainMapAsset.MeshQuadSize * (Vector2)quadPerChunk;
                        posToChunkIndex = chunkIndexToPos != Vector2.Zero
                                            ? 1f / chunkIndexToPos
                                            : Vector2.Zero;
                    }
                }

                var chunkIndexToChunkDataMap = new Dictionary<TerrainChunkIndex2d, ObjectPlacementsChunkData>();

                var modelAssetUrlList = objPlacementMapAsset.ModelAssetUrlList;
                var prefabAssetUrlList = objPlacementMapAsset.PrefabAssetUrlList;

                var objPlcMapAssetItem = AssetFinder.FindAsset(objPlacementMapAsset.Id);
                if (objPlacementMapAsset.SpawnerDataList is not null)
                {
                    foreach (var spawnerData in objPlacementMapAsset.SpawnerDataList)
                    {
                        if (spawnerData.IsDeserializeIntermediateFileRequired)
                        {
                            spawnerData.DeserializeIntermediateFile(intermediateFilesFullFolderPath, assetFullFolderPath, objPlacementMapAsset, terrainMapTextureSize, logger);
                        }
                        if (spawnerData is ModelInstancingSpawnerData modelInstancingSpawnerData)
                        {
                            logger.Info($"{Url}: Generating {modelInstancingSpawnerData.GetType().Name} objects: ObjectCount: {modelInstancingSpawnerData.SpawnPlacementDataList.Count} - ModelType: {modelInstancingSpawnerData.ModelType}");

                            var objectPlacementDataList = modelInstancingSpawnerData.SpawnPlacementDataList;
                            foreach (var placementData in objectPlacementDataList)
                            {
                                var chunkIndex = TerrainChunkIndex2d.ToChunkIndex(placementData.Position, posToChunkIndex);
                                var chunk = chunkIndexToChunkDataMap.GetOrCreateValue(chunkIndex, x => new ObjectPlacementsChunkData());

                                int assetUrlListIndex = placementData.AssetUrlListIndex;
                                if (assetUrlListIndex < 0 || assetUrlListIndex >= modelAssetUrlList.Count)
                                {
                                    logger.Warning($"{Url}: ModelAssetUrlList out of bounds: Index: {placementData.AssetUrlListIndex} - modelAssetUrlList.Count: {modelAssetUrlList.Count}");
                                    continue;
                                }
                                List<ObjectPlacementData> chunkObjectPlacementDataList;
                                if (chunk.ModelPlacements.TryFindItem(
                                    x => x.ModelAssetUrlListIndex == assetUrlListIndex && x.ModelType == modelInstancingSpawnerData.ModelType,
                                    out var modelPlacementData))
                                {
                                    chunkObjectPlacementDataList = modelPlacementData.Placements;
                                }
                                else
                                {
                                    var modelObjectPlacementData = new ModelObjectPlacementsData
                                    {
                                        ModelAssetUrlListIndex = assetUrlListIndex,
                                        ModelType = modelInstancingSpawnerData.ModelType,
                                        Placements = []
                                    };
                                    chunk.ModelPlacements.Add(modelObjectPlacementData);
                                    chunkObjectPlacementDataList = modelObjectPlacementData.Placements;
                                }

                                var objPlacementData = placementData.ToObjectPlacementData();
                                chunkObjectPlacementDataList.Add(objPlacementData);
                            }
                        }
                        else if (spawnerData is PrefabSpawnerData prefabSpawnerData)
                        {
                            logger.Info($"{Url}: Generating {prefabSpawnerData.GetType().Name} objects: ObjectCount: {prefabSpawnerData.SpawnPlacementDataList.Count}");
                            var spawnAssetDefinitionList = prefabSpawnerData.SpawnAssetDefinitionList;
                            var spawnListIndexToAssetUrlListIndex = GetOrCreateSpawnListIndexToAssetUrlListIndex(spawnAssetDefinitionList, prefabAssetUrlList);

                            var objectPlacementDataList = prefabSpawnerData.SpawnPlacementDataList;
                            foreach (var placementData in objectPlacementDataList)
                            {
                                var chunkIndex = TerrainChunkIndex2d.ToChunkIndex(placementData.Position, posToChunkIndex);
                                var chunk = chunkIndexToChunkDataMap.GetOrCreateValue(chunkIndex, x => new ObjectPlacementsChunkData());

                                int assetUrlListIndex = placementData.AssetUrlListIndex;
                                if (assetUrlListIndex < 0 || assetUrlListIndex >= prefabAssetUrlList.Count)
                                {
                                    logger.Warning($"{Url}: PrefabAssetUrlList out of bounds: Index: {placementData.AssetUrlListIndex} - prefabAssetUrlList.Count: {prefabAssetUrlList.Count}");
                                    continue;
                                }
                                List<ObjectPlacementData> chunkObjectPlacementDataList;
                                if (chunk.PrefabPlacements.TryFindItem(
                                    x => x.PrefabAssetUrlListIndex == assetUrlListIndex,
                                    out var prefabPlacementData))
                                {
                                    chunkObjectPlacementDataList = prefabPlacementData.Placements;
                                }
                                else
                                {
                                    var prefabObjectPlacementData = new PrefabObjectPlacementsData
                                    {
                                        PrefabAssetUrlListIndex = assetUrlListIndex,
                                        Placements = []
                                    };
                                    chunk.PrefabPlacements.Add(prefabObjectPlacementData);
                                    chunkObjectPlacementDataList = prefabObjectPlacementData.Placements;
                                }

                                var objPlacementData = placementData.ToObjectPlacementData();
                                chunkObjectPlacementDataList.Add(objPlacementData);
                            }
                        }
                        else if (spawnerData is ManualPrefabSpawnerData manualPrefabSpawnerData)
                        {
                            logger.Info($"{Url}: Generating {manualPrefabSpawnerData.GetType().Name} objects: ObjectCount: {manualPrefabSpawnerData.SpawnPlacementDataList.Count}");
                            var spawnAssetDefinitionList = manualPrefabSpawnerData.SpawnAssetDefinitionList;
                            var spawnListIndexToAssetUrlListIndex = GetOrCreateSpawnListIndexToAssetUrlListIndex(spawnAssetDefinitionList, prefabAssetUrlList);

                            var objectPlacementDataList = manualPrefabSpawnerData.SpawnPlacementDataList;
                            foreach (var placementData in objectPlacementDataList)
                            {
                                var chunkIndex = TerrainChunkIndex2d.ToChunkIndex(placementData.Position, posToChunkIndex);
                                var chunk = chunkIndexToChunkDataMap.GetOrCreateValue(chunkIndex, x => new ObjectPlacementsChunkData());

                                int assetUrlListIndex = placementData.AssetUrlListIndex;
                                if (assetUrlListIndex < 0 || assetUrlListIndex >= prefabAssetUrlList.Count)
                                {
                                    logger.Warning($"{Url}: PrefabAssetUrlList out of bounds: Index: {placementData.AssetUrlListIndex} - prefabAssetUrlList.Count: {prefabAssetUrlList.Count}");
                                    continue;
                                }
                                List<ObjectPlacementData> chunkObjectPlacementDataList;
                                if (chunk.PrefabPlacements.TryFindItem(
                                    x => x.PrefabAssetUrlListIndex == assetUrlListIndex,
                                    out var prefabPlacementData))
                                {
                                    chunkObjectPlacementDataList = prefabPlacementData.Placements;
                                }
                                else
                                {
                                    var prefabObjectPlacementData = new PrefabObjectPlacementsData
                                    {
                                        PrefabAssetUrlListIndex = assetUrlListIndex,
                                        Placements = []
                                    };
                                    chunk.PrefabPlacements.Add(prefabObjectPlacementData);
                                    chunkObjectPlacementDataList = prefabObjectPlacementData.Placements;
                                }

                                var objPlacementData = placementData.ToObjectPlacementData();
                                chunkObjectPlacementDataList.Add(objPlacementData);
                            }
                        }
                        else
                        {
                            throw new NotImplementedException($"Unhandled spawner data type: {spawnerData.GetType().Name}");
                        }
                    }
                }

                // Serialize all chunk data to disk as separate content
                var chunkIndexToChunkDataUrlMap = new Dictionary<TerrainChunkIndex2d, string>();
                lock (ObjectPlacementsChunkData.SerializerLock)
                {
                    foreach (var kv in chunkIndexToChunkDataMap)
                    {
                        var (chunkIndex, chunkData) = kv;
                        var chunkDataUrl = $"{Url}_ChunkData_({chunkIndex.X},{chunkIndex.Z})";
                        commandContext.AddTag(new ObjectUrl(UrlType.Content, chunkDataUrl), Builder.DoNotCompressTag);

                        using var outputStream = MicrothreadLocalDatabases.DatabaseFileProvider.OpenStream(
                            chunkDataUrl, VirtualFileMode.Create, VirtualFileAccess.Write, VirtualFileShare.Read, StreamFlags.Seekable);
                        var writer = new BinarySerializationWriter(outputStream);
                        chunkData.Serialize(writer);

                        chunkIndexToChunkDataUrlMap.Add(chunkIndex, chunkDataUrl);

                        logger.Info($"{Url}: ObjectPlacementMap chunk data saved at: {chunkDataUrl}");
                    }
                }
                logger.Info($"{Url}: ObjectPlacementMap chunk count: {chunkIndexToChunkDataUrlMap.Count}");

                var modelAssetUrlRefList = new List<UrlReference<Model>>();
                var prefabAssetUrlRefList = new List<UrlReference<Prefab>>();
                foreach (var url in modelAssetUrlList)
                {
                    var asset = AssetFinder.FindAsset(url);
                    modelAssetUrlRefList.Add(new UrlReference<Model>(asset?.Id ?? default, url));
                }
                foreach (var url in prefabAssetUrlList)
                {
                    var asset = AssetFinder.FindAsset(url);
                    prefabAssetUrlRefList.Add(new UrlReference<Prefab>(asset?.Id ?? default, url));
                }
                var result = new ObjectPlacementMap
                {
                    ObjectPlacementMapAssetId = objPlacementMapAsset.Id,
                    TerrainMapAssetId = terrainMapAsset?.Id,
                    ModelAssetUrlRefList = modelAssetUrlRefList,
                    PrefabAssetUrlRefList = prefabAssetUrlRefList,
                    ChunkIndexToChunkDataUrlMap = chunkIndexToChunkDataUrlMap,
                };
                var assetManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);
                assetManager.Save(Url, result);
            }

            return Task.FromResult(ResultStatus.Successful);

            static int[] GetOrCreateSpawnListIndexToAssetUrlListIndex(List<ObjectSpawnAssetDefinition> spawnAssetDefinitionList, List<string> assetUrlList)
            {
                var spawnAssetDefListIndexToAssetUrlListIndex = new int[spawnAssetDefinitionList.Count];

                for (int i = 0; i < spawnAssetDefinitionList.Count; i++)
                {
                    var spawnAssetDef = spawnAssetDefinitionList[i];
                    var assetUrl = spawnAssetDef.AssetUrl;
                    if (!string.IsNullOrEmpty(assetUrl))
                    {
                        if (!assetUrlList.TryFindIndex(assetUrl, StringComparer.OrdinalIgnoreCase, out int assetUrlListIndex))
                        {
                            assetUrlList.Add(assetUrl);
                            assetUrlListIndex = assetUrlList.Count - 1;
                        }
                        spawnAssetDefListIndexToAssetUrlListIndex[i] = assetUrlListIndex;
                    }
                    else
                    {
                        spawnAssetDefListIndexToAssetUrlListIndex[i] = -1;
                    }
                }

                return spawnAssetDefListIndexToAssetUrlListIndex;
            }
        }
    }
}
