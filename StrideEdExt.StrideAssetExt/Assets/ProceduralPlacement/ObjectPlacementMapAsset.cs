using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement;

/**
 * The asset as seen by Game Studio.
 * Refer to Stride's source could for additional asset options, eg. referencing a file.
 */
[DataContract]
[AssetDescription(".gtopm")]
[ContentSerializer(typeof(DataContentSerializer<ObjectPlacementMapAsset>))]
[AssetContentType(typeof(ObjectPlacementMap))]
//[CategoryOrder(1000, "Placement")]
[AssetFormatVersion(StrideEdExtConfig.PackageName, CurrentVersion)]
//[AssetUpgrader(StrideEdExtConfig.PackageName, "0.0.0.1", "1.0.0.0", typeof(ObjectPlacementMapAssetUpgrader))]    // Can be used to update an old asset format to a new format.
[Display(10000, "Object Placement Map")]
public class ObjectPlacementMapAsset : Asset, IStrideCustomAsset
{
    private const string CurrentVersion = "0.0.0.1";

    /// <summary>
    /// The full file path of the asset if it was deserialized from a file.
    /// </summary>
    internal string? OriginalAssetFullFilePath;
    /// <summary>
    /// Full file path of the asset. This is only applicable for serialization since
    /// this is only set in OnAssetLoaded or ObjectPlacementMapAssetViewModel.Initialize (after the asset has been loaded).
    /// </summary>
    internal string? AssetFullFilePath;
    /// <summary>
    /// Path relative to <see cref="OriginalAssetFullFilePath"/>.
    /// </summary>
    internal string? OriginalResourceRelativeFolderPath;

    //public Int2 ChunkSize { get; set; }         // HACK: Int2 instead of Size2 because the editor UI doesn't have an inline version of Size2 control
    public TerrainMap? TerrainMap { get; set; }

    [Display(Browsable = false)]
    [DataMember]
    internal Size2 TerrainMapTextureSize { get; set; }

    /// <summary>
    /// Debug only field. Used to quickly determine how many objects have been serialized.
    /// </summary>
    [Display(Browsable = false)]
    public int TotalObjectPlacementCount { get; set; }

    private readonly List<ObjectPlacementLayerDataBase> _pendingRemovalLayerDataList = [];

    /// <summary>
    /// List must not be directly modified externally.
    /// Use <see cref="GetOrCreateLayerData"/> or <see cref="GetOrCreateDensityMapLayerData"/>, and <see cref="TryRemoveLayerData"/>.
    /// </summary>
    [Display(Browsable = false)]
    [DataMember]
    internal List<ObjectDensityMapLayerDataBase> DensityMapLayerDataList { get; } = [];

    /// <summary>
    /// List must not be directly modified externally.
    /// Use <see cref="GetOrCreateLayerData"/> or <see cref="GetOrCreateSpawnerData"/>, and <see cref="TryRemoveLayerData"/>.
    /// </summary>
    [Display(Browsable = false)]
    [DataMember]
    internal List<ObjectSpawnerDataBase> SpawnerDataList { get; } = [];

    [Display(Browsable = false)]
    [DataMember]
    internal DateTimeOffset? LastUserModifiedDateTimeUtc { get; set; }

    /// <remarks>
    /// Folder path should be unique for each <see cref="ObjectPlacementMapAsset"/> due to hardcoded file names for generated intermediate files.
    /// Path relative to <see cref="AssetFullFilePath"/>.
    /// </remarks>
    public UDirectory? ResourceRelativeFolderPath { get; set; }

    [Display(Browsable = false)]
    [DataMember]
    internal List<string> ModelAssetUrlList { get; } = [];
    [Display(Browsable = false)]
    [DataMember]
    internal List<string> PrefabAssetUrlList { get; } = [];

    public ObjectPlacementMapAsset()
    {
        DensityMapLayerDataList = [];
        SpawnerDataList = [];
    }

    public bool TryGetDensityMapLayerData<TLayerData>(Guid layerId, [NotNullWhen(true)] out TLayerData? layerData)
        where TLayerData : ObjectDensityMapLayerDataBase
    {
        layerData = null;
        if (DensityMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var dmLayerData))
        {
            layerData = dmLayerData as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetObjectSpawnerData<TSpawnerData>(Guid layerId, [NotNullWhen(true)] out TSpawnerData? spawnerData)
        where TSpawnerData : ObjectSpawnerDataBase
    {
        spawnerData = null;
        if (SpawnerDataList.TryFindItem(x => x.LayerId == layerId, out var spwnLayerData))
        {
            spawnerData = spwnLayerData as TSpawnerData;
        }
        return spawnerData is not null;
    }

    public bool TryGetLayerData(Guid layerId, [NotNullWhen(true)] out ObjectPlacementLayerDataBase? layerData)
    {
        layerData = null;
        if (DensityMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var dmLayerData))
        {
            layerData = dmLayerData;
        }
        else if (SpawnerDataList.TryFindItem(x => x.LayerId == layerId, out var spwnLayerData))
        {
            layerData = spwnLayerData;
        }
        return layerData is not null;
    }

    public TLayerData GetOrCreateLayerData<TLayerData>(Guid layerId)
        where TLayerData : ObjectPlacementLayerDataBase, new()
    {
        if (typeof(TLayerData).IsAssignableTo(typeof(ObjectDensityMapLayerDataBase)))
        {
            var layerData = GetOrCreateDensityMapLayerData(layerId, typeof(TLayerData));
            return (layerData as TLayerData)!;
        }
        else if (typeof(TLayerData).IsAssignableTo(typeof(ObjectSpawnerDataBase)))
        {
            var layerData = GetOrCreateSpawnerData(layerId, typeof(TLayerData));
            return (layerData as TLayerData)!;
        }
        else
        {
            throw new NotImplementedException($"Unhandled layer type: {typeof(TLayerData).Name}");
        }
    }

    public ObjectPlacementLayerDataBase GetOrCreateLayerData(Guid layerId, Type layerDataType)
    {
        if (layerDataType.IsAssignableTo(typeof(ObjectDensityMapLayerDataBase)))
        {
            var layerData = GetOrCreateDensityMapLayerData(layerId, layerDataType);
            return layerData;
        }
        else if (layerDataType.IsAssignableTo(typeof(ObjectSpawnerDataBase)))
        {
            var layerData = GetOrCreateSpawnerData(layerId, layerDataType);
            return layerData;
        }
        else
        {
            throw new NotImplementedException($"Unhandled layer type: {layerDataType.Name}");
        }
    }

    private ObjectDensityMapLayerDataBase GetOrCreateDensityMapLayerData(Guid layerId, Type layerDataType)
    {
        if (DensityMapLayerDataList.TryFindItem(x => x.LayerId == layerId, out var layerData))
        {
            return layerData;
        }
        else
        {
            if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
            {
                // Add back to active list
                layerData = (ObjectDensityMapLayerDataBase)restoredLayerData;
            }
            else
            {
                layerData = (ObjectDensityMapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsSerializeIntermediateFileRequired = true;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            DensityMapLayerDataList.Add(layerData!);
            return layerData;
        }
    }

    private ObjectSpawnerDataBase GetOrCreateSpawnerData(Guid layerId, Type layerDataType)
    {
        if (SpawnerDataList.TryFindItem(x => x.LayerId == layerId, out var layerData))
        {
            return layerData;
        }
        else
        {
            if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
            {
                // Add back to active list
                layerData = (ObjectSpawnerDataBase)restoredLayerData;
            }
            else
            {
                layerData = (ObjectSpawnerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsSerializeIntermediateFileRequired = true;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            SpawnerDataList.Add(layerData!);
            return layerData;
        }
    }

    public bool TryRemoveLayerData(Guid layerId)
    {
        if (DensityMapLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var dmLayerData))
        {
            _pendingRemovalLayerDataList.Add(dmLayerData);
            return true;
        }
        else if (SpawnerDataList.TryRemoveItem(x => x.LayerId == layerId, out var spwnLayerData))
        {
            _pendingRemovalLayerDataList.Add(spwnLayerData);
            return true;
        }
        return false;
    }

    public void SetDensityMapLayerOrdering(List<Guid> densityMapLayerIds)
    {
        var layerIdToLayerData = DensityMapLayerDataList.ToDictionary(x => x.LayerId);
        DensityMapLayerDataList.Clear();
        foreach (var layerId in densityMapLayerIds)
        {
            if (!layerIdToLayerData.Remove(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
                {
                    // Add back to active list
                    layerData = (ObjectDensityMapLayerDataBase)restoredLayerData;
                }
                else
                {
                    throw new InvalidOperationException($"Layer not found: {layerId}");
                }
            }
            DensityMapLayerDataList.Add(layerData);
        }
        // Any remainder is removed
        foreach (var (_, layerData) in layerIdToLayerData)
        {
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    public void SetSpawnerOrdering(List<Guid> spawnerIds)
    {
        var layerIdToLayerData = SpawnerDataList.ToDictionary(x => x.LayerId);
        SpawnerDataList.Clear();
        foreach (var layerId in spawnerIds)
        {
            if (!layerIdToLayerData.Remove(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryRemoveItem(x => x.LayerId == layerId, out var restoredLayerData))
                {
                    // Add back to active list
                    layerData = (ObjectSpawnerDataBase)restoredLayerData;
                }
                else
                {
                    throw new InvalidOperationException($"Layer not found: {layerId}");
                }
            }
            SpawnerDataList.Add(layerData);
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
            logger?.Error($"{nameof(ObjectPlacementMapAsset)} {Id}: '{nameof(AssetFullFilePath)}' was not assigned.");
            return;
        }

        var assetFullFolderPath = assetFullFilePath.GetFullDirectory();

        if (string.IsNullOrWhiteSpace(ResourceRelativeFolderPath?.FullPath))
        {
            logger?.Warning($"{nameof(ObjectPlacementMapAsset)} {Id}: '{nameof(ResourceRelativeFolderPath)}' was not assigned. Creating default path.");

            ResourceRelativeFolderPath = CreateDefaultResourceRelativeFolderPath(assetFullFilePath);
        }
        else
        {
            // Ensure path is relative to this asset's path
            ResourceRelativeFolderPath = ResourceRelativeFolderPath.MakeRelative(assetFullFolderPath);
            OriginalResourceRelativeFolderPath = ResourceRelativeFolderPath.FullPath;
        }

        // Note that layer data for DensityMapLayerDataList & SpawnerDataList are lazy loaded
    }

    public void OnAssetSaving(ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(AssetFullFilePath))
        {
            logger?.Error($"{nameof(ObjectPlacementMapAsset)} {Id}: '{nameof(AssetFullFilePath)}' was not assigned.");
            return;
        }

        var assetFullFilePath = new UFile(AssetFullFilePath);
        var assetFullFolderPath = assetFullFilePath.GetFullDirectory();

        if (string.IsNullOrWhiteSpace(ResourceRelativeFolderPath?.FullPath))
        {
            logger?.Warning($"{nameof(ObjectPlacementMapAsset)} {Id}: '{nameof(ResourceRelativeFolderPath)}' was not assigned. Creating default path.");

            ResourceRelativeFolderPath = CreateDefaultResourceRelativeFolderPath(assetFullFilePath);
        }
        else
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
            // Delete layer intermediate files in old location
            if (DensityMapLayerDataList is not null)
            {
                foreach (var layerData in DensityMapLayerDataList)
                {
                    layerData.DeleteIntermediateFile(pendingDeleteIntermediateFilesFullFolderPath, pendingDeleteAssetFullFolderPath, logger);
                    layerData.IsSerializeIntermediateFileRequired = true;   // In case of undo, we may need to regenerate this file after deletion
                }
            }
            if (SpawnerDataList is not null)
            {
                foreach (var layerData in SpawnerDataList)
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

        // Save layer intermediate files
        string intermediateFilesFullFolderPath = UPath.Combine(assetFullFolderPath, ResourceRelativeFolderPath).ToOSPath();
        Directory.CreateDirectory(intermediateFilesFullFolderPath);
        if (DensityMapLayerDataList is not null)
        {
            foreach (var layerData in DensityMapLayerDataList)
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
        if (SpawnerDataList is not null)
        {
            foreach (var layerData in SpawnerDataList)
            {
                if (!layerData.IsSerializeIntermediateFileRequired)
                {
                    continue;
                }
                layerData.SerializeIntermediateFile(intermediateFilesFullFolderPath, assetFullFolderPath, this, logger);
                layerData.LastModifiedIntermediateFile = DateTimeOffset.UtcNow;
                layerData.IsSerializeIntermediateFileRequired = false;
            }

            TotalObjectPlacementCount = SpawnerDataList.Sum(x => x.SpawnPlacementDataList.Count);
        }

        // Save the actual paths used as the 'original' paths for reloading intermediate files
        OriginalAssetFullFilePath = AssetFullFilePath;
        OriginalResourceRelativeFolderPath = ResourceRelativeFolderPath;
    }

    private static UDirectory CreateDefaultResourceRelativeFolderPath(UFile assetFullFilePath)
    {
        string assetFileName = assetFullFilePath.GetFileNameWithoutExtension()!;
        var path = new UDirectory($"{assetFileName}_OpmData");
        return path;
    }
}
