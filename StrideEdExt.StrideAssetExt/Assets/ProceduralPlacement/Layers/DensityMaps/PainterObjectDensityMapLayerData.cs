using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using StrideEdExt.StrideAssetExt.Assets.Transaction.Commands;
using System.Diagnostics;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.DensityMaps;

public class PainterObjectDensityMapLayerData : ObjectDensityMapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTime? LastModifiedSourceFile { get; set; }

    /// <summary>
    /// ObjectDensityMap values are normalized values in range [0...1].
    /// </summary>
    [DataMemberIgnore]
    public Array2d<Half>? ObjectDensityMapData;

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger logger)
    {
        if (ObjectDensityMapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        var densityMapFullFilePath = this.GetFilePathOrDefaultPath(ObjectDensityMapFilePath, packageFolderPath, IntermediateObjectDensityMapFileNameFormat);
        HeightmapSerializationHelper.SerializeHalfArray2dToHexFile(ObjectDensityMapData, densityMapFullFilePath);
        ObjectDensityMapFilePath = densityMapFullFilePath;
    }

    protected override void OnDeserializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger logger)
    {
        if (ObjectDensityMapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var densityMapFullFilePath = this.GetFilePathOrDefaultPath(ObjectDensityMapFilePath, packageFolderPath, IntermediateObjectDensityMapFileNameFormat);
        if (!File.Exists(densityMapFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {densityMapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeHalfArray2dFromHexFile(densityMapFullFilePath, out var densityMapData, out var errorMessage))
        {
            ObjectDensityMapData = densityMapData;
            EnsureCorrectMapSize(ObjectDensityMapData, terrainMapTextureSize);
        }
        else
        {
            logger.Error(errorMessage);
        }
    }

    protected override void OnTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (ObjectDensityMapData is Array2d<Half> curObjectDensityMapData)
        {
            int maxNonZeroX = 0;
            int maxNonZeroY = 0;
            foreach (var (index, heightValue) in curObjectDensityMapData)
            {
                if (heightValue != Half.Zero)
                {
                    maxNonZeroX = Math.Max(index.X, maxNonZeroX);
                    maxNonZeroY = Math.Max(index.Y, maxNonZeroY);
                }
            }
            int resizeX = Math.Max(maxNonZeroX, heightmapTextureSize.Width);
            int resizeY = Math.Max(maxNonZeroY, heightmapTextureSize.Height);
            if (curObjectDensityMapData.LengthX < resizeX || curObjectDensityMapData.LengthY < resizeY)
            {
                var oldObjectDensityMapData = ObjectDensityMapData;
                var newObjectDensityMapData = new Array2d<Half>(resizeX, resizeY);
                curObjectDensityMapData.CopyToUnaligned(newObjectDensityMapData);
                ObjectDensityMapData = newObjectDensityMapData;

                var cmd = this.CreateSetValueCommand((rootObj, val) => rootObj.ObjectDensityMapData = val, oldValue: oldObjectDensityMapData, newValue: newObjectDensityMapData);
                assetTransactionBuilder.AddCommand(cmd);
            }
        }
    }

    public void ApplyObjectDensityMapAdjustments(Array2d<float> adjustmentObjectDensityMapData, Int2 startPosition, AssetTransactionBuilder assetTransactionBuilder)
    {
        if (ObjectDensityMapData is null)
        {
            Debug.WriteLine($"ObjectDensityMapData not assigned.");
            return;
        }

        var cmd = new ModifyArray2dCommand<Half>(ObjectDensityMapData);

        int maxXExcl = Math.Min(ObjectDensityMapData.LengthX - startPosition.X + 1, adjustmentObjectDensityMapData.LengthX);
        int maxYExcl = Math.Min(ObjectDensityMapData.LengthY - startPosition.Y + 1, adjustmentObjectDensityMapData.LengthY);
        Debug.WriteLineIf(adjustmentObjectDensityMapData.LengthX < maxXExcl, $"{nameof(ApplyObjectDensityMapAdjustments)}: adjustmentWeightMapData will be truncated on x-axis - expected length: {adjustmentObjectDensityMapData.LengthX}, actual length: {maxXExcl}");
        Debug.WriteLineIf(adjustmentObjectDensityMapData.LengthY < maxYExcl, $"{nameof(ApplyObjectDensityMapAdjustments)}: adjustmentWeightMapData will be truncated on y-axis - expected length: {adjustmentObjectDensityMapData.LengthY}, actual length: {maxYExcl}");

        for (int y = 0; y < maxYExcl; y++)
        {
            for (int x = 0; x < maxXExcl; x++)
            {
                var adjustmentMapIndex = new Int2(x, y);
                var densityMapIndex = adjustmentMapIndex + startPosition;

                float adjustmentValue = adjustmentObjectDensityMapData[adjustmentMapIndex];
                if (adjustmentValue == 0)
                {
                    continue;
                }
                float curValue = (float)ObjectDensityMapData[densityMapIndex];
                curValue += adjustmentValue;
                curValue = Math.Clamp(curValue, 0, 1);
                var prevValue = ObjectDensityMapData[densityMapIndex];
                var newValue = (Half)curValue;
                if (prevValue != newValue)
                {
                    ObjectDensityMapData[densityMapIndex] = newValue;
                    cmd.AddValueChange(densityMapIndex, prevValue, newValue);
                }
            }
        }

        assetTransactionBuilder.AddCommand(cmd);
    }
}
