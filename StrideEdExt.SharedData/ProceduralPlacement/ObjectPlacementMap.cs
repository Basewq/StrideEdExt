using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Rendering;
using StrideEdExt.SharedData.Terrain3d;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.SharedData.ProceduralPlacement;

/**
 * This is the custom data as seen at run-time.
 */
[DataContract]
[ContentSerializer(typeof(DataContentSerializer<ObjectPlacementMap>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<ObjectPlacementMap>), Profile = "Content")]
public class ObjectPlacementMap
{
    [Display(Browsable = false)]
    public AssetId ObjectPlacementMapAssetId { get; set; }
    [Display(Browsable = false)]
    public AssetId? TerrainMapAssetId { get; set; }

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    public List<UrlReference<Model>> ModelAssetUrlRefList { get; set; } = [];
    public List<UrlReference<Prefab>> PrefabAssetUrlRefList { get; set; } = [];

    /// <remarks>
    /// Chunk Index to Content Url where content is the serialized binary of <see cref="ObjectPlacementsChunkData"/>.
    /// </remarks>
    [Display(Browsable = false)]
    public Dictionary<TerrainChunkIndex2d, string> ChunkIndexToChunkDataUrlMap { get; set; } = [];

    public void Initialize()
    {
        //RebuildChunks();
        IsInitialized = true;
    }

    public IEnumerable<TerrainChunkIndex2d> GetAllChunkIndices()
    {
        return ChunkIndexToChunkDataUrlMap.Keys;
    }

    public bool TryGetChunkDataUrl(TerrainChunkIndex2d chunkIndex, [NotNullWhen(true)] out string? chunkDataUrl)
    {
        if (ChunkIndexToChunkDataUrlMap.TryGetValue(chunkIndex, out chunkDataUrl))
        {
            return true;
        }
        chunkDataUrl = null;
        return false;
    }
}

//[DataContract]
public class ObjectPlacementsChunkData
{
    public static readonly object SerializerLock = new();   // TODO C# 13 System.Threading.Lock

    public List<ModelObjectPlacementsData> ModelPlacements { get; set; } = [];
    public List<PrefabObjectPlacementsData> PrefabPlacements { get; set; } = [];

    public void Serialize(BinarySerializationWriter binaryWriter)
    {
        binaryWriter.Write(ModelPlacements.Count);
        foreach (var plc in ModelPlacements)
        {
            binaryWriter.Write(plc);
        }

        binaryWriter.Write(PrefabPlacements.Count);
        foreach (var plc in PrefabPlacements)
        {
            binaryWriter.Write(plc);
        }
    }

    public void Deserialize(BinarySerializationReader binaryReader)
    {
        ModelPlacements.Clear();
        int modelPlacementCount = binaryReader.ReadInt32();
        ModelPlacements.EnsureCapacity(modelPlacementCount);
        for (int i = 0; i < modelPlacementCount; i++)
        {
            var plcData = binaryReader.Read<ModelObjectPlacementsData>();
            ModelPlacements.Add(plcData);
        }

        PrefabPlacements.Clear();
        int prefabPlacementCount = binaryReader.ReadInt32();
        PrefabPlacements.EnsureCapacity(prefabPlacementCount);
        for (int i = 0; i < prefabPlacementCount; i++)
        {
            var plcData = binaryReader.Read<PrefabObjectPlacementsData>();
            PrefabPlacements.Add(plcData);
        }
    }
}

//[DataContract]
//public class ObjectPlacementsPrefabAsset //: ObjectPlacementsAsset<Prefab>
//{
//    public List<PrefabObjectPlacementsData> Placements { get; set; } = [];
//}

//[DataContract]
//public class ObjectPlacementAssetSetBase<TAsset, TObjectPlacementAsset>
//    where TAsset : class
//    where TObjectPlacementAsset : ObjectPlacementsAsset<TAsset>
//{
//    public List<UrlReference<TAsset>> AssetUrlList { get; set; } = [];
//    public List<TObjectPlacementAsset> PlacementAssetList { get; set; } = [];
//}

//[DataContract]
//public enum ObjectPlacementAssetType
//{
//    Model,
//    Prefab
//}

[DataContract]
public enum ObjectPlacementModelType
{
    Static,
    Foliage
}

[DataContract]
public record struct ObjectPlacementData
{
    // All data in world space (except SurfaceNormalModelSpace).
    public required Vector3 Position;
    public required Quaternion Orientation;
    public required Vector3 Scale;
    public required Vector3 SurfaceNormalModelSpace;

    public readonly Matrix GetWorldTransformMatrix()
    {
        Matrix.Transformation(in Scale, in Orientation, in Position, out var transformMatrix);
        return transformMatrix;
    }
}

[DataContract]
public record struct ModelObjectPlacementsData
{
    public required int ModelAssetUrlListIndex;
    public required ObjectPlacementModelType ModelType;
    public required List<ObjectPlacementData> Placements;
}

[DataContract]
public record struct PrefabObjectPlacementsData
{
    public required int PrefabAssetUrlListIndex;
    public required List<ObjectPlacementData> Placements;
}

[DataContract]
public record struct ChunkIndexChunkDataUrlData
{
    public required TerrainChunkIndex2d ChunkIndex;
    public required string ChunkDataUrl;
}
