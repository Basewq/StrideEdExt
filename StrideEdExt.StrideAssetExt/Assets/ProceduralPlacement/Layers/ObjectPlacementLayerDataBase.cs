using Stride.Core;
using Stride.Core.Assets;
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

    public void SerializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger logger)
    {
        OnSerializeIntermediateFile(packageFolderPath, objectPlacementMapAsset, logger);
    }
    protected abstract void OnSerializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger logger);

    public void DeserializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger logger)
    {
        OnDeserializeIntermediateFile(packageFolderPath, objectPlacementMapAsset, terrainMapTextureSize, logger);
        IsDeserializeIntermediateFileRequired = false;
    }
    protected abstract void OnDeserializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger logger);

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
}

public abstract class ObjectDensityMapLayerDataBase : ObjectPlacementLayerDataBase
{
    internal const string IntermediateObjectDensityMapFileNameFormat = "layer_density_{0}.txt";

    [UPath(UPathRelativeTo.Package)]
    public UFile? ObjectDensityMapFilePath { get; set; }

    public Int2? ObjectDensityMapTexturePixelStartPosition;
}

public abstract class ObjectSpawnerDataBase : ObjectPlacementLayerDataBase, IObjectSpawnerLayerData
{
    internal const string IntermediateObjectSpawnerFileNameFormat = "object_placement_spawner_{0}.txt";

    [UPath(UPathRelativeTo.Package)]
    public UFile? ObjectSpawnerFilePath { get; set; }

    public float MinimumDensityValueThreshold { get; set; }

    /// <summary>
    /// Pool of assets used to spawn an object.
    /// </summary>
    public List<ObjectSpawnAssetDefinition> SpawnAssetDefinitionList { get; set; } = [];

    [DataMemberIgnore]
    public List<ObjectPlacementSpawnPlacementData> SpawnPlacementDataList { get; set; } = [];

    public bool IsOccupied(Vector3 position, float radius)
    {
        var sphereA = new BoundingSphere(position, radius);
        foreach (var placementData in SpawnPlacementDataList)
        {
            int spawnAssetDefAssetListIndex = placementData.AssetUrlListIndex;
            if (SpawnAssetDefinitionList.TryGetValue(spawnAssetDefAssetListIndex, out var spawnAssetDefinition))
            {
                float scale = placementData.GetMaxScale1d();
                var sphereB = new BoundingSphere(placementData.Position, spawnAssetDefinition.CollisionRadius * scale);
                if (CollisionHelper.SphereIntersectsSphere(in sphereA, in sphereB))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
