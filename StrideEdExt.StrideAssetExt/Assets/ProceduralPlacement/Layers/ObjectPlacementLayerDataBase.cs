using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using System.Diagnostics;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers;

[DataContract(Inherited = true)]
public abstract class ObjectPlacementLayerDataBase
{
    public Guid LayerId { get; set; }
    public DateTimeOffset? LastModifiedIntermediateFile { get; set; }

    [DataMemberIgnore]
    public bool IsSerializeIntermediateFileRequired { get; set; }

    [DataMemberIgnore]
    public bool IsDeserializeIntermediateFileRequired { get; set; } = true;

    public void SerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger? logger)
    {
        OnSerializeIntermediateFile(intermediateFilesFullFolderPath, objectPlacementMapAssetFullFolderPath, objectPlacementMapAsset, logger);
    }
    protected abstract void OnSerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger? logger);

    public void DeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger? logger)
    {
        OnDeserializeIntermediateFile(intermediateFilesFullFolderPath, objectPlacementMapAssetFullFolderPath, objectPlacementMapAsset, terrainMapTextureSize, logger);
        IsDeserializeIntermediateFileRequired = false;
    }
    protected abstract void OnDeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger? logger);

    public abstract void DeleteIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, ILogger? logger);

    public void UpdateForTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder)
    {
        OnTerrainMapResized(heightmapTextureSize, assetTransactionBuilder);
    }
    protected virtual void OnTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder) { }

    protected static void EnsureCorrectMapSize<T>(Array2d<T> array2d, Size2 expectedSize, bool minimumSizeCheckOnly = true)
    {
        if (minimumSizeCheckOnly)
        {
            int sizeX = Math.Max(array2d.LengthX, expectedSize.Width);
            int sizeY = Math.Max(array2d.LengthY, expectedSize.Height);
            if (sizeX != array2d.LengthX || sizeY != array2d.LengthY)
            {
                Debug.WriteLine($"Deserialized map data size mismatched with expected map size: Array2d: {array2d.Length2d} - Expected: {new Int2(sizeX, sizeY)}. Resizing the array.");
                array2d.Resize(sizeX, sizeY);
            }
        }
        else if (array2d.Length2d != expectedSize)
        {
            Debug.WriteLine($"Deserialized map data size mismatched with expected map size: Array2d: {array2d.Length2d} - Expected: {expectedSize}. Resizing the array.");
            array2d.Resize(expectedSize);
        }
    }

    public string GetIntermediateFileFullFilePath(
        UDirectory intermediateFilesFullFolderPath, string fileNameFormat)
    {
        string fileName = string.Format(fileNameFormat, LayerId.ToString("N"));
        string layerFullFilePath = UDirectory.Combine(intermediateFilesFullFolderPath, fileName).ToOSPath();
        return layerFullFilePath;
    }
}

public abstract class ObjectDensityMapLayerDataBase : ObjectPlacementLayerDataBase
{
    internal const string IntermediateObjectDensityMapFileNameFormat = "layer_density_{0}.txt";

    /// <summary>
    /// Path relative to its main asset.
    /// </summary>
    public UFile? ObjectDensityMapRelativeFilePath { get; set; }

    public Int2 ObjectDensityMapTexturePixelStartPosition { get; set; }

    public override void DeleteIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, ILogger? logger)
    {
        string? intermediateFileFullFilePath;
        if (ObjectDensityMapRelativeFilePath is not null)
        {
            intermediateFileFullFilePath = UPath.Combine(terrainMapAssetFullFolderPath, ObjectDensityMapRelativeFilePath).ToOSPath();
        }
        else
        {
            intermediateFileFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectDensityMapFileNameFormat);
        }
        _ = AssetExt.TryDeleteFile(intermediateFileFullFilePath, logger);
    }
}

public abstract class ObjectSpawnerDataBase : ObjectPlacementLayerDataBase, IObjectSpawnerLayerData
{
    internal const string IntermediateObjectSpawnerFileNameFormat = "object_placement_spawner_{0}.txt";

    /// <summary>
    /// Path relative to its main asset.
    /// </summary>
    public UFile? ObjectSpawnerRelativeFilePath { get; set; }

    /// <summary>
    /// Pool of assets used to spawn an object.
    /// </summary>
    public List<ObjectSpawnAssetDefinition> SpawnAssetDefinitionList { get; set; } = [];

    [DataMemberIgnore]
    public List<ObjectPlacementSpawnPlacementData> PreviousSpawnPlacementDataList { get; set; } = [];
    [DataMemberIgnore]
    public List<ObjectPlacementSpawnPlacementData> SpawnPlacementDataList { get; set; } = [];

    public override void DeleteIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, ILogger? logger)
    {
        string? intermediateFileFullFilePath;
        if (ObjectSpawnerRelativeFilePath is not null)
        {
            intermediateFileFullFilePath = UPath.Combine(terrainMapAssetFullFolderPath, ObjectSpawnerRelativeFilePath).ToOSPath();
        }
        else
        {
            intermediateFileFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectSpawnerFileNameFormat);
        }
        _ = AssetExt.TryDeleteFile(intermediateFileFullFilePath, logger);
    }
}
