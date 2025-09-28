using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Rendering;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement;

/**
 * The asset as seen by Game Studio.
 * Refer to Stride's source could for additional asset options, eg. referencing a file.
 */
[DataContract(Inherited = true)]
public abstract class ObjectAssetDefinitionBase
{
    /// <summary>
    /// Distance between each object from the same object type (assuming no position offset randomization).
    /// </summary>
    public float ObjectSpacing { get; set; }

    /// <summary>
    /// Radius to block any objects from being placed within the object's range.
    /// Used to prevent objects from overlapping.
    /// </summary>
    public float CollisionRadius { get; set; }
}

public class ModelObjectAssetDefinition : ObjectAssetDefinitionBase
{
    public UrlReference<Model>? ModelUrl { get; set; }//ColliderShapeAsset
}

public class PrefabObjectAssetDefinition : ObjectAssetDefinitionBase
{
    public UrlReference<Prefab>? PrefabUrl { get; set; }
}
