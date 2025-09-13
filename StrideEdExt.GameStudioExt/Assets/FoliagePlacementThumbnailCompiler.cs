using StrideEdExt.GameStudioExt.Resources;
using StrideEdExt.StrideAssetExt.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Editor.Thumbnails;

namespace StrideEdExt.GameStudioExt.Assets
{
    [AssetCompiler(typeof(FoliagePlacementAsset), typeof(ThumbnailCompilationContext))]
    public class FoliagePlacementThumbnailCompiler : StaticThumbnailCompiler<FoliagePlacementAsset>
    {
        public FoliagePlacementThumbnailCompiler()
            : base(StrideEdExtAssetsThumbnails.FoliageThumbnail)
        {
        }
    }
}
