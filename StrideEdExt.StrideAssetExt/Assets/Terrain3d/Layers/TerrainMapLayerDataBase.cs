using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using System.Diagnostics;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers;

[DataContract(Inherited = true)]
public abstract class TerrainMapLayerDataBase
{
    public Guid LayerId { get; set; }
    public DateTimeOffset? LastModifiedIntermediateFile { get; set; }

    [DataMemberIgnore]
    public bool IsSerializeIntermediateFileRequired { get; set; }

    [DataMemberIgnore]
    public bool IsDeserializeIntermediateFileRequired { get; set; } = true;

    public void SerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger)
    {
        OnSerializeIntermediateFile(intermediateFilesFullFolderPath, terrainMapAssetFullFolderPath, terrainMapAsset, logger);
    }
    protected abstract void OnSerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger);

    public void DeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger)
    {
        OnDeserializeIntermediateFile(intermediateFilesFullFolderPath, terrainMapAssetFullFolderPath, terrainMapAsset, logger);
        IsDeserializeIntermediateFileRequired = false;
    }
    protected abstract void OnDeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger);

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
        string heightmapFullFilePath = UDirectory.Combine(intermediateFilesFullFolderPath, fileName).ToOSPath();
        return heightmapFullFilePath;
    }
}

public abstract class TerrainHeightmapLayerDataBase : TerrainMapLayerDataBase
{
    internal const string IntermediateHeightmapFileNameFormat = "layer_heightmap_{0}.txt";

    /// <summary>
    /// Path relative to its main asset.
    /// </summary>
    public UFile? HeightmapRelativeFilePath { get; set; }

    public Int2 HeightmapTexturePixelStartPosition { get; set; }

    public TerrainHeightmapLayerBlendType LayerBlendType { get; set; }

    public override void DeleteIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, ILogger? logger)
    {
        string? intermediateFileFullFilePath;
        if (HeightmapRelativeFilePath is not null)
        {
            intermediateFileFullFilePath = UPath.Combine(terrainMapAssetFullFolderPath, HeightmapRelativeFilePath).ToOSPath();
        }
        else
        {
            intermediateFileFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateHeightmapFileNameFormat);
        }
        _ = AssetExt.TryDeleteFile(intermediateFileFullFilePath, logger);
    }

    /// <summary>
    /// Apply this layer's modification to <paramref name="terrainMapHeightmapData"/>.
    /// </summary>
    public abstract void ApplyHeightmapModifications(Array2d<float> terrainMapHeightmapData, Vector2 heightRange);
}

public abstract class TerrainMaterialMapLayerDataBase : TerrainMapLayerDataBase
{
    internal const string IntermediateMaterialWeightMapFileNameFormat = "layer_material_{0}.txt";

    /// <summary>
    /// Path relative to its main asset.
    /// </summary>
    public UFile? MaterialWeightMapRelativeFilePath { get; set; }

    public Int2 MaterialWeightMapTexturePixelStartPosition { get; set; }

    /// <summary>
    /// Target Material Name. Must match <see cref="TerrainMaterialLayerDefinitionAsset.MaterialName"/>.
    /// </summary>
    public string? MaterialName { get; set; }

    public override void DeleteIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, ILogger? logger)
    {
        string? intermediateFileFullFilePath;
        if (MaterialWeightMapRelativeFilePath is not null)
        {
            intermediateFileFullFilePath = UPath.Combine(terrainMapAssetFullFolderPath, MaterialWeightMapRelativeFilePath).ToOSPath();
        }
        else
        {
            intermediateFileFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateMaterialWeightMapFileNameFormat);
        }
        _ = AssetExt.TryDeleteFile(intermediateFileFullFilePath, logger);
    }

    /// <summary>
    /// Apply this layer's modification to <paramref name="materialIndexMapData"/>.
    /// </summary>
    public abstract void ApplyLayerMaterialMapModifications(Array2d<Half> materialWeightMapData, Array2d<byte> materialIndexMapData, List<TerrainMaterialLayerDefinitionAsset> materialLayers);
}
