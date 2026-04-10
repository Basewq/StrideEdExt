using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData.AssetSerialization;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

public class ManualPrefabSpawnerData : ObjectSpawnerDataBase
{
    [Display(Browsable = false)]
    public DateTimeOffset? LastModifiedSourceFile { get; set; }

    /// <summary>
    /// List corresponds 1-1 with <see cref="ObjectSpawnerDataBase.SpawnAssetDefinitionList"/>.
    /// </summary>
    public List<Guid> SpawnAssetDefinitionSpawnInstancingIdList { get; set; } = [];

    [DataMemberIgnore]
    public new List<ObjectPlacementManualPrefabSpawnPlacementData> SpawnPlacementDataList { get; set; } = [];

    protected override void OnSerializeIntermediateFile(UDirectory packageFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger logger)
    {
        var spawnerFullFilePath = this.GetFilePathOrDefaultPath(ObjectSpawnerFilePath, packageFolderPath, IntermediateObjectSpawnerFileNameFormat);
        SpawnerDataSerializationHelper.SerializeObjectPlacementsToFile(OnSerializeMetadata, SpawnPlacementDataList, spawnerFullFilePath);

        ObjectSpawnerFilePath = spawnerFullFilePath;
        return;

        void OnSerializeMetadata(AssetTextWriter writer)
        {
            writer.Write(SpawnPlacementDataList.Count);
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
            spawnerFullFilePath, OnDeserializeMetadata, out List<ObjectPlacementManualPrefabSpawnPlacementData>? objectPlacementDataList, out var errorMessage))
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

            const int TotalTokens = 1;
            Span<Range> tokenRanges = stackalloc Range[TotalTokens];
            var metadataLineSpan = metadataLine.AsSpan();
            int tokenCount = metadataLineSpan.Split(tokenRanges, '\t');
            if (tokenCount < TotalTokens)
            {
                errorMessage = $"Metadata value count mismatched. Expected: {TotalTokens} - Actual: {tokenCount}";
                return false;
            }
            int nextTokenIndex = 0;
            if (!SpawnerDataSerializationHelper.TryReadNextInt(metadataLineSpan, tokenRanges, ref nextTokenIndex, out objectPlacementDataListCount, out errorMessage))
            {
                return false;
            }

            return true;
        }
    }
}
