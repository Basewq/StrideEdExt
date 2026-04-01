using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData.AssetSerialization;
using StrideEdExt.SharedData.ProceduralPlacement;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

public class ModelInstancingSpawnerData : ObjectSpawnerDataBase
{
    [Display(Browsable = false)]
    public DateTimeOffset? LastModifiedSourceFile { get; set; }

    //[DataMemberIgnore]
    public ObjectPlacementModelType ModelType { get; set; }

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger logger)
    {
        //if (AssetReferenceList is null)
        //{
        //    logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate AssetReference list.");
        //    return;
        //}
        //if (ObjectPlacementDataList is null)
        //{
        //    logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate ObjectPlacementData list.");
        //    return;
        //}

        var spawnerFullFilePath = this.GetFilePathOrDefaultPath(ObjectSpawnerFilePath, packageFolderPath, IntermediateObjectSpawnerFileNameFormat);
        SpawnerDataSerializationHelper.SerializeObjectPlacementsToFile(OnSerializeMetadata, SpawnPlacementDataList, spawnerFullFilePath);

        ObjectSpawnerFilePath = spawnerFullFilePath;
        return;

        void OnSerializeMetadata(AssetTextWriter writer)
        {
            //writer.Write(AssetReferenceList.Count);
            //writer.WriteTab();
            writer.Write(SpawnPlacementDataList.Count);
            writer.WriteTab();
            writer.Write(ModelType.ToString());
            writer.WriteLine();
        }
    }

    protected override void OnDeserializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger logger)
    {
        if (ObjectSpawnerFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var spawnerFullFilePath = this.GetFilePathOrDefaultPath(ObjectSpawnerFilePath, packageFolderPath, IntermediateObjectSpawnerFileNameFormat);
        if (!File.Exists(spawnerFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {spawnerFullFilePath}");
            return;
        }
        if (SpawnerDataSerializationHelper.TryDeserializeObjectPlacementsFromFile(
            spawnerFullFilePath, OnDeserializeMetadata, out var objectPlacementDataList, out var errorMessage))
        {
            SpawnPlacementDataList = objectPlacementDataList;
        }
        else
        {
            logger.Error(errorMessage);
        }
        return;

        bool OnDeserializeMetadata(StreamReader reader, out int assetRefListCount, out int objectPlacementDataListCount, [NotNullWhen(false)] out string? errorMessage)
        {
            assetRefListCount = 0;
            objectPlacementDataListCount = 0;
            errorMessage = null;

            var metadataLine = reader.ReadLine();

            const int TotalTokens = 2;
            Span<Range> tokenRanges = stackalloc Range[TotalTokens];
            var metadataLineSpan = metadataLine.AsSpan();
            int tokenCount = metadataLineSpan.Split(tokenRanges, '\t');
            if (tokenCount < TotalTokens)
            {
                errorMessage = $"Metadata value count mismatched. Expected: {TotalTokens} - Actual: {tokenCount}";
                return false;
            }
            int nextTokenIndex = 0;
            //if (!SpawnerDataSerializationHelper.TryReadNextInt(metadataLineSpan, tokenRanges, ref nextTokenIndex, out assetRefListCount, out errorMessage)
            //    || !SpawnerDataSerializationHelper.TryReadNextInt(metadataLineSpan, tokenRanges, ref nextTokenIndex, out objectPlacementDataListCount, out errorMessage)
            //    || !SpawnerDataSerializationHelper.TryReadNextString(metadataLineSpan, tokenRanges, ref nextTokenIndex, out var modelTypeString, out errorMessage))
            //{
            //    return false;
            //}
            if (!SpawnerDataSerializationHelper.TryReadNextInt(metadataLineSpan, tokenRanges, ref nextTokenIndex, out objectPlacementDataListCount, out errorMessage)
                || !SpawnerDataSerializationHelper.TryReadNextString(metadataLineSpan, tokenRanges, ref nextTokenIndex, out var modelTypeString, out errorMessage))
            {
                return false;
            }

            if (Enum.TryParse<ObjectPlacementModelType>(modelTypeString, out var parsedModelType))
            {
                ModelType = parsedModelType;
                return true;
            }
            else
            {
                errorMessage = $"Failed to parse ModelType: '{modelTypeString}'";
                return false;
            }
        }
    }

    ////protected override void OnTerrainMapResized(Size2 heightmapTextureSize, AssetTransactionBuilder assetTransactionBuilder)
    ////{
    ////    if (ObjectPlacementDataList is List<ObjectPlacementData> curObjectDensityMapData)
    ////    {
    ////        int maxNonZeroX = 0;
    ////        int maxNonZeroY = 0;
    ////        foreach (var (index, heightValue) in curObjectDensityMapData)
    ////        {
    ////            if (heightValue != Half.Zero)
    ////            {
    ////                maxNonZeroX = Math.Max(index.X, maxNonZeroX);
    ////                maxNonZeroY = Math.Max(index.Y, maxNonZeroY);
    ////            }
    ////        }
    ////        int resizeX = Math.Max(maxNonZeroX, heightmapTextureSize.Width);
    ////        int resizeY = Math.Max(maxNonZeroY, heightmapTextureSize.Height);
    ////        if (curObjectDensityMapData.LengthX < resizeX || curObjectDensityMapData.LengthY < resizeY)
    ////        {
    ////            var oldObjectDensityMapData = ObjectPlacementDataList;
    ////            var newObjectDensityMapData = new Array2d<Half>(resizeX, resizeY);
    ////            curObjectDensityMapData.CopyToUnaligned(newObjectDensityMapData);
    ////            ObjectPlacementDataList = newObjectDensityMapData;

    ////            var cmd = this.CreateSetValueCommand((rootObj, val) => rootObj.ObjectDensityMapData = val, oldValue: oldObjectDensityMapData, newValue: newObjectDensityMapData);
    ////            assetTransactionBuilder.AddCommand(cmd);
    ////        }
    ////    }
    ////}

    ////public void ApplyWeightMapAdjustments(Array2d<float> adjustmentWeightMapData, Int2 startPosition, AssetTransactionBuilder assetTransactionBuilder)
    ////{
    ////    if (ObjectDensityMapData is null)
    ////    {
    ////        Debug.WriteLine($"ObjectDensityMapData not assigned.");
    ////        return;
    ////    }

    ////    var cmd = new ModifyArray2dCommand<Half>(ObjectDensityMapData);

    ////    int maxXExcl = Math.Min(ObjectDensityMapData.LengthX - startPosition.X + 1, adjustmentWeightMapData.LengthX);
    ////    int maxYExcl = Math.Min(ObjectDensityMapData.LengthY - startPosition.Y + 1, adjustmentWeightMapData.LengthY);
    ////    Debug.WriteLineIf(adjustmentWeightMapData.LengthX < maxXExcl, $"{nameof(ApplyWeightMapAdjustments)}: adjustmentWeightMapData will be truncated on x-axis - expected length: {adjustmentWeightMapData.LengthX}, actual length: {maxXExcl}");
    ////    Debug.WriteLineIf(adjustmentWeightMapData.LengthY < maxYExcl, $"{nameof(ApplyWeightMapAdjustments)}: adjustmentWeightMapData will be truncated on y-axis - expected length: {adjustmentWeightMapData.LengthY}, actual length: {maxYExcl}");

    ////    for (int y = 0; y < maxYExcl; y++)
    ////    {
    ////        for (int x = 0; x < maxXExcl; x++)
    ////        {
    ////            var adjustmentMapIndex = new Int2(x, y);
    ////            var weightMapIndex = adjustmentMapIndex + startPosition;

    ////            float adjustmentValue = adjustmentWeightMapData[adjustmentMapIndex];
    ////            if (adjustmentValue == 0)
    ////            {
    ////                continue;
    ////            }
    ////            float curValue = (float)ObjectDensityMapData[weightMapIndex];
    ////            curValue += adjustmentValue;
    ////            curValue = Math.Clamp(curValue, 0, 1);
    ////            var prevValue = ObjectDensityMapData[weightMapIndex];
    ////            var newValue = (Half)curValue;
    ////            if (prevValue != newValue)
    ////            {
    ////                ObjectDensityMapData[weightMapIndex] = newValue;
    ////                cmd.AddValueChange(weightMapIndex, prevValue, newValue);
    ////            }
    ////        }
    ////    }

    ////    assetTransactionBuilder.AddCommand(cmd);
    ////}
}
