using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
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
public class TerrainMapAsset : Asset, IStrideCustomAsset
{
    private const string CurrentVersion = "0.0.0.1";

    private const string CategoryName_MapSettings = "Map Settings";

    private const string IntermediateHeightmapFileName = "terrain_heightmap.txt";
    private const string IntermediateMaterialIndexMapFileName = "terrain_materialmap.txt";

    /// <summary>
    /// The full file path of the asset if it was deserialized from a file.
    /// </summary>
    internal string? OriginalAssetFullFilePath;
    /// <summary>
    /// Full file path of the asset. This is only applicable for serialization since
    /// this is only set in OnAssetLoaded or TerrainMapAssetViewModel.Initialize (after the asset has been loaded).
    /// </summary>
    internal string? AssetFullFilePath;
    /// <summary>
    /// Path relative to <see cref="OriginalAssetFullFilePath"/>.
    /// </summary>
    internal string? OriginalResourceRelativeFolderPath;

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

    /// <summary>
    /// List must not be directly modified externally.
    /// Use <see cref="GetOrCreateLayerData"/> or <see cref="GetOrCreateHeightmapLayerData"/>,  and <see cref="TryRemoveLayerData"/>.
    /// </summary>
    [Display(Browsable = false)]
    [DataMember]
    internal List<TerrainHeightmapLayerDataBase> HeightmapLayerDataList { get; } = [];

    /// <summary>
    /// List must not be directly modified externally.
    /// Use <see cref="GetOrCreateLayerData"/> or <see cref="GetOrCreateMaterialMapLayerData"/>,  and <see cref="TryRemoveLayerData"/>.
    /// </summary>
    [Display(Browsable = false)]
    [DataMember]
    internal List<TerrainMaterialMapLayerDataBase> MaterialWeightMapLayerDataList { get; } = [];

    [Display(Browsable = false)]
    [DataMember]
    internal DateTimeOffset? LastUserModifiedDateTimeUtc { get; set; }

    /// <remarks>
    /// Folder path should be unique for each <see cref="TerrainMapAsset"/> due to hardcoded file names for generated intermediate files.
    /// Path relative to <see cref="AssetFullFilePath"/>.
    /// </remarks>
    public UDirectory? ResourceRelativeFolderPath { get; set; }

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

    /// <inheritdoc cref="TerrainMap.MeshQuadSize"/>
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
        if (HeightmapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var hmLayerData))
        {
            layerData = hmLayerData as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetMaterialMapLayerData<TLayerData>(Guid layerId, [NotNullWhen(true)] out TLayerData? layerData)
        where TLayerData : TerrainMaterialMapLayerDataBase
    {
        layerData = null;
        if (MaterialWeightMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var wmLayerData))
        {
            layerData = wmLayerData as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetLayerData(Guid layerId, [NotNullWhen(true)] out TerrainMapLayerDataBase? layerData)
    {
        layerData = null;
        if (HeightmapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var hmLayerData))
        {
            layerData = hmLayerData;
        }
        else if (MaterialWeightMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var wmLayerData))
        {
            layerData = wmLayerData;
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
            throw new NotImplementedException($"Unhandled layer type: {typeof(TLayerData).Name}");
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
            throw new NotImplementedException($"Unhandled layer type: {layerDataType.Name}");
        }
    }

    private TerrainHeightmapLayerDataBase GetOrCreateHeightmapLayerData(Guid layerId, Type layerDataType)
    {
        if (HeightmapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var layerData))
        {
            return layerData;
        }
        else
        {
            if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
            {
                // Add back to active list
                layerData = (TerrainHeightmapLayerDataBase)restoredLayerData;
            }
            else
            {
                layerData = (TerrainHeightmapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsSerializeIntermediateFileRequired = true;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            HeightmapLayerDataList.Add(layerData);
            return layerData;
        }
    }

    private TerrainMaterialMapLayerDataBase GetOrCreateMaterialMapLayerData(Guid layerId, Type layerDataType)
    {
        if (MaterialWeightMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var layerData))
        {
            return layerData;
        }
        else
        {
            if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
            {
                // Add back to active list
                layerData = (TerrainMaterialMapLayerDataBase)restoredLayerData;
            }
            else
            {
                layerData = (TerrainMaterialMapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsSerializeIntermediateFileRequired = true;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            MaterialWeightMapLayerDataList.Add(layerData);
            return layerData;
        }
    }

    public bool TryRemoveLayerData(Guid layerId)
    {
        if (HeightmapLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var hmLayerData))
        {
            _pendingRemovalLayerDataList.Add(hmLayerData);
            return true;
        }
        else if (MaterialWeightMapLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var wmLayerData))
        {
            _pendingRemovalLayerDataList.Add(wmLayerData);
            return true;
        }
        return false;
    }

    public void SetHeightmapLayerOrdering(List<Guid> heightmapLayerIds)
    {
        var layerIdToLayerData = HeightmapLayerDataList.ToDictionary(x => x.LayerId);
        HeightmapLayerDataList.Clear();
        foreach (var layerId in heightmapLayerIds)
        {
            if (!layerIdToLayerData.Remove(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
                {
                    // Add back to active list
                    layerData = (TerrainHeightmapLayerDataBase)restoredLayerData;
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
        var layerIdToLayerData = MaterialWeightMapLayerDataList.ToDictionary(x => x.LayerId);
        MaterialWeightMapLayerDataList.Clear();
        foreach (var layerId in materialWeightMapLayerIds)
        {
            if (!layerIdToLayerData.Remove(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
                {
                    // Add back to active list
                    layerData = (TerrainMaterialMapLayerDataBase)restoredLayerData;
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

    private bool _hasCalledOnAssetLoaded = false;
    public void OnAssetLoaded(UFile assetFullFilePath, ILogger? logger)
    {
        if (_hasCalledOnAssetLoaded)
        {
            return;
        }
        _hasCalledOnAssetLoaded = true;
        OriginalAssetFullFilePath = assetFullFilePath.ToOSPath();
        AssetFullFilePath = OriginalAssetFullFilePath;

        if (string.IsNullOrWhiteSpace(AssetFullFilePath))
        {
            logger?.Error($"{nameof(TerrainMapAsset)} {Id}: '{nameof(AssetFullFilePath)}' was not assigned.");
            return;
        }

        var assetFullFolderPath = assetFullFilePath.GetFullDirectory();

        if (string.IsNullOrWhiteSpace(ResourceRelativeFolderPath?.FullPath))
        {
            logger?.Warning($"{nameof(TerrainMapAsset)} {Id}: '{nameof(ResourceRelativeFolderPath)}' was not assigned. Creating default path.");

            ResourceRelativeFolderPath = CreateDefaultResourceRelativeFolderPath(assetFullFilePath);
        }
        else
        {
            // Ensure path is relative to this asset's path
            ResourceRelativeFolderPath = ResourceRelativeFolderPath.MakeRelative(assetFullFolderPath);
            OriginalResourceRelativeFolderPath = ResourceRelativeFolderPath.FullPath;
        }

        string intermediateFilesFullFolderPath = UPath.Combine(assetFullFolderPath, ResourceRelativeFolderPath).ToOSPath();

        var expectedDataSize = HeightmapTextureSize.ToSize2();
        // Load HeightmapData
        {
            string heightmapFullFilePath = Path.Combine(intermediateFilesFullFolderPath, IntermediateHeightmapFileName);
            logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Deserializing heightmap: {heightmapFullFilePath}");
            if (File.Exists(heightmapFullFilePath))
            {
                if (HeightmapSerializationHelper.TryDeserializeFloatArray2dFromHexFile(heightmapFullFilePath, out var heightmapData, out var errorMessage))
                {
                    if (heightmapData.Length2d != expectedDataSize)
                    {
                        logger?.Warning($"{nameof(TerrainMapAsset)} {Id}: Deserialized heightmap size mismatch with asset map size: RawFile: {heightmapData.Length2d} - Asset: {expectedDataSize}. Resizing the array.");
                        heightmapData.Resize(expectedDataSize);
                    }
                    HeightmapData = heightmapData;
                    Debug.WriteLineIf(condition: true, $"Deserialized heightmap size: {HeightmapData.Length2d}");
                    logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Deserialized heightmap size: {HeightmapData.Length2d}");
                }
                else
                {
                    logger?.Error($"{nameof(TerrainMapAsset)} {Id}: Failed to deserialize intermediate heightmap file: {heightmapFullFilePath}\r\n{errorMessage}");
                }
            }
            else
            {
                logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Heightmap file does not exist at path: {heightmapFullFilePath}");
            }
        }
        // Load MaterialIndexMapData
        {
            string materialIndexMapFullFilePath = Path.Combine(intermediateFilesFullFolderPath, IntermediateMaterialIndexMapFileName);
            logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Deserializing material index map: {materialIndexMapFullFilePath}");
            if (File.Exists(materialIndexMapFullFilePath))
            {
                if (HeightmapSerializationHelper.TryDeserializeByteArray2dFromHexFile(materialIndexMapFullFilePath, out var materialIndexMapData, out var errorMessage))
                {
                    if (materialIndexMapData.Length2d != expectedDataSize)
                    {
                        logger?.Warning($"{nameof(TerrainMapAsset)} {Id}: Deserialized material index map size mismatch with asset map size: RawFile: {materialIndexMapData.Length2d} - Asset: {expectedDataSize}. Resizing the array.");
                        materialIndexMapData.Resize(expectedDataSize);
                    }
                    MaterialIndexMapData = materialIndexMapData;
                    logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Deserialized material index map size: {MaterialIndexMapData.Length2d}");
                }
                else
                {
                    logger?.Error($"{nameof(TerrainMapAsset)} {Id}: Failed to deserialize intermediate material index map file: {materialIndexMapFullFilePath}\r\n{errorMessage}");
                }
            }
            else
            {
                logger?.Info($"{nameof(TerrainMapAsset)} {Id}: Material index map file does not exist at path: {materialIndexMapFullFilePath}");
            }
        }

        // Note that layer data for HeightmapLayerDataList & MaterialWeightMapLayerDataList are lazy loaded
    }

    public void OnAssetSaving(ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(AssetFullFilePath))
        {
            logger?.Error($"{nameof(TerrainMapAsset)} {Id}: '{nameof(AssetFullFilePath)}' was not assigned.");
            return;
        }

        var assetFullFilePath = new UFile(AssetFullFilePath);
        var assetFullFolderPath = assetFullFilePath.GetFullDirectory();

        if (string.IsNullOrWhiteSpace(ResourceRelativeFolderPath?.FullPath))
        {
            logger?.Warning($"{nameof(TerrainMapAsset)} {Id}: '{nameof(ResourceRelativeFolderPath)}' was not assigned. Creating default path.");

            ResourceRelativeFolderPath = CreateDefaultResourceRelativeFolderPath(assetFullFilePath);
        }
        else if (!ResourceRelativeFolderPath.IsRelative)
        {
            // Ensure path is relative to this asset's path
            ResourceRelativeFolderPath = ResourceRelativeFolderPath.MakeRelative(assetFullFolderPath);
        }

        // Delete any old intermediate files due to moving the asset or changing ResourceRelativeFolderPath
        bool hasAssetOrResourceMoved = false;
        string pendingDeleteResourceRelativeFolderPath = ResourceRelativeFolderPath.FullPath;
        string pendingDeleteTerrainMapAssetFullFolderPath = AssetFullFilePath;
        if (!string.IsNullOrWhiteSpace(OriginalResourceRelativeFolderPath)
            && !string.Equals(OriginalResourceRelativeFolderPath, ResourceRelativeFolderPath.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            pendingDeleteResourceRelativeFolderPath = OriginalResourceRelativeFolderPath;
            hasAssetOrResourceMoved = true;
        }
        else if (!string.IsNullOrWhiteSpace(OriginalAssetFullFilePath)
            && !string.IsNullOrWhiteSpace(AssetFullFilePath)
            && !string.Equals(OriginalAssetFullFilePath, AssetFullFilePath, StringComparison.OrdinalIgnoreCase))
        {
            pendingDeleteTerrainMapAssetFullFolderPath = OriginalAssetFullFilePath;
            hasAssetOrResourceMoved = true;
        }

        var pendingDeleteResourceRelativeDir = new UDirectory(pendingDeleteResourceRelativeFolderPath);
        var pendingDeleteAssetFullFolderPath = new UFile(pendingDeleteTerrainMapAssetFullFolderPath).GetFullDirectory();
        var pendingDeleteIntermediateFilesFullFolderPath = UPath.Combine(pendingDeleteAssetFullFolderPath, pendingDeleteResourceRelativeDir);
        if (hasAssetOrResourceMoved)
        {
            // Delete intermediate files in old location
            string heightmapFullFilePath = UPath.Combine(pendingDeleteIntermediateFilesFullFolderPath, new UFile(IntermediateHeightmapFileName)).ToOSPath();
            _ = AssetExt.TryDeleteFile(heightmapFullFilePath, logger);
            string materialMapFullFilePath = UPath.Combine(pendingDeleteIntermediateFilesFullFolderPath, new UFile(IntermediateMaterialIndexMapFileName)).ToOSPath();
            _ = AssetExt.TryDeleteFile(materialMapFullFilePath, logger);

            // Delete layer intermediate files in old location
            if (HeightmapLayerDataList is not null)
            {
                foreach (var layerData in HeightmapLayerDataList)
                {
                    layerData.DeleteIntermediateFile(pendingDeleteIntermediateFilesFullFolderPath, pendingDeleteAssetFullFolderPath, logger);
                    layerData.IsSerializeIntermediateFileRequired = true;   // In case of undo, we may need to regenerate this file after deletion
                }
            }
            if (MaterialWeightMapLayerDataList is not null)
            {
                foreach (var layerData in MaterialWeightMapLayerDataList)
                {
                    layerData.DeleteIntermediateFile(pendingDeleteIntermediateFilesFullFolderPath, pendingDeleteAssetFullFolderPath, logger);
                    layerData.IsSerializeIntermediateFileRequired = true;   // In case of undo, we may need to regenerate this file after deletion
                }
            }
        }
        // Delete any old intermediate files from deleted layers
        foreach (var layerData in _pendingRemovalLayerDataList)
        {
            layerData.DeleteIntermediateFile(pendingDeleteIntermediateFilesFullFolderPath, pendingDeleteAssetFullFolderPath, logger);
            layerData.IsSerializeIntermediateFileRequired = true;   // In case of undo, we may need to regenerate this file after deletion
        }

        // Save intermediate files
        string intermediateFilesFullFolderPath = UPath.Combine(assetFullFolderPath, ResourceRelativeFolderPath).ToOSPath();
        Directory.CreateDirectory(intermediateFilesFullFolderPath);
        if (HeightmapData is not null)
        {
            string heightmapFullFilePath = Path.Combine(intermediateFilesFullFolderPath, IntermediateHeightmapFileName);
            HeightmapSerializationHelper.SerializeFloatArray2dToHexFile(HeightmapData, heightmapFullFilePath);
        }
        if (MaterialIndexMapData is not null)
        {
            string materialMapFullFilePath = Path.Combine(intermediateFilesFullFolderPath, IntermediateMaterialIndexMapFileName);
            HeightmapSerializationHelper.SerializeByteArray2dToHexFile(MaterialIndexMapData, materialMapFullFilePath);
        }

        // Save layer intermediate files
        if (HeightmapLayerDataList is not null)
        {
            foreach (var layerData in HeightmapLayerDataList)
            {
                if (!layerData.IsSerializeIntermediateFileRequired)
                {
                    continue;
                }
                layerData.SerializeIntermediateFile(intermediateFilesFullFolderPath, assetFullFolderPath, this, logger);
                layerData.LastModifiedIntermediateFile = DateTimeOffset.UtcNow;
                layerData.IsSerializeIntermediateFileRequired = false;
            }
        }
        if (MaterialWeightMapLayerDataList is not null)
        {
            foreach (var layerData in MaterialWeightMapLayerDataList)
            {
                if (!layerData.IsSerializeIntermediateFileRequired)
                {
                    continue;
                }
                layerData.SerializeIntermediateFile(intermediateFilesFullFolderPath, assetFullFolderPath, this, logger);
                layerData.LastModifiedIntermediateFile = DateTimeOffset.UtcNow;
                layerData.IsSerializeIntermediateFileRequired = false;
            }
        }

        // Save the actual paths used as the 'original' paths for reloading intermediate files
        OriginalAssetFullFilePath = AssetFullFilePath;
        OriginalResourceRelativeFolderPath = ResourceRelativeFolderPath;
    }

    private static UDirectory CreateDefaultResourceRelativeFolderPath(UFile assetFullFilePath)
    {
        string assetFileName = assetFullFilePath.GetFileNameWithoutExtension()!;
        var path = new UDirectory($"{assetFileName}_TerrainData");
        return path;
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
        var terrainMap = new TerrainMap();
        TerrainPropertiesCopyToInternal(terrainMap, reuseHeightmapData: true);
        terrainMap.TerrainMaterial = TerrainMaterial;
        return terrainMap;
    }
}
