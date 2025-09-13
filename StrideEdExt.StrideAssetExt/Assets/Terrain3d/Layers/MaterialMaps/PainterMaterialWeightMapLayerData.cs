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
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.MaterialMaps;

public class PainterMaterialWeightMapLayerData : TerrainMaterialMapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTime? LastModifiedSourceFile { get; set; }

    /// <summary>
    /// MaterialWeightMap values are normalized values in range [0...1].
    /// </summary>
    [DataMemberIgnore]
    public Array2d<Half>? MaterialWeightMapData;

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (MaterialWeightMapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        var heightmapFullFilePath = this.GetFilePathOrDefaultPath(MaterialWeightMapFilePath, packageFolderPath, IntermediateMaterialWeightMapFileNameFormat);
        HeightmapSerializationHelper.SerializeHalfArray2dToHexFile(MaterialWeightMapData, heightmapFullFilePath);
        MaterialWeightMapFilePath = heightmapFullFilePath;
    }

    protected override void OnDeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (MaterialWeightMapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var materialWeightMapFullFilePath = this.GetFilePathOrDefaultPath(MaterialWeightMapFilePath, packageFolderPath, IntermediateMaterialWeightMapFileNameFormat);
        if (!File.Exists(materialWeightMapFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {materialWeightMapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeHalfArray2dFromHexFile(materialWeightMapFullFilePath, out var materialWeightMapData, out var errorMessage))
        {
            MaterialWeightMapData = materialWeightMapData;
            EnsureCorrectMapSize(MaterialWeightMapData, terrainMapAsset.HeightmapTextureSize.ToSize2());
        }
        else
        {
            logger.Error(errorMessage);
        }
    }

    public override void ApplyLayerMaterialMapModifications(Array2d<Half> materialWeightMapData, Array2d<byte> materialIndexMapData, List<TerrainMaterialLayerDefinitionAsset> materialLayers)
    {
        if (MaterialWeightMapData is not Array2d<Half> layerMaterialWeightMapData
            || MaterialWeightMapTexturePixelStartPosition is not Int2 startingIndex)
        {
            return;
        }
        if (!materialLayers.TryGetMaterialIndex(MaterialName, out byte materialIndex))
        {
            Debug.WriteLine($"Failed to get material index for material name '{MaterialName}'");
            return;
        }

        TerrainMapLayerExtensions.UpdateMaterialWeightMapRegion(
            layerMaterialWeightMapData, materialIndex, startingIndex,
            materialWeightMapData, materialIndexMapData);
    }

    protected override void OnTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (MaterialWeightMapData is Array2d<Half> curMaterialWeightMapData)
        {
            int maxNonZeroX = 0;
            int maxNonZeroY = 0;
            foreach (var (index, heightValue) in curMaterialWeightMapData)
            {
                if (heightValue != Half.Zero)
                {
                    maxNonZeroX = Math.Max(index.X, maxNonZeroX);
                    maxNonZeroY = Math.Max(index.Y, maxNonZeroY);
                }
            }
            int resizeX = Math.Max(maxNonZeroX, heightmapTextureSize.Width);
            int resizeY = Math.Max(maxNonZeroY, heightmapTextureSize.Height);
            if (curMaterialWeightMapData.LengthX < resizeX || curMaterialWeightMapData.LengthY < resizeY)
            {
                var oldMaterialWeightMapData = MaterialWeightMapData;
                var newMaterialWeightMapData = new Array2d<Half>(resizeX, resizeY);
                curMaterialWeightMapData.CopyToUnaligned(newMaterialWeightMapData);
                MaterialWeightMapData = newMaterialWeightMapData;

                var cmd = this.CreateSetValueCommand((rootObj, val) => rootObj.MaterialWeightMapData = val, oldValue: oldMaterialWeightMapData, newValue: newMaterialWeightMapData);
                assetTransactionBuilder.AddCommand(cmd);
            }
        }
    }

    public void ApplyWeightMapAdjustments(Array2d<float> adjustmentWeightMapData, Int2 startPosition, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (MaterialWeightMapData is null)
        {
            Debug.WriteLine($"MaterialWeightMapData not assigned.");
            return;
        }

        var cmd = new ModifyArray2dCommand<Half>(MaterialWeightMapData);

        int maxXExcl = Math.Min(MaterialWeightMapData.LengthX - startPosition.X + 1, adjustmentWeightMapData.LengthX);
        int maxYExcl = Math.Min(MaterialWeightMapData.LengthY - startPosition.Y + 1, adjustmentWeightMapData.LengthY);
        Debug.WriteLineIf(adjustmentWeightMapData.LengthX < maxXExcl, $"{nameof(ApplyWeightMapAdjustments)}: adjustmentWeightMapData will be truncated on x-axis - expected length: {adjustmentWeightMapData.LengthX}, actual length: {maxXExcl}");
        Debug.WriteLineIf(adjustmentWeightMapData.LengthY < maxYExcl, $"{nameof(ApplyWeightMapAdjustments)}: adjustmentWeightMapData will be truncated on y-axis - expected length: {adjustmentWeightMapData.LengthY}, actual length: {maxYExcl}");

        for (int y = 0; y < maxYExcl; y++)
        {
            for (int x = 0; x < maxXExcl; x++)
            {
                var adjustmentMapIndex = new Int2(x, y);
                var weightMapIndex = adjustmentMapIndex + startPosition;

                float adjustmentValue = adjustmentWeightMapData[adjustmentMapIndex];
                if (adjustmentValue == 0)
                {
                    continue;
                }
                float curValue = (float)MaterialWeightMapData[weightMapIndex];
                curValue += adjustmentValue;
                curValue = Math.Clamp(curValue, 0, 1);
                var prevValue = MaterialWeightMapData[weightMapIndex];
                var newValue = (Half)curValue;
                if (prevValue != newValue)
                {
                    MaterialWeightMapData[weightMapIndex] = newValue;
                    cmd.AddValueChange(weightMapIndex, prevValue, newValue);
                }
            }
        }

        assetTransactionBuilder.AddCommand(cmd);
    }
}
