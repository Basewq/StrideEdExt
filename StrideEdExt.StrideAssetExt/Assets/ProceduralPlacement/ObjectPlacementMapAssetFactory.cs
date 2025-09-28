using Stride.Core.Assets;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement;

public class ObjectPlacementMapAssetFactory : AssetFactory<ObjectPlacementMapAsset>
{
    public override ObjectPlacementMapAsset New()
    {
        // Can set up default values.
        return new ObjectPlacementMapAsset
        {

        };
    }
}
