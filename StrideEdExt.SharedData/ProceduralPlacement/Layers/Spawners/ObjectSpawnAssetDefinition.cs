using Stride.Core;

namespace StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;

/// <summary>
/// Class that defines what asset should be used to spawn an object,
/// along with spawn rules for this particular asset.
/// </summary>
[DataContract]
public class ObjectSpawnAssetDefinition
{
    public string? AssetUrl { get; set; }
    /// <summary>
    /// Weight value proportion to determine if this specific asset object should be spawned
    /// for a given pool of assets.
    /// </summary>
    public float SpawnWeightValue { get; set; } = 1;
    public float CollisionRadius { get; set; } = 0.5f;
}
