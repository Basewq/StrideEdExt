using Stride.Assets.Entities;
using Stride.Assets.Materials;
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
        yield return new BuildDependencyInfo(typeof(MaterialAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
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
        asset.TotalObjectPlacementCount = 0;

        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new ObjectPlacementMapAssetCommand(targetUrlInStorage, asset, assetItem.Package, assetItem.Package.FullPath));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Serialization", "STRDIAG010:Invalid Constructor", Justification = "Not required")]
    private class ObjectPlacementMapAssetCommand : AssetCommand<ObjectPlacementMapAsset>
    {
        private UFile _assetPackageFullPath;

        public ObjectPlacementMapAssetCommand(string url, ObjectPlacementMapAsset parameters, IAssetFinder assetFinder, UFile assetPackageFullPath)
            : base(url, parameters, assetFinder)
        {
            Version = 1;

            _assetPackageFullPath = assetPackageFullPath;
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            // Converts the 'asset' object into the real 'definition' object which will be serialised.
            var logger = commandContext.Logger;
            var objPlacementMapAsset = Parameters;

            // Get the assigned Terrain Map Asset
            TerrainMapAsset? terrainMapAsset = null;
            Size2 terrainMapTextureSize = Size2.Zero;
            Vector2 chunkIndexToPos = Vector2.Zero;
            Vector2 posToChunkIndex = Vector2.Zero;
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

            string? resourceFolderFullPath = null;
            if (objPlacementMapAsset.ResourceFolderPath is not null)
            {
                var packageFolderPath = _assetPackageFullPath.GetFullDirectory();
                objPlacementMapAsset.EnsureFinalizeContentDeserialization(logger, packageFolderPath);

                resourceFolderFullPath = UPath.Combine(packageFolderPath, objPlacementMapAsset.ResourceFolderPath)?.ToOSPath();
            }

            var chunkIndexToChunkDataMap = new Dictionary<TerrainChunkIndex2d, ObjectPlacementsChunkData>();

            var modelAssetUrlList = objPlacementMapAsset.ModelAssetUrlList;
            var prefabAssetUrlList = objPlacementMapAsset.PrefabAssetUrlList;

            if (objPlacementMapAsset.SpawnerDataList is not null)
            {
                foreach (var spawnerData in objPlacementMapAsset.SpawnerDataList)
                {
                    if (resourceFolderFullPath is not null
                        && spawnerData is ObjectPlacementLayerDataBase layerData
                        && layerData.IsDeserializeIntermediateFileRequired)
                    {
                        layerData.DeserializeIntermediateFile(resourceFolderFullPath, objPlacementMapAsset, default, logger);
                    }
                    if (spawnerData is ModelInstancingSpawnerData modelInstancingSpawnerData)
                    {
                        logger.Info($"Generating {spawnerData.GetType().Name} objects: ObjectCount: {spawnerData.SpawnPlacementDataList.Count} - ModelType: {modelInstancingSpawnerData.ModelType}");

                        var objectPlacementDataList = modelInstancingSpawnerData.SpawnPlacementDataList;
                        foreach (var placementData in objectPlacementDataList)
                        {
                            var chunkIndex = TerrainChunkIndex2d.ToChunkIndex(placementData.Position, posToChunkIndex);
                            var chunk = chunkIndexToChunkDataMap.GetOrCreateValue(chunkIndex, x => new ObjectPlacementsChunkData());

                            int assetUrlListIndex = placementData.AssetUrlListIndex;
                            if (assetUrlListIndex < 0 || assetUrlListIndex >= modelAssetUrlList.Count)
                            {
                                logger.Warning($"objPlacementMapAsset.ModelAssetUrlList out of bounds: Index: {placementData.AssetUrlListIndex} - modelAssetUrlList.Count: {modelAssetUrlList.Count}");
                                continue;
                            }
                            List<ObjectPlacementData> chunkObjectPlacementDataList;
                            if (chunk.ModelPlacements.TryFindIndex(
                                x => x.ModelAssetUrlListIndex == assetUrlListIndex && x.ModelType == modelInstancingSpawnerData.ModelType,
                                out int modelPlacementDataIndex))
                            {
                                chunkObjectPlacementDataList = chunk.ModelPlacements[modelPlacementDataIndex].Placements;
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
                        logger.Info($"Generating {spawnerData.GetType().Name} objects: ObjectCount: {spawnerData.SpawnPlacementDataList.Count}");
                        var spawnAssetDefinitionList = prefabSpawnerData.SpawnAssetDefinitionList;
                        var spawnListIndexToAssetUrlListIndex = GetOrCreateSpawnListIndexToAssetUrlListIndex(spawnAssetDefinitionList, prefabAssetUrlList);

                        var objectPlacementDataList = prefabSpawnerData.SpawnPlacementDataList;
                        foreach (var placementData in objectPlacementDataList)
                        {
                            var chunkIndex = TerrainChunkIndex2d.ToChunkIndex(placementData.Position, posToChunkIndex);
                            var chunk = chunkIndexToChunkDataMap.GetOrCreateValue(chunkIndex, x => new ObjectPlacementsChunkData());

                            //if (!spawnListIndexToAssetUrlListIndex.TryGetValue(placementData.AssetUrlListIndex, out int assetUrlListIndex))
                            //{
                            //    logger.Warning($"placementData.AssetUrlListIndex out of bounds: Index: {placementData.AssetUrlListIndex} - spawnListIndexToAssetUrlListIndex.Length: {spawnListIndexToAssetUrlListIndex.Length}");
                            //    continue;
                            //}
                            int assetUrlListIndex = placementData.AssetUrlListIndex;
                            if (assetUrlListIndex < 0 || assetUrlListIndex >= prefabAssetUrlList.Count)
                            {
                                logger.Warning($"objPlacementMapAsset.PrefabAssetUrlList out of bounds: Index: {placementData.AssetUrlListIndex} - prefabAssetUrlList.Count: {prefabAssetUrlList.Count}");
                                continue;
                            }
                            List<ObjectPlacementData> chunkObjectPlacementDataList;
                            if (chunk.PrefabPlacements.TryFindIndex(
                                x => x.PrefabAssetUrlListIndex == assetUrlListIndex,
                                out int prefabPlacementDataIndex))
                            {
                                chunkObjectPlacementDataList = chunk.PrefabPlacements[prefabPlacementDataIndex].Placements;
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
                    var chunkIndex = kv.Key;
                    var chunkData = kv.Value;
                    var chunkDataUrl = $"{Url}_ChunkData_({chunkIndex.X},{chunkIndex.Z})";
                    commandContext.AddTag(new ObjectUrl(UrlType.Content, chunkDataUrl), Builder.DoNotCompressTag);

                    using var outputStream = MicrothreadLocalDatabases.DatabaseFileProvider.OpenStream(
                        chunkDataUrl, VirtualFileMode.Create, VirtualFileAccess.Write, VirtualFileShare.Read, StreamFlags.Seekable);
                    var writer = new BinarySerializationWriter(outputStream);
                    chunkData.Serialize(writer);

                    chunkIndexToChunkDataUrlMap.Add(chunkIndex, chunkDataUrl);

                    logger.Info($"ObjectPlacementMap chunk data saved at: {chunkDataUrl}");
                }
            }
            logger.Info($"ObjectPlacementMap chunk count: {chunkIndexToChunkDataUrlMap.Count}");

            var result = new ObjectPlacementMap
            {
                ObjectPlacementMapAssetId = objPlacementMapAsset.Id,
                TerrainMapAssetId = terrainMapAsset?.Id,
                //ChunkSize = Parameters.ChunkSize.ToSize2(),
                ModelAssetUrlRefList = modelAssetUrlList.Select(url => new UrlReference<Model>(url)).ToList(),
                PrefabAssetUrlRefList = prefabAssetUrlList.Select(url => new UrlReference<Prefab>(url)).ToList(),
                ChunkIndexToChunkDataUrlMap = chunkIndexToChunkDataUrlMap,
            };
            var assetManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            assetManager.Save(Url, result);

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
