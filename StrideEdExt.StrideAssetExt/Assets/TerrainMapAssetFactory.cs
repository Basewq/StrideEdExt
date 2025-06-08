using Stride.Core.Assets;

namespace SceneEditorExtensionExample.StrideAssetExt.Assets;

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
