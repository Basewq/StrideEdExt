using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d.Layers;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;

public class TextureHeightmapLayerData : TerrainHeightmapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTime? LastModifiedSourceFile { get; set; }

    /// <summary>
    /// Heightmap values are normalized values in range [0...1].
    /// </summary>
    [DataMemberIgnore]
    public Array2d<float>? HeightmapData;

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        var heightmapFullFilePath = this.GetFilePathOrDefaultPath(HeightmapFilePath, packageFolderPath, IntermediateHeightmapFileNameFormat);
        HeightmapSerializationHelper.SerializeFloatArray2dToHexFile(HeightmapData, heightmapFullFilePath);
        HeightmapFilePath = heightmapFullFilePath;
    }

    protected override void OnDeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var heightmapFilePath = this.GetFilePathOrDefaultPath(HeightmapFilePath, packageFolderPath, IntermediateHeightmapFileNameFormat);
        if (!File.Exists(heightmapFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {heightmapFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeFloatArray2dFromHexFile(heightmapFilePath, out var heightmapData, out var errorMessage))
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
        if (HeightmapData is not Array2d<float> localHeightmapData
            || HeightmapTexturePixelStartPosition is not Int2 startingIndex)
        {
            return;
        }

        const bool isHeightmapValueNormalized = true;
        TerrainMapLayerExtensions.UpdateHeightmapRegion(
            localHeightmapData, startingIndex,
            terrainMapHeightmapData, heightRange,
            LayerBlendType, isHeightmapValueNormalized);
    }
}
