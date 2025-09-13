using Stride.Core.Assets;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

public class TerrainMapAssetFactory : AssetFactory<TerrainMapAsset>
{
    public override TerrainMapAsset New()
    {
        // Can set up default values.
        return new TerrainMapAsset
        {

        };
    }
}
