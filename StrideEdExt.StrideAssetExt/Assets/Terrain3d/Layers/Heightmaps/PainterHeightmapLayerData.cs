using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using StrideEdExt.StrideAssetExt.Assets.Transaction.Commands;
using System.Diagnostics;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;

public class PainterHeightmapLayerData : TerrainHeightmapLayerDataBase
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
            EnsureCorrectMapSize(HeightmapData, terrainMapAsset.HeightmapTextureSize.ToSize2());
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

    protected override void OnTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (HeightmapData is Array2d<float> curHeightmapData)
        {
            int maxNonZeroX = 0;
            int maxNonZeroY = 0;
            foreach (var (index, heightValue) in curHeightmapData)
            {
                if (heightValue != 0)
                {
                    maxNonZeroX = Math.Max(index.X, maxNonZeroX);
                    maxNonZeroY = Math.Max(index.Y, maxNonZeroY);
                }
            }
            int resizeX = Math.Max(maxNonZeroX, heightmapTextureSize.Width);
            int resizeY = Math.Max(maxNonZeroY, heightmapTextureSize.Height);
            if (curHeightmapData.LengthX < resizeX || curHeightmapData.LengthY < resizeY)
            {
                var oldHeightmapData = HeightmapData;
                var newHeightmapData = new Array2d<float>(resizeX, resizeY);
                curHeightmapData.CopyToUnaligned(newHeightmapData);
                HeightmapData = newHeightmapData;

                var cmd = this.CreateSetValueCommand((rootObj, val) => rootObj.HeightmapData = val, oldValue: oldHeightmapData, newValue: newHeightmapData);
                assetTransactionBuilder.AddCommand(cmd);
            }
        }
    }

    public void ApplyHeightmapAdjustments(Array2d<float> adjustmentHeightmapData, Int2 startPosition, Array2d<float> terrainHeightmapData, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (HeightmapData is null)
        {
            Debug.WriteLine($"HeightmapData not assigned.");
            return;
        }

        var cmd = new ModifyArray2dCommand<float>(HeightmapData);

        int maxXExcl = Math.Min(HeightmapData.LengthX - startPosition.X + 1, adjustmentHeightmapData.LengthX);
        int maxYExcl = Math.Min(HeightmapData.LengthY - startPosition.Y + 1, adjustmentHeightmapData.LengthY);
        Debug.WriteLineIf(adjustmentHeightmapData.LengthX < maxXExcl, $"{nameof(ApplyHeightmapAdjustments)}: adjustmentHeightmapData will be truncated on x-axis - expected length: {adjustmentHeightmapData.LengthX}, actual length: {maxXExcl}");
        Debug.WriteLineIf(adjustmentHeightmapData.LengthY < maxYExcl, $"{nameof(ApplyHeightmapAdjustments)}: adjustmentHeightmapData will be truncated on y-axis - expected length: {adjustmentHeightmapData.LengthY}, actual length: {maxYExcl}");

        for (int y = 0; y < maxYExcl; y++)
        {
            for (int x = 0; x < maxXExcl; x++)
            {
                var adjustmentMapIndex = new Int2(x, y);
                var heightmapIndex = adjustmentMapIndex + startPosition;

                float adjustmentValue = adjustmentHeightmapData[adjustmentMapIndex];
                if (adjustmentValue == 0)
                {
                    continue;
                }
                float terrainHeightmapValue = terrainHeightmapData[heightmapIndex];
                float curValue = terrainHeightmapValue;  // HeightmapData[heightmapIndex];
                curValue += adjustmentValue;
                curValue = Math.Clamp(curValue, 0, 1);
                var prevValue = HeightmapData[heightmapIndex];
                var newValue = curValue;
                if (prevValue != newValue)
                {
                    HeightmapData[heightmapIndex] = newValue;
                    cmd.AddValueChange(heightmapIndex, prevValue, newValue);
                }
            }
        }

        assetTransactionBuilder.AddCommand(cmd);
    }
}
