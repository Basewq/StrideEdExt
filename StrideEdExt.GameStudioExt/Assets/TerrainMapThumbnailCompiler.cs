using StrideEdExt.GameStudioExt.Resources;
using Stride.Core.Assets.Compiler;
using Stride.Editor.Thumbnails;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d;

namespace StrideEdExt.GameStudioExt.Assets;

[AssetCompiler(typeof(TerrainMapAsset), typeof(ThumbnailCompilationContext))]
public class TerrainMapThumbnailCompiler : StaticThumbnailCompiler<TerrainMapAsset>
{
    public TerrainMapThumbnailCompiler()
        : base(StrideEdExtAssetsThumbnails.TerrainMapThumbnail)
    {
    }
}
