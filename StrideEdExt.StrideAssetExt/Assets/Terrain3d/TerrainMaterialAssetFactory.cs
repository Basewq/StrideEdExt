using Stride.Core.Assets;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

public class TerrainMaterialAssetFactory : AssetFactory<TerrainMaterialAsset>
{
    public override TerrainMaterialAsset New()
    {
        // Can set up default values.
        return new TerrainMaterialAsset
        {

        };
    }
}
