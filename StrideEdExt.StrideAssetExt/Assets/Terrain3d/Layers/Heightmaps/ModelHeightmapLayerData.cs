using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d.Layers;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;

public class ModelHeightmapLayerData : TerrainHeightmapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTime? LastModifiedSourceFile { get; set; }
    public string? ModelAssetUrl { get; set; }

    /// <summary>
    /// Heightmap values are world-space height values.
    /// </summary>
    [DataMemberIgnore]
    public Array2d<Half?>? HeightmapData;

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        string heightmapFullFilePath = this.GetFilePathOrDefaultPath(HeightmapFilePath, packageFolderPath, IntermediateHeightmapFileNameFormat);
        HeightmapSerializationHelper.SerializeMaskableHalfArray2dToFile(HeightmapData, heightmapFullFilePath);
        HeightmapFilePath = heightmapFullFilePath;
    }

    protected override void OnDeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var heightmapFullFilePath = this.GetFilePathOrDefaultPath(HeightmapFilePath, packageFolderPath, IntermediateHeightmapFileNameFormat);
        if (!File.Exists(heightmapFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {heightmapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeMaskableHalfArray2dFromFile(heightmapFullFilePath, out var heightmapData, out var errorMessage))
        {
            HeightmapData = heightmapData;
        }
        else
        {
            logger.Error(errorMessage);
        }
    }

    public override void ApplyHeightmapModifications(Array2d<float> terrainMapHeightmapData, Vector2 heightRange)
    {
        if (HeightmapData is not Array2d<Half?> localHeightmapData
            || HeightmapTexturePixelStartPosition is not Int2 startingIndex)
        {
            return;
        }
        TerrainMapLayerExtensions.UpdateHeightmapRegion(
            localHeightmapData, startingIndex,
            terrainMapHeightmapData, heightRange,
            LayerBlendType,
            isLocalRegionDataNormalized: false);
    }
}
