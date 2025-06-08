using SceneEditorExtensionExample.GameStudioExt.Resources;
using SceneEditorExtensionExample.StrideAssetExt.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Editor.Thumbnails;

namespace SceneEditorExtensionExample.GameStudioExt.Assets;

[AssetCompiler(typeof(TerrainMapAsset), typeof(ThumbnailCompilationContext))]
public class TerrainMapThumbnailCompiler : StaticThumbnailCompiler<TerrainMapAsset>
{
    public TerrainMapThumbnailCompiler()
        : base(SceneEditorExtensionExampleAssetsThumbnails.TerrainMapThumbnail)
    {
    }
}
