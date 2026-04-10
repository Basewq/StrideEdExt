using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement;

/// <summary>
/// Entity with this component should be placed as a sub-entity to the entity with
/// <see cref="Layers.Spawners.ManualPrefabSpawnerComponent"/>, to inform that the entity
/// with this component should generate a manual prefab at this entity's location.
/// </summary>
[ComponentCategory("Procedural")]
[DataContract]
[DefaultEntityComponentProcessor(typeof(ManualPrefabSpawnInstancingProcessor), ExecutionMode = ExecutionMode.Editor)]
public class ManualPrefabSpawnInstancingComponent : EntityComponent
{
    [DataMemberIgnore]
    public Guid SpawnInstancingId => Id;      // Use the entity component's ID as the layer ID

    public bool IsEnabled { get; set; } = true;

    public Prefab? Prefab { get; set; }
    public float CollisionRadius { get; set; }
}
