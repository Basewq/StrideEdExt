using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData.AssetSerialization;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

public class ModelInstancingSpawnerData : ObjectSpawnerDataBase
{
    [Display(Browsable = false)]
    public DateTimeOffset? LastModifiedSourceFile { get; set; }

    //[DataMemberIgnore]
    public ObjectPlacementModelType ModelType { get; set; }

    protected override void OnSerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger? logger)
    {
        string spawnerFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectSpawnerFileNameFormat);
        SpawnerDataSerializationHelper.SerializeObjectPlacementsToFile(OnSerializeMetadata, SpawnPlacementDataList, spawnerFullFilePath);

        ObjectSpawnerRelativeFilePath = new UFile(spawnerFullFilePath).MakeRelative(objectPlacementMapAssetFullFolderPath);
        return;

        void OnSerializeMetadata(AssetTextWriter writer)
        {
            writer.Write(SpawnPlacementDataList.Count);
            writer.WriteTab();
            writer.Write(ModelType.ToString());
            writer.WriteLine();
        }
    }

    protected override void OnDeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger? logger)
    {
        if (ObjectSpawnerRelativeFilePath is null)
        {
            logger?.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        string spawnerFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectSpawnerFileNameFormat);
        if (!File.Exists(spawnerFullFilePath))
        {
            logger?.Info($"Intermediate file for layer {LayerId} does not exist: {spawnerFullFilePath}");
            return;
        }
        if (SpawnerDataSerializationHelper.TryDeserializeObjectPlacementsFromFile(
            spawnerFullFilePath, OnDeserializeMetadata, out var objectPlacementDataList, out var errorMessage))
        {
            SpawnPlacementDataList = objectPlacementDataList;
        }
        else
        {
            logger?.Error(errorMessage);
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
}
