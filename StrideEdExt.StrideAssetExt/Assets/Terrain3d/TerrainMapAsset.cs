using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
using Stride.Core.Collections;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

/**
 * The asset as seen by Game Studio.
 * Refer to Stride's source could for additional asset options, eg. referencing a file.
 */
[DataContract]
[AssetDescription(".gttrr")]
[AssetContentType(typeof(TerrainMap))]
//[CategoryOrder(1000, "Terrain")]
[AssetFormatVersion(StrideEdExtConfig.PackageName, CurrentVersion)]
//[AssetUpgrader(StrideEdExtConfig.PackageName, "0.0.0.1", "1.0.0.0", typeof(TerrainMapAssetUpgrader))]    // Can be used to update an old asset format to a new format.
[Display(10000, "Terrain Map 3D")]
public class TerrainMapAsset : Asset
{
    private const string CurrentVersion = "0.0.0.1";

    private const string CategoryName_MapSettings = "Map Settings";

    private const string IntermediateHeightmapFileName = "terrain_heightmap.txt";
    private const string IntermediateMaterialIndexMapFileName = "terrain_materialmap.txt";

    private readonly object _serializationLock = new();

    public static string HeightmapLayerDataListName => nameof(HeightmapLayerDataList);    // HACK: we need the editor to detect changes to list in the editor, but we don't want to expose this list directly.
    public static string MaterialWeightMapLayerDataListName => nameof(MaterialWeightMapLayerDataList);    

    /// <summary>
    /// Normalized heightmap data.
    /// This data is loaded from its intermediate file and serialized to its run-time version <see cref="TerrainMap.HeightmapData"/>.
    /// </summary>
    [DataMemberIgnore]
    public Array2d<float>? HeightmapData { get; set; }
    /// <summary>
    /// Material map data. Map size is 1-1 with <see cref="TerrainMap.HeightmapData"/>.
    /// This data is loaded from its intermediate file and serialized as a texture in its run-time version <see cref="TerrainMap.MaterialIndexMapTexture"/>.
    /// </summary>
    [DataMemberIgnore]
    public Array2d<byte>? MaterialIndexMapData { get; set; }
    [DataMemberIgnore]
    public Array2d<Half>? MaterialWeightMapData { get; set; }

    private readonly List<TerrainMapLayerDataBase> _pendingRemovalLayerDataList = [];
    private TrackingCollection<TerrainHeightmapLayerDataBase>? _heightmapLayerDataList;
    [Display(Browsable = false)]
    [DataMember]
    internal TrackingCollection<TerrainHeightmapLayerDataBase>? HeightmapLayerDataList
    {
        get => _heightmapLayerDataList;
        set
        {
            if (_heightmapLayerDataList is not null)
            {
                _heightmapLayerDataList.CollectionChanged -= OnLayerCollectionChanged;
            }
            _heightmapLayerDataList = value;
            if (_heightmapLayerDataList is not null)
            {
                _heightmapLayerDataList.CollectionChanged += OnLayerCollectionChanged;
            }
        }
    }

    private TrackingCollection<TerrainMaterialMapLayerDataBase>? _materialWeightMapLayerDataList;
    [Display(Browsable = false)]
    [DataMember]
    internal TrackingCollection<TerrainMaterialMapLayerDataBase>? MaterialWeightMapLayerDataList
    {
        get => _materialWeightMapLayerDataList;
        set
        {
            if (_materialWeightMapLayerDataList is not null)
            {
                _materialWeightMapLayerDataList.CollectionChanged -= OnLayerCollectionChanged;
            }
            _materialWeightMapLayerDataList = value;
            if (_materialWeightMapLayerDataList is not null)
            {
                _materialWeightMapLayerDataList.CollectionChanged += OnLayerCollectionChanged;
            }
        }
    }

    private void OnLayerCollectionChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            var layerData = (TerrainMapLayerDataBase)e.Item!;
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    [Display(Browsable = false)]
    [DataMember]
    internal DateTime? LastUserModifiedDateTimeUtc { get; set; }

    [DataMemberIgnore]
    public bool HasTerrainMapLayerSerializedIntermediateFiles { get; set; }

    /// <remarks>
    /// Folder path should be unique for each <see cref="TerrainMapAsset"/> due to hardcoded file names for generated intermediate files.
    /// </remarks>
    [UPath(UPathRelativeTo.Package)]
    public UDirectory? ResourceFolderPath { get; set; }

    /// <inheritdoc cref="TerrainMap.MapSize"/>
    ///<remarks>
    /// This data is loaded from its intermediate file and serialized to its run-time version <see cref="TerrainMap"/>.
    /// </remarks>
    [DataMemberIgnore]
    public Size2 MapSize => (HeightmapTextureSize - Int2.One).ToSize2();

    /// <inheritdoc cref="TerrainMap.HeightmapTextureSize"/>
    [Display(category: CategoryName_MapSettings)]
    [DataMemberRange(minimum: 2, maximum: 4096, smallStep: 8, largeStep: 32, decimalPlaces: 0)]
    public Int2 HeightmapTextureSize { get; set; } = new Int2(64, 64);      // HACK: Int2 instead of Size2 because the editor UI doesn't have an inline version of Size2 control

    /// <inheritdoc cref="TerrainMap.QuadsPerMesh"/>
    [Display(category: CategoryName_MapSettings)]
    [DataMemberRange(minimum: 1, maximum: 4095, smallStep: 2, largeStep: 8, decimalPlaces: 0)]
    public Int2 QuadsPerMesh { get; set; } = new Int2(16, 16);

    /// <summary>
    /// The size of a single terrain mesh quad.
    /// </summary>
    [Display(category: CategoryName_MapSettings)]
    public Vector2 MeshQuadSize { get; set; } = Vector2.One;

    [Display(category: CategoryName_MapSettings)]
    public TerrainMeshPerChunk MeshPerChunk { get; set; } = TerrainMeshPerChunk.Count4x4;

    [Display(category: CategoryName_MapSettings)]
    public Vector2 HeightRange { get; set; } = new(-100, 100);

    public TerrainMaterial? TerrainMaterial { get; set; }

    public TerrainMapAsset()
    {
        HeightmapLayerDataList = [];
        MaterialWeightMapLayerDataList = [];
    }

    public bool TryGetHeightmapLayerData<TLayerData>(Guid layerId, [NotNullWhen(true)] out TLayerData? layerData)
        where TLayerData : TerrainHeightmapLayerDataBase
    {
        layerData = null;
        if (HeightmapLayerDataList is not null
            && HeightmapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            layerData = HeightmapLayerDataList[index] as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetMaterialMapLayerData<TLayerData>(Guid layerId, [NotNullWhen(true)] out TLayerData? layerData)
        where TLayerData : TerrainMaterialMapLayerDataBase
    {
        layerData = null;
        if (MaterialWeightMapLayerDataList is not null
            && MaterialWeightMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            layerData = MaterialWeightMapLayerDataList[index] as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetLayerData(Guid layerId, [NotNullWhen(true)] out TerrainMapLayerDataBase? layerData)
    {
        layerData = null;
        if (HeightmapLayerDataList is not null
            && HeightmapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            layerData = HeightmapLayerDataList[index];
        }
        else if (MaterialWeightMapLayerDataList is not null
            && MaterialWeightMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out index))
        {
            layerData = MaterialWeightMapLayerDataList[index];
        }
        return layerData is not null;
    }

    public TLayerData GetOrCreateLayerData<TLayerData>(Guid layerId)
        where TLayerData : TerrainMapLayerDataBase, new()
    {
        if (typeof(TLayerData).IsAssignableTo(typeof(TerrainHeightmapLayerDataBase)))
        {
            var layerData = GetOrCreateHeightmapLayerData(layerId, typeof(TLayerData));
            return (layerData as TLayerData)!;
        }
        else if (typeof(TLayerData).IsAssignableTo(typeof(TerrainMaterialMapLayerDataBase)))
        {
            var layerData = GetOrCreateMaterialMapLayerData(layerId, typeof(TLayerData));
            return (layerData as TLayerData)!;
        }
        else
        {
            throw new NotSupportedException($"Unhandled layer type: {typeof(TLayerData).Name}");
        }
    }

    public TerrainMapLayerDataBase GetOrCreateLayerData(Guid layerId, Type layerDataType)
    {
        if (layerDataType.IsAssignableTo(typeof(TerrainHeightmapLayerDataBase)))
        {
            var layerData = GetOrCreateHeightmapLayerData(layerId, layerDataType);
            return layerData;
        }
        else if (layerDataType.IsAssignableTo(typeof(TerrainMaterialMapLayerDataBase)))
        {
            var layerData = GetOrCreateMaterialMapLayerData(layerId, layerDataType);
            return layerData;
        }
        else
        {
            throw new NotSupportedException($"Unhandled layer type: {layerDataType.Name}");
        }
    }

    private TerrainHeightmapLayerDataBase GetOrCreateHeightmapLayerData(Guid layerId, Type layerDataType)
    {
        HeightmapLayerDataList ??= [];
        if (HeightmapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            var layerData = HeightmapLayerDataList[index];
            return layerData;
        }
        else
        {
            var layerData = _pendingRemovalLayerDataList.Find(x => x.LayerId == layerId) as TerrainHeightmapLayerDataBase;
            if (layerData is not null)
            {
                // Add back to active list
                _pendingRemovalLayerDataList.Remove(layerData);
            }
            else
            {
                layerData = (TerrainHeightmapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            HeightmapLayerDataList.Add(layerData!);
            return layerData;
        }
    }

    private TerrainMaterialMapLayerDataBase GetOrCreateMaterialMapLayerData(Guid layerId, Type layerDataType)
    {
        MaterialWeightMapLayerDataList ??= [];
        if (MaterialWeightMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            var layerData = MaterialWeightMapLayerDataList[index];
            return layerData;
        }
        else
        {
            var layerData = _pendingRemovalLayerDataList.Find(x => x.LayerId == layerId) as TerrainMaterialMapLayerDataBase;
            if (layerData is not null)
            {
                // Add back to active list
                _pendingRemovalLayerDataList.Remove(layerData);
            }
            else
            {
                layerData = (TerrainMaterialMapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsSerializeIntermediateFileRequired = true;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            MaterialWeightMapLayerDataList.Add(layerData!);
            return layerData;
        }
    }

    public bool TryRemoveLayerData(Guid layerId)
    {
        if (HeightmapLayerDataList is not null
            && HeightmapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            HeightmapLayerDataList.RemoveAt(index);
            return true;
        }
        else if (MaterialWeightMapLayerDataList is not null
            && MaterialWeightMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out index))
        {
            MaterialWeightMapLayerDataList.RemoveAt(index);
            return true;
        }
        return false;
    }

    public void SetHeightmapLayerOrdering(List<Guid> heightmapLayerIds)
    {
        HeightmapLayerDataList ??= [];
        var layerIdToLayerData = HeightmapLayerDataList.ToDictionary(x => x.LayerId);
        HeightmapLayerDataList.Clear();
        foreach (var layerId in heightmapLayerIds)
        {
            if (!layerIdToLayerData.TryGetValue(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int removalListIndex))
                {
                    layerData = (TerrainHeightmapLayerDataBase)_pendingRemovalLayerDataList[removalListIndex];

                    // Add back to active list
                    _pendingRemovalLayerDataList.RemoveAt(removalListIndex);
                }
                else
                {
                    throw new InvalidOperationException($"Layer not found: {layerId}");
                }
            }
            HeightmapLayerDataList.Add(layerData);
        }
        // Any remainder is removed
        foreach (var (_, layerData) in layerIdToLayerData)
        {
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    public void SetMaterialWeightMapLayerOrdering(List<Guid> materialWeightMapLayerIds)
    {
        MaterialWeightMapLayerDataList ??= [];
        var layerIdToLayerData = MaterialWeightMapLayerDataList.ToDictionary(x => x.LayerId);
        MaterialWeightMapLayerDataList.Clear();
        foreach (var layerId in materialWeightMapLayerIds)
        {
            if (!layerIdToLayerData.TryGetValue(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int removalListIndex))
                {
                    layerData = (TerrainMaterialMapLayerDataBase)_pendingRemovalLayerDataList[removalListIndex];

                    // Add back to active list
                    _pendingRemovalLayerDataList.RemoveAt(removalListIndex);
                }
                else
                {
                    throw new InvalidOperationException($"Layer not found: {layerId}");
                }
            }
            MaterialWeightMapLayerDataList.Add(layerData);
        }
        // Any remainder is removed
        foreach (var (_, layerData) in layerIdToLayerData)
        {
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    private bool _hasCalledFinalizeContentDeserialization = false;
    public void EnsureFinalizeContentDeserialization(ILogger logger, UDirectory packageFolderPath)
    {
        lock (_serializationLock)
        {
            if (_hasCalledFinalizeContentDeserialization)
            {
                return;
            }

            if (ResourceFolderPath is not null)
            {
                var resourceFolderFullPath = UPath.Combine(packageFolderPath, ResourceFolderPath)?.ToOSPath();
                if (resourceFolderFullPath is not null)
                {
                    var expectedDataSize = HeightmapTextureSize.ToSize2();
                    Directory.CreateDirectory(resourceFolderFullPath);
                    if (HeightmapData is null)
                    {
                        string heightmapFilePath = Path.Combine(resourceFolderFullPath, IntermediateHeightmapFileName);
                        logger.Info($"Deserializing heightmap: {heightmapFilePath}");
                        if (File.Exists(heightmapFilePath))
                        {
                            if (HeightmapSerializationHelper.TryDeserializeFloatArray2dFromHexFile(heightmapFilePath, out var heightmapData, out var errorMessage))
                            {
                                if (heightmapData.Length2d != expectedDataSize)
                                {
                                    logger.Warning($"Deserialized heightmap size mismatch with asset map size: RawFile: {heightmapData.Length2d} - Asset: {expectedDataSize}. Resizing the array.");
                                    heightmapData.Resize(expectedDataSize);
                                }
                                HeightmapData = heightmapData;
                                Debug.WriteLineIf(condition: true, $"Deserialized heightmap size: {HeightmapData.Length2d}");
                                logger.Info($"Deserialized heightmap size: {HeightmapData.Length2d}");
                            }
                            else
                            {
                                logger.Error($"Failed to deserialize intermediate heightmap file: {heightmapFilePath}\r\n{errorMessage}");
                            }
                        }
                        else
                        {
                            logger.Info($"Heightmap file does not exist at path: {heightmapFilePath}");
                        }
                    }
                    if (MaterialIndexMapData is null)
                    {
                        string materialIndexMapFilePath = Path.Combine(resourceFolderFullPath, IntermediateMaterialIndexMapFileName);
                        logger.Info($"Deserializing material index map: {materialIndexMapFilePath}");
                        if (File.Exists(materialIndexMapFilePath))
                        {
                            if (HeightmapSerializationHelper.TryDeserializeByteArray2dFromHexFile(materialIndexMapFilePath, out var materialIndexMapData, out var errorMessage))
                            {
                                if (materialIndexMapData.Length2d != expectedDataSize)
                                {
                                    logger.Warning($"Deserialized material index map size mismatch with asset map size: RawFile: {materialIndexMapData.Length2d} - Asset: {expectedDataSize}. Resizing the array.");
                                    materialIndexMapData.Resize(expectedDataSize);
                                }
                                MaterialIndexMapData = materialIndexMapData;
                                logger.Info($"Deserialized material index map size: {MaterialIndexMapData.Length2d}");
                            }
                            else
                            {
                                logger.Error($"Failed to deserialize intermediate material index map file: {materialIndexMapFilePath}\r\n{errorMessage}");
                            }
                        }
                        else
                        {
                            logger.Info($"Material index map file does not exist at path: {materialIndexMapFilePath}");
                        }
                    }

                    //foreach (var layerData in LayerDataList)
                    //{
                    //    layerData.DeserializeIntermediateFile(resourceFolderFullPath, this, logger);
                    //}
                }
            }

            //var chunkItemIdSerializationFixer = GetChunkItemIdSerializationFixer();
            //chunkItemIdSerializationFixer.TrackIds();
            //foreach (var chunk in Chunks)
            //{
            //    chunk.FinalizeDeserialization();
            //}

            _hasCalledFinalizeContentDeserialization = true;
        }
    }

    public void PrepareContentSerialization()
    {
        lock (_serializationLock)
        {
            //var resourceFolderPath = ResourceFolderPath?.ToOSPath();
            //if (resourceFolderPath is not null && HeightmapData is not null)
            //{
            //    Directory.CreateDirectory(resourceFolderPath);
            //    string heightmapRawFilePath = Path.Combine(resourceFolderPath, HeightmapRawFileName);

            //    HeightmapSerializationHelper.SerializeRawToFile(heightmapRawFilePath, HeightmapData);
            //}


            //    bool doFixId = false;
            //    var chunkItemIdSerializationFixer = GetChunkItemIdSerializationFixer();
            //    chunkItemIdSerializationFixer.TrackIds();
            //    if (chunkAsset is not null)
            //    {
            //        //bool isNotEmpty = chunkAsset.HasNonEmptyTile();
            //        //if (isNotEmpty)
            //        {
            //            chunkAsset.PrepareSerialization();
            //        }
            //        //else
            //        //{
            //        //    doFixId = TryRemoveChunkAssetInternal(chunkAsset.ChunkIndex);
            //        //}
            //    }
            //    else
            //    {
            //        for (int i = Chunks.Count - 1; i >= 0; i--)
            //        {
            //            var chAsset = Chunks[i];
            //            //bool isNotEmpty = chAsset.HasNonEmptyTile();
            //            //if (isNotEmpty)
            //            {
            //                chAsset.PrepareSerialization();
            //            }
            //            //else
            //            //{
            //            //    Chunks.RemoveAt(i);
            //            //    doFixId = true;
            //            //}
            //        }
            //    }
            //    if (doFixId)
            //    {
            //        chunkItemIdSerializationFixer.FixIds();
            //    }
        }
    }

    public void SerializeIntermediateFiles(ILogger logger, UDirectory packageFolderPath)
    {
        lock (_serializationLock)
        {
            if (ResourceFolderPath is not null)
            {
                var resourceFolderFullPath = UPath.Combine(packageFolderPath, ResourceFolderPath)?.ToOSPath();
                if (resourceFolderFullPath is not null)
                {
                    Directory.CreateDirectory(resourceFolderFullPath);
                    if (HeightmapData is not null)
                    {
                        string heightmapFilePath = Path.Combine(resourceFolderFullPath, IntermediateHeightmapFileName);
                        HeightmapSerializationHelper.SerializeFloatArray2dToHexFile(HeightmapData, heightmapFilePath);
                    }
                    if (MaterialIndexMapData is not null)
                    {
                        string materialMapFilePath = Path.Combine(resourceFolderFullPath, IntermediateMaterialIndexMapFileName);
                        HeightmapSerializationHelper.SerializeByteArray2dToHexFile(MaterialIndexMapData, materialMapFilePath);
                    }

                    if (HeightmapLayerDataList is not null)
                    {
                        foreach (var layerData in HeightmapLayerDataList)
                        {
                            if (layerData.IsSerializeIntermediateFileRequired)
                            {
                                layerData.SerializeIntermediateFile(resourceFolderFullPath, this, logger);
                                layerData.LastModifiedIntermediateFile = DateTime.UtcNow;
                                layerData.IsSerializeIntermediateFileRequired = false;
                                HasTerrainMapLayerSerializedIntermediateFiles = true;
                            }
                        }
                    }
                    if (MaterialWeightMapLayerDataList is not null)
                    {
                        foreach (var layerData in MaterialWeightMapLayerDataList)
                        {
                            if (layerData.IsSerializeIntermediateFileRequired)
                            {
                                layerData.SerializeIntermediateFile(resourceFolderFullPath, this, logger);
                                layerData.LastModifiedIntermediateFile = DateTime.UtcNow;
                                layerData.IsSerializeIntermediateFileRequired = false;
                                HasTerrainMapLayerSerializedIntermediateFiles = true;
                            }
                        }
                    }
                }
            }
        }
    }

    public void TerrainPropertiesCopyTo(TerrainMap terrainMap) => TerrainPropertiesCopyToInternal(terrainMap, reuseHeightmapData: false);

    private void TerrainPropertiesCopyToInternal(TerrainMap terrainMap, bool reuseHeightmapData)
    {
        terrainMap.TerrainMapAssetId = Id;
        terrainMap.HeightmapTextureSize = HeightmapTextureSize.ToSize2();
        terrainMap.QuadsPerMesh = QuadsPerMesh;
        terrainMap.MeshQuadSize = MeshQuadSize;
        terrainMap.MeshPerChunk = MeshPerChunk;
        terrainMap.HeightRange = HeightRange;

        if (reuseHeightmapData)
        {
            terrainMap.HeightmapData = HeightmapData;
        }
        else if (HeightmapData is not null)
        {
            if (terrainMap.HeightmapData is Array2d<float> destHeightmapData)
            {
                if (destHeightmapData.Length2d != HeightmapData.Length2d)
                {
                    destHeightmapData.Resize(HeightmapData.Length2d);
                }
            }
            else
            {
                destHeightmapData = new(HeightmapTextureSize);
                terrainMap.HeightmapData = destHeightmapData;
            }
            HeightmapData.CopyToAligned(destHeightmapData);
        }
    }

    public TerrainMap ToTerrainMap(ILogger? logger = null)
    {
        lock (_serializationLock)
        {
            var terrainMap = new TerrainMap();
            TerrainPropertiesCopyToInternal(terrainMap, reuseHeightmapData: true);
            terrainMap.TerrainMaterial = TerrainMaterial;
            return terrainMap;
        }
    }
}
