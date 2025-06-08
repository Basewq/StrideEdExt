using Stride.Assets.Entities;
using Stride.Assets.Materials;
using Stride.Assets.Models;
using Stride.Assets.Textures;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.IO;
using Stride.Core.Serialization.Contents;

namespace SceneEditorExtensionExample.StrideAssetExt.Assets;

[AssetCompiler(typeof(TerrainMapAsset), typeof(AssetCompilationContext))]
public class TerrainMapAssetCompiler : AssetCompilerBase
{
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        // We depend on the following Assets to ensure if TerrainMapAsset is the only thing that is referencing the model asset, then it
        // will actually be included in the build, otherwise Stride may think the model isn't being used.
        yield return new BuildDependencyInfo(typeof(PrefabAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(ModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(ProceduralModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(PrefabModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(MaterialAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(TextureAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var packageFolderPath = assetItem.Package.FullPath.GetFullDirectory();
        var asset = (TerrainMapAsset)assetItem.Asset;
        //asset.ChunkCount = asset.Chunks.Count;
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new TerrainMapAssetCommand(targetUrlInStorage, asset, assetItem.Package, packageFolderPath));
    }

    private class TerrainMapAssetCommand : AssetCommand<TerrainMapAsset>
    {
        private UDirectory _packageFolderPath;

        public TerrainMapAssetCommand(string url, TerrainMapAsset parameters, IAssetFinder assetFinder, Stride.Core.IO.UDirectory packageFolderPath)
            : base(url, parameters, assetFinder)
        {
            Version = 1;
            _packageFolderPath = packageFolderPath;
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var assetManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);

            // Converts the 'asset' object into the real 'definition' object which will be serialised.
            var terrainMapAsset = Parameters;
            terrainMapAsset.EnsureFinalizeContentDeserialization(commandContext.Logger, _packageFolderPath);
            terrainMapAsset.PrepareContentSerialization();
            var terrain = terrainMapAsset.ToTerrain(commandContext.Logger);
            assetManager.Save(Url, terrain);

            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
