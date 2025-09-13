using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d;
using Stride.Assets;
using Stride.Assets.Entities;
using Stride.Assets.Materials;
using Stride.Assets.Models;
using Stride.Assets.Textures;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.IO;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Stride.TextureConverter;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

[AssetCompiler(typeof(TerrainMapAsset), typeof(AssetCompilationContext))]
public class TerrainMapAssetCompiler : AssetCompilerBase
{
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        yield return new BuildDependencyInfo(typeof(GameSettingsAsset), typeof(AssetCompilationContext), BuildDependencyType.CompileAsset);
        // We depend on the following Assets to ensure if TerrainMapAsset is the only thing that is referencing the model asset, then it
        // will actually be included in the build, otherwise Stride may think the model isn't being used.
        yield return new BuildDependencyInfo(typeof(PrefabAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(ModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(ProceduralModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(PrefabModelAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(MaterialAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(TextureAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(TerrainMaterialAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var packageFolderPath = assetItem.Package.FullPath.GetFullDirectory();
        var asset = (TerrainMapAsset)assetItem.Asset;
        //asset.ChunkCount = asset.Chunks.Count;
        result.BuildSteps = new AssetBuildStep(assetItem);

        var gameSettingsAsset = context.GetGameSettingsAsset();
        var texImportParams = new TerrainTextureImportParameters
        {
            TextureQuality = gameSettingsAsset.GetOrCreate<TextureSettings>().TextureQuality,
            GraphicsPlatform = context.GetGraphicsPlatform(assetItem.Package),
            GraphicsProfile = gameSettingsAsset.GetOrCreate<RenderingSettings>(context.Platform).DefaultGraphicsProfile,
            Platform = context.Platform,
            ColorSpace = context.GetColorSpace(),
        };
        result.BuildSteps.Add(new TerrainMapAssetCommand(targetUrlInStorage, asset, assetItem.Package, packageFolderPath, texImportParams));
    }

    private class TerrainMapAssetCommand : AssetCommand<TerrainMapAsset>
    {
        private UDirectory _packageFolderPath;
        private TerrainTextureImportParameters _textureImportParams;

        public TerrainMapAssetCommand(string url, TerrainMapAsset parameters, IAssetFinder assetFinder, UDirectory packageFolderPath, TerrainTextureImportParameters textureImportParams)
            : base(url, parameters, assetFinder)
        {
            Version = 1;
            _packageFolderPath = packageFolderPath;
            _textureImportParams = textureImportParams;
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var assetManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);

            // Converts the 'asset' object into the real 'definition' object which will be serialised.
            var terrainMapAsset = Parameters;
            terrainMapAsset.EnsureFinalizeContentDeserialization(commandContext.Logger, _packageFolderPath);
            terrainMapAsset.PrepareContentSerialization();
            var terrainMap = terrainMapAsset.ToTerrainMap(commandContext.Logger);

            if (terrainMapAsset.MaterialIndexMapData is Array2d<byte> materialIndexMapData)
            {
                try
                {
                    using var texTool = new TextureTool();

                    using var materialMapImage = Image.New2D(
                        width: materialIndexMapData.LengthX, height: materialIndexMapData.LengthY,
                        mipMapCount: MipMapCount.Auto, format: PixelFormat.R8_UNorm);
                    var pixelBuffer = materialMapImage.PixelBuffer[0];     // Should only have one
                    for (int y = 0; y < pixelBuffer.Height; y++)
                    {
                        for (int x = 0; x < pixelBuffer.Width; x++)
                        {
                            // TODO: should bulk set...
                            var byteValue = materialIndexMapData[x, y];
                            pixelBuffer.SetPixel(x, y, byteValue);
                        }
                    }

                    var texImage = texTool.Load(materialMapImage, isSRgb: false);
                    var texImportParams = GetMaterialMapImportParameters(Url + "_MaterialIndexMap");
                    commandContext.Logger.Info($"Processing Terrain Material Index Map Texture: Writing to {texImportParams.OutputUrl}");
                    var importResult = ImportTexture(commandContext, assetManager, texTool, texImage, texImportParams);
                    if (importResult != ResultStatus.Successful && importResult != ResultStatus.NotTriggeredWasSuccessful)
                    {
                        return Task.FromResult(importResult);
                    }
                    terrainMap.MaterialIndexMapTexture = AttachedReferenceManager.CreateProxyObject<Texture>(AssetId.Empty, texImportParams.OutputUrl);
                }
                catch (Exception ex)
                {
                    commandContext.Logger.Error($"Failed to process Terrain Material.", ex);
                    return Task.FromResult(ResultStatus.Failed);
                }
            }

            if (terrainMap.TerrainMaterial is TerrainMaterial terrainMaterial)
            {
                var terrainMaterialAttachedRef = AttachedReferenceManager.GetAttachedReference(terrainMaterial);
                if (terrainMaterialAttachedRef is not null)
                {
                    if (!terrainMaterialAttachedRef.IsProxy)
                    {
                        terrainMap.TerrainMaterial = AttachedReferenceManager.CreateProxyObject<TerrainMaterial>(AssetId.Empty, terrainMaterialAttachedRef.Url);
                    }
                }
            }

            //commandContext.Logger.Info($"TerrainMapAsset.Chunks: {terrainMapAsset.Chunks.Count} - Terrain.LoadedChunkCount: {terrain.LoadedChunkCount}");
            //commandContext.Logger.Info($"TerrainMapAsset.TileSets: {terrainMapAsset.TileSets.Count} - Terrain.TileSetNameToTileSetPrefabMap: {terrain.TileSetNameToTileSetPrefabMap.Count}");

            assetManager.Save(Url, terrainMap);

            return Task.FromResult(ResultStatus.Successful);
        }

        // Code from Stride.Assets.Textures.TextureAssetCompiler
        private ResultStatus ImportTexture(
            ICommandContext commandContext, ContentManager assetManager,
            TextureTool textureTool, TexImage texImage, TextureHelper.ImportParameters convertParameters)
        {
            var useSeparateDataContainer = TextureHelper.ShouldUseDataContainer(isStreamable: false, texImage.Dimension);
            // Note: for streamable textures we want to store mip maps in a separate storage container and read them on request instead of whole asset deserialization (at once)
            return useSeparateDataContainer
                ? TextureHelper.ImportStreamableTextureImage(assetManager, textureTool, texImage, convertParameters, CancellationToken, commandContext)
                : TextureHelper.ImportTextureImage(assetManager, textureTool, texImage, convertParameters, CancellationToken, commandContext.Logger);
        }

        private TextureHelper.ImportParameters GetMaterialMapImportParameters(string outputUrl)
        {
            //var terrainMaterialAsset = Parameters;
            var convParams = new TextureConvertParameters
            {
                TextureQuality = _textureImportParams.TextureQuality,
                GraphicsPlatform = _textureImportParams.GraphicsPlatform,
                GraphicsProfile = _textureImportParams.GraphicsProfile,
                Platform = _textureImportParams.Platform,
                ColorSpace = _textureImportParams.ColorSpace,
            };
            // HACK: needed to construct TextureHelper.ImportParameters
            convParams.Texture = new TextureAsset
            {
                IsCompressed = false,
                GenerateMipmaps = false,
                Type = new GrayscaleTextureType(),      // Can leave with default settings
            };
            var impParams = new TextureHelper.ImportParameters(convParams)
            {
                OutputUrl = outputUrl,
            };
            return impParams;
        }
    }

    [DataContract]
    public class TerrainTextureImportParameters
    {
        public TextureQuality TextureQuality;
        public GraphicsPlatform GraphicsPlatform;
        public GraphicsProfile GraphicsProfile;
        public PlatformType Platform;
        public ColorSpace ColorSpace;
    }
}
