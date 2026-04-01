using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Collections;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
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
public class ObjectPlacementMapAsset : Asset
{
    private const string CurrentVersion = "0.0.0.1";

    private readonly object _serializationLock = new();

    //public Int2 ChunkSize { get; set; }         // HACK: Int2 instead of Size2 because the editor UI doesn't have an inline version of Size2 control
    public TerrainMap? TerrainMap { get; set; }

    /// <summary>
    /// Debug only field. Used to quickly determine how many objects have been serialized.
    /// </summary>
    [Display(Browsable = false)]
    public int TotalObjectPlacementCount { get; set; }
    // Do not make this Browsable, it'll crash the editor due to too much data.
    ////[Display(Browsable = false)]
    ////public List<ModelPlacement> ModelPlacements { get; set; } = new();

    private readonly List<ObjectPlacementLayerDataBase> _pendingRemovalLayerDataList = [];

    private TrackingCollection<ObjectDensityMapLayerDataBase>? _densityMapLayerDataList;
    [Display(Browsable = false)]
    [DataMember]
    internal TrackingCollection<ObjectDensityMapLayerDataBase>? DensityMapLayerDataList
    {
        get => _densityMapLayerDataList;
        set
        {
            if (_densityMapLayerDataList is not null)
            {
                _densityMapLayerDataList.CollectionChanged -= OnDensityMapLayerCollectionChanged;
            }
            _densityMapLayerDataList = value;
            if (_densityMapLayerDataList is not null)
            {
                _densityMapLayerDataList.CollectionChanged += OnDensityMapLayerCollectionChanged;
            }
        }
    }

    private void OnDensityMapLayerCollectionChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            var layerData = (ObjectDensityMapLayerDataBase)e.Item!;
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    private TrackingCollection<ObjectSpawnerDataBase>? _spawnerDataList;
    [Display(Browsable = false)]
    [DataMember]
    internal TrackingCollection<ObjectSpawnerDataBase>? SpawnerDataList
    {
        get => _spawnerDataList;
        set
        {
            if (_spawnerDataList is not null)
            {
                _spawnerDataList.CollectionChanged -= OnSpawnerCollectionChanged;
            }
            _spawnerDataList = value;
            if (_spawnerDataList is not null)
            {
                _spawnerDataList.CollectionChanged += OnSpawnerCollectionChanged;
            }
        }
    }

    private void OnSpawnerCollectionChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            var layerData = (ObjectSpawnerDataBase)e.Item!;
            _pendingRemovalLayerDataList.Add(layerData);
        }
    }

    [Display(Browsable = false)]
    [DataMember]
    internal DateTimeOffset? LastUserModifiedDateTimeUtc { get; set; }

    [DataMemberIgnore]
    public bool HasObjectPlacementLayerSerializedIntermediateFiles { get; set; }

    /// <remarks>
    /// Folder path should be unique for each <see cref="ObjectPlacementMapAsset"/> due to hardcoded file names for generated intermediate files.
    /// </remarks>
    [UPath(UPathRelativeTo.Package)]
    public UDirectory? ResourceFolderPath { get; set; }

    public List<string> ModelAssetUrlList { get; set; } = [];
    public List<string> PrefabAssetUrlList { get; set; } = [];

    public ObjectPlacementMapAsset()
    {
        DensityMapLayerDataList = [];
        SpawnerDataList = [];
    }

    public bool TryGetDensityMapLayerData<TLayerData>(Guid layerId, [NotNullWhen(true)] out TLayerData? layerData)
        where TLayerData : ObjectDensityMapLayerDataBase
    {
        layerData = null;
        if (DensityMapLayerDataList is not null
            && DensityMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            layerData = DensityMapLayerDataList[index] as TLayerData;
        }
        return layerData is not null;
    }

    public bool TryGetObjectSpawnerData<TSpawnerData>(Guid layerId, [NotNullWhen(true)] out TSpawnerData? spawnerData)
        where TSpawnerData : ObjectSpawnerDataBase
    {
        spawnerData = null;
        if (SpawnerDataList is not null
            && SpawnerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            spawnerData = SpawnerDataList[index] as TSpawnerData;
        }
        return spawnerData is not null;
    }

    public bool TryGetLayerData(Guid layerId, [NotNullWhen(true)] out ObjectPlacementLayerDataBase? layerData)
    {
        layerData = null;
        if (DensityMapLayerDataList is not null
            && DensityMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            layerData = DensityMapLayerDataList[index];
        }
        else if (SpawnerDataList is not null
            && SpawnerDataList.TryFindIndex(x => x.LayerId == layerId, out index))
        {
            layerData = SpawnerDataList[index];
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
        DensityMapLayerDataList ??= [];
        if (DensityMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            var layerData = DensityMapLayerDataList[index];
            return layerData;
        }
        else
        {
            var layerData = _pendingRemovalLayerDataList.Find(x => x.LayerId == layerId) as ObjectDensityMapLayerDataBase;
            if (layerData is not null)
            {
                // Add back to active list
                _pendingRemovalLayerDataList.Remove(layerData);
            }
            else
            {
                layerData = (ObjectDensityMapLayerDataBase)Activator.CreateInstance(layerDataType)!;
                layerData.LayerId = layerId;
                layerData.IsDeserializeIntermediateFileRequired = false;
            }
            DensityMapLayerDataList.Add(layerData!);
            return layerData;
        }
    }

    private ObjectSpawnerDataBase GetOrCreateSpawnerData(Guid layerId, Type layerDataType)
    {
        SpawnerDataList ??= [];
        if (SpawnerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            var layerData = SpawnerDataList[index];
            return layerData;
        }
        else
        {
            var layerData = _pendingRemovalLayerDataList.Find(x => x.LayerId == layerId) as ObjectSpawnerDataBase;
            if (layerData is not null)
            {
                // Add back to active list
                _pendingRemovalLayerDataList.Remove(layerData);
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
        if (DensityMapLayerDataList is not null
            && DensityMapLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int index))
        {
            DensityMapLayerDataList.RemoveAt(index);
            return true;
        }
        else if (SpawnerDataList is not null
            && SpawnerDataList.TryFindIndex(x => x.LayerId == layerId, out index))
        {
            SpawnerDataList.RemoveAt(index);
            return true;
        }
        return false;
    }

    public void SetDensityMapLayerOrdering(List<Guid> densityMapLayerIds)
    {
        DensityMapLayerDataList ??= [];
        var layerIdToLayerData = DensityMapLayerDataList.ToDictionary(x => x.LayerId);
        DensityMapLayerDataList.Clear();
        foreach (var layerId in densityMapLayerIds)
        {
            if (!layerIdToLayerData.TryGetValue(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int removalListIndex))
                {
                    layerData = (ObjectDensityMapLayerDataBase)_pendingRemovalLayerDataList[removalListIndex];

                    // Add back to active list
                    _pendingRemovalLayerDataList.RemoveAt(removalListIndex);
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
        SpawnerDataList ??= [];
        var layerIdToLayerData = SpawnerDataList.ToDictionary(x => x.LayerId);
        SpawnerDataList.Clear();
        foreach (var layerId in spawnerIds)
        {
            if (!layerIdToLayerData.TryGetValue(layerId, out var layerData))
            {
                if (_pendingRemovalLayerDataList.TryFindIndex(x => x.LayerId == layerId, out int removalListIndex))
                {
                    layerData = (ObjectSpawnerDataBase)_pendingRemovalLayerDataList[removalListIndex];

                    // Add back to active list
                    _pendingRemovalLayerDataList.RemoveAt(removalListIndex);
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
                    Directory.CreateDirectory(resourceFolderFullPath);
                }
            }

            _hasCalledFinalizeContentDeserialization = true;
        }
    }

    public void PrepareContentSerialization()
    {
        //lock (_serializationLock)
        //{
        //}
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

                    if (DensityMapLayerDataList is not null)
                    {
                        foreach (var layerData in DensityMapLayerDataList)
                        {
                            if (layerData.IsSerializeIntermediateFileRequired)
                            {
                                layerData.SerializeIntermediateFile(resourceFolderFullPath, this, logger);
                                layerData.LastModifiedIntermediateFile = DateTimeOffset.UtcNow;
                                layerData.IsSerializeIntermediateFileRequired = false;
                                HasObjectPlacementLayerSerializedIntermediateFiles = true;
                            }
                        }
                    }
                    if (SpawnerDataList is not null)
                    {
                        foreach (var layerData in SpawnerDataList)
                        {
                            if (layerData.IsSerializeIntermediateFileRequired)
                            {
                                layerData.SerializeIntermediateFile(resourceFolderFullPath, this, logger);
                                layerData.LastModifiedIntermediateFile = DateTimeOffset.UtcNow;
                                layerData.IsSerializeIntermediateFileRequired = false;
                                HasObjectPlacementLayerSerializedIntermediateFiles = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
