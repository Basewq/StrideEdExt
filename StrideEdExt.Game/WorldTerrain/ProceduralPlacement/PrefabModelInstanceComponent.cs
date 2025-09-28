using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Rendering;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement;

/// <summary>
/// Tag component to inform <see cref="ProceduralObjectPlacementComponent"/> that the entity
/// with this component should generate an entity with <see cref="InstancingComponent"/> to
/// link with this entity.
/// </summary>
[ComponentCategory("Procedural")]
[DataContract]
public class PrefabModelInstanceComponent : EntityComponent
{
    public UrlReference<Model>? ModelUrlRef { get; set; }
}
