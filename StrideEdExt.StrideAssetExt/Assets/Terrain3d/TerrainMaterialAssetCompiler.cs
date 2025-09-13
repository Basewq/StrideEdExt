using StrideEdExt.SharedData.Terrain3d;
using Stride.Assets;
using Stride.Assets.Textures;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Stride.TextureConverter;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

[AssetCompiler(typeof(TerrainMaterialAsset), typeof(AssetCompilationContext))]
public class TerrainMaterialAssetCompiler : AssetCompilerBase
{
    public override IEnumerable<BuildDependencyInfo> GetInputTypes(AssetItem assetItem)
    {
        yield return new BuildDependencyInfo(typeof(GameSettingsAsset), typeof(AssetCompilationContext), BuildDependencyType.CompileAsset);
        //yield return new BuildDependencyInfo(typeof(MaterialAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
        yield return new BuildDependencyInfo(typeof(TextureAsset), typeof(AssetCompilationContext), BuildDependencyType.Runtime | BuildDependencyType.CompileContent);
    }

    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var asset = (TerrainMaterialAsset)assetItem.Asset;
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
        result.BuildSteps.Add(new TerrainMaterialAssetCommand(targetUrlInStorage, asset, assetItem.Package, texImportParams));
    }

    private class TerrainMaterialAssetCommand : AssetCommand<TerrainMaterialAsset>
    {
        private TerrainTextureImportParameters _textureImportParams;

        public TerrainMaterialAssetCommand(string url, TerrainMaterialAsset parameters, IAssetFinder assetFinder, TerrainTextureImportParameters textureImportParams)
            : base(url, parameters, assetFinder)
        {
            Version = 1;
            _textureImportParams = textureImportParams;
        }

        //protected override void ComputeAssemblyHash(BinarySerializationWriter writer)
        //{
        //    // Code from Stride.Assets.Textures.TextureAssetCompiler
        //    writer.Write(DataSerializer.BinaryFormatVersion);
        //    //writer.Write(TextureSerializationData.Version);

        //    // Since Image format is quite stable, we want to manually control it's assembly hash here
        //    writer.Write(1);
        //}

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var assetManager = new ContentManager(MicrothreadLocalDatabases.ProviderService);

            // Converts the 'asset' object into the real 'definition' object which will be serialised.
            var terrainMaterialAsset = Parameters;
            var terrainMaterial = new TerrainMaterial();

            var disposableList = new List<IDisposable>();
            try
            {
                var logger = commandContext.Logger;
                using var texTool = new TextureTool();

                var graphicsDevice = GraphicsDevice.New();
                //var graphicsContext = new GraphicsContext(graphicsDevice);

                {   // Diffuse Map
                    TexImage? dummyTexImage = null;
                    var textureSize = terrainMaterialAsset.DiffuseTextureSize.ToSize2();
                    var diffuseMapTextures = terrainMaterialAsset.MaterialLayers.Select(x => x.DiffuseMap).ToList();
                    var diffuseMapImportParams = GetDiffuseImportParameters(Url + "_DiffuseMap", textureSize);
                    using var diffuseMapTextureArray = CreateDiffuseTexture2dArray(
                        diffuseMapTextures, textureSize,
                        ref dummyTexImage, texTool, logger);
                    if (diffuseMapTextureArray is not null)
                    {
                        var importResult = ImportTexture(commandContext, assetManager, texTool, diffuseMapTextureArray, diffuseMapImportParams);
                        if (importResult != ResultStatus.Successful && importResult != ResultStatus.NotTriggeredWasSuccessful)
                        {
                            return Task.FromResult(importResult);
                        }
                        terrainMaterial.DiffuseMapTextureArray = AttachedReferenceManager.CreateProxyObject<Texture>(AssetId.Empty, diffuseMapImportParams.OutputUrl);
                    }
                    if (dummyTexImage is not null)
                    {
                        disposableList.Add(dummyTexImage);
                    }
                }
                {   // Normal Map
                    TexImage? dummyTexImage = null;
                    var textureSize = terrainMaterialAsset.NormalMapTextureSize.ToSize2();
                    var normalMapTextures = terrainMaterialAsset.MaterialLayers.Select(x => x.NormalMap).ToList();
                    var normalMapInvertYList = terrainMaterialAsset.MaterialLayers.Select(x => x.NormalMapInvertY).ToList();
                    var normalMapImportParams = GetNormalMapImportParameters(Url + "_NormalMap", textureSize);
                    using var normalMapTextureArray = CreateNormalMapTexture2dArray(
                        normalMapTextures, normalMapInvertYList, textureSize,
                        ref dummyTexImage, texTool, logger);
                    if (normalMapTextureArray is not null)
                    {
                        var importResult = ImportTexture(commandContext, assetManager, texTool, normalMapTextureArray, normalMapImportParams);
                        if (importResult != ResultStatus.Successful && importResult != ResultStatus.NotTriggeredWasSuccessful)
                        {
                            return Task.FromResult(importResult);
                        }
                        terrainMaterial.NormalMapTextureArray = AttachedReferenceManager.CreateProxyObject<Texture>(AssetId.Empty, normalMapImportParams.OutputUrl);
                    }
                    if (dummyTexImage is not null)
                    {
                        disposableList.Add(dummyTexImage);
                    }
                }
                {   // Height Blend Map
                    TexImage? dummyTexImage = null;
                    var textureSize = terrainMaterialAsset.HeightBlendMapTextureSize.ToSize2();
                    var heightBlendMapTextures = terrainMaterialAsset.MaterialLayers.Select(x => x.HeightBlendMap).ToList();
                    var heightBlendMapImportParams = GetHeightBlendImportParameters(Url + "_HeightBlendMap", textureSize);
                    using var heightBlendMapTextureArray = CreateGreyscaleTexture2dArray(
                        heightBlendMapTextures, textureSize,
                        ref dummyTexImage, texTool, logger);
                    if (heightBlendMapTextureArray is not null)
                    {
                        var importResult = ImportTexture(commandContext, assetManager, texTool, heightBlendMapTextureArray, heightBlendMapImportParams);
                        if (importResult != ResultStatus.Successful && importResult != ResultStatus.NotTriggeredWasSuccessful)
                        {
                            return Task.FromResult(importResult);
                        }
                        terrainMaterial.HeightBlendMapTextureArray = AttachedReferenceManager.CreateProxyObject<Texture>(AssetId.Empty, heightBlendMapImportParams.OutputUrl);
                    }
                    if (dummyTexImage is not null)
                    {
                        disposableList.Add(dummyTexImage);
                    }
                }
            }
            catch (Exception ex)
            {
                commandContext.Logger.Error($"Failed to process Terrain Material.", ex);
                return Task.FromResult(ResultStatus.Failed);
            }

            assetManager.Save(Url, terrainMaterial);

            foreach (var disp in disposableList)
            {
                disp.Dispose();
            }
            return Task.FromResult(ResultStatus.Successful);
        }

        // Code from Stride.Assets.Textures.TextureAssetCompiler
        private ResultStatus ImportTexture(
            ICommandContext commandContext, ContentManager assetManager,
            TextureTool textureTool, TexImage texImage, TextureHelper.ImportParameters convertParameters)
        {
            var useSeparateDataContainer = TextureHelper.ShouldUseDataContainer(Parameters.IsStreamable, texImage.Dimension);
            // Note: for streamable textures we want to store mip maps in a separate storage container and read them on request instead of whole asset deserialization (at once)
            return useSeparateDataContainer
                ? TextureHelper.ImportStreamableTextureImage(assetManager, textureTool, texImage, convertParameters, CancellationToken, commandContext)
                : TextureHelper.ImportTextureImage(assetManager, textureTool, texImage, convertParameters, CancellationToken, commandContext.Logger);
        }

        private TexImage CreateDummyTexture<TValue>(
            Size2 textureSize, PixelFormat pixelFormat, TValue defaultValue,
            TextureTool texTool)
            where TValue : struct
        {
            var image = Image.New2D(textureSize.Width, textureSize.Height, mipMapCount: MipMapCount.Auto, pixelFormat);
            var pixelBuffer = image.PixelBuffer[0];     // Should only have one
            // TODO: should bulk set...
            for (int y = 0; y < pixelBuffer.Height; y++)
            {
                for (int x = 0; x < pixelBuffer.Width; x++)
                {
                    pixelBuffer.SetPixel(x, y, defaultValue);
                }
            }
            bool isSRgb = false;// TODO: isSRgb?
            var texImage = texTool.Load(image, isSRgb);

            image.Dispose();
            return texImage;
        }

        private TextureHelper.ImportParameters GetDiffuseImportParameters(string outputUrl, Size2 textureSize)
        {
            var terrainMaterialAsset = Parameters;
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
                Width = textureSize.Width,
                Height = textureSize.Height,
                IsSizeInPercentage = false,
                IsCompressed = terrainMaterialAsset.IsCompressed,
                GenerateMipmaps = terrainMaterialAsset.GenerateMipmaps,
            };
            ((ColorTextureType)convParams.Texture.Type).Alpha = AlphaFormat.None;
            var impParams = new TextureHelper.ImportParameters(convParams)
            {
                OutputUrl = outputUrl,
            };
            return impParams;
        }

        private TextureHelper.ImportParameters GetNormalMapImportParameters(string outputUrl, Size2 textureSize)
        {
            var terrainMaterialAsset = Parameters;
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
                Width = textureSize.Width,
                Height = textureSize.Height,
                IsSizeInPercentage = false,
                IsCompressed = terrainMaterialAsset.IsCompressed,
                GenerateMipmaps = terrainMaterialAsset.GenerateMipmaps,
                Type = new NormapMapTextureType
                {
                    InvertY = false                 // We invert when packing
                },
            };
            var impParams = new TextureHelper.ImportParameters(convParams)
            {
                OutputUrl = outputUrl,
            };
            return impParams;
        }

        private TextureHelper.ImportParameters GetHeightBlendImportParameters(string outputUrl, Size2 textureSize)
        {
            var terrainMaterialAsset = Parameters;
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
                Width = textureSize.Width,
                Height = textureSize.Height,
                IsSizeInPercentage = false,
                IsCompressed = terrainMaterialAsset.IsCompressed,
                GenerateMipmaps = terrainMaterialAsset.GenerateMipmaps,
                Type = new GrayscaleTextureType(),      // Can leave with default settings
            };
            var impParams = new TextureHelper.ImportParameters(convParams)
            {
                OutputUrl = outputUrl,
            };
            return impParams;
        }

        private TexImage? CreateDiffuseTexture2dArray(
            List<Texture?> textures, Size2 textureSize,
            ref TexImage? dummyTexImage, TextureTool texTool, ILogger logger)
        {
            if (textures.Count == 0 || textures.All(x => x is null))
            {
                return null;
            }

            int rgba16Count = 0;
            int rgba8Count = 0;
            int bgra8Count = 0;
            const bool isSRgb = true;
            var texImages = new List<TexImage>(capacity: textures.Count);
            for (int i = 0; i < textures.Count; i++)
            {
                TexImage texImage;

                var tex = textures[i];
                if (tex is not null)
                {
                    var texAssetItem = AssetFinder.FindAssetFromProxyObject(tex);
                    if (texAssetItem.Asset is not TextureAsset texAsset)
                    {
                        throw new Exception($"{Url} - TextureAsset not found at index {i}");
                    }
                    else if (texAsset.Source is null)
                    {
                        throw new Exception($"{Url} - TextureAsset file path not specified at index {i}");
                    }
                    texImage = texTool.Load(texAsset.Source, isSRgb);
                    switch (texImage.Format)
                    {
                        case PixelFormat.R16G16B16A16_UNorm:
                            rgba16Count++;
                            break;
                        case PixelFormat.R8G8B8A8_UNorm_SRgb:
                            rgba8Count++;
                            break;
                        case PixelFormat.B8G8R8A8_UNorm_SRgb:
                            bgra8Count++;
                            break;
                    }
                    texTool.Decompress(texImage, texImage.Format.IsSRgb());
                    if (texImage.Width != textureSize.Width || texImage.Height != textureSize.Height)
                    {
                        logger.Warning($"{Url} - Inconsistent diffuse texture size at index {i}. Changing from ({texImage.Width},{texImage.Height}) to {textureSize}");
                        texTool.Resize(texImage, textureSize.Width, textureSize.Height, Filter.Rescaling.Nearest);
                    }
                }
                else
                {
                    texImage = null!;
                }

                texImages.Add(texImage);
            }

            // Convert texture format to all be the same
            var texArrayPixelFormat = PixelFormat.R8G8B8A8_UNorm_SRgb;
            if (rgba16Count > 0)
            {
                texArrayPixelFormat = PixelFormat.R16G16B16A16_UNorm;
            }
            else if (bgra8Count > rgba8Count)
            {
                texArrayPixelFormat = PixelFormat.B8G8R8A8_UNorm_SRgb;
            }
            for (int i = 0; i < texImages.Count; i++)
            {
                var texImage = texImages[i];
                if (texImage is null)
                {
                    dummyTexImage ??= CreateDummyTexture(textureSize, texArrayPixelFormat, defaultValue: Color.Zero, texTool);
                    texImages[i] = dummyTexImage;
                }
                else if (texImage.Format != texArrayPixelFormat)
                {
                    logger.Warning($"{Url} - Inconsistent diffuse texture pixel format at index {i}. Changing from {texImage.Format} to {texArrayPixelFormat}");
                    texTool.Convert(texImage, texArrayPixelFormat);
                }
            }

            var texImageArray = texTool.CreateTextureArray(texImages);
            return texImageArray;
        }

        private TexImage? CreateNormalMapTexture2dArray(
            List<Texture?> textures, List<bool> normalMapInvertYList, Size2 textureSize,
            ref TexImage? dummyTexImage, TextureTool texTool, ILogger logger)
        {
            if (textures.Count == 0 || textures.All(x => x is null))
            {
                return null;
            }

            int validTextureCount = 0;
            int rgba16Count = 0;
            int rgba8Count = 0;
            int bgra8Count = 0;
            const bool isSRgb = false;
            var texImages = new List<TexImage>(capacity: textures.Count);
            for (int i = 0; i < textures.Count; i++)
            {
                TexImage texImage;

                var tex = textures[i];
                if (tex is not null)
                {
                    validTextureCount++;
                    var texAssetItem = AssetFinder.FindAssetFromProxyObject(tex);
                    if (texAssetItem.Asset is not TextureAsset texAsset)
                    {
                        throw new Exception($"{Url} - TextureAsset not found at index {i}");
                    }
                    else if (texAsset.Source is null)
                    {
                        throw new Exception($"{Url} - TextureAsset file path not specified at index {i}");
                    }
                    texImage = texTool.Load(texAsset.Source, isSRgb);
                    texTool.Decompress(texImage, texImage.Format.IsSRgb());
                    switch (texImage.Format)
                    {
                        case PixelFormat.R16G16B16A16_UNorm:
                            rgba16Count++;
                            break;
                        case PixelFormat.R8G8B8A8_UNorm:
                            rgba8Count++;
                            break;
                        case PixelFormat.B8G8R8A8_UNorm:
                            bgra8Count++;
                            break;
                    }
                    if (texImage.Width != textureSize.Width || texImage.Height != textureSize.Height)
                    {
                        logger.Warning($"{Url} - Inconsistent normal map texture size at index {i}. Changing from ({texImage.Width},{texImage.Height}) to {textureSize}");
                        texTool.Resize(texImage, textureSize.Width, textureSize.Height, Filter.Rescaling.Nearest);
                    }
                    bool invertY = normalMapInvertYList[i];
                    if (invertY)
                    {
                        texTool.InvertY(texImage);
                    }
                }
                else
                {
                    texImage = null!;
                }

                texImages.Add(texImage);
            }

            // Convert texture format to all be the same
            var texArrayPixelFormat = PixelFormat.R8G8B8A8_UNorm;
            if (rgba16Count > 0)
            {
                texArrayPixelFormat = PixelFormat.R16G16B16A16_UNorm;
            }
            else if (bgra8Count > rgba8Count)
            {
                texArrayPixelFormat = PixelFormat.B8G8R8A8_UNorm;
            }
            for (int i = 0; i < texImages.Count; i++)
            {
                var texImage = texImages[i];
                if (texImage is null)
                {
                    var defaultNormalMapValue = new Color(128, 128, 255);
                    dummyTexImage ??= CreateDummyTexture(textureSize, texArrayPixelFormat, defaultNormalMapValue, texTool);
                    texImages[i] = dummyTexImage;
                }
                else if (texImage.Format != texArrayPixelFormat)
                {
                    logger.Warning($"{Url} - Inconsistent normal map texture pixel format at index {i}. Changing from {texImage.Format} to {texArrayPixelFormat}");
                    texTool.Convert(texImage, texArrayPixelFormat);
                }
            }

            var texImageArray = texTool.CreateTextureArray(texImages);
            return texImageArray;
        }

        private TexImage? CreateGreyscaleTexture2dArray(
            List<Texture?> textures, Size2 textureSize,
            ref TexImage? dummyTexImage, TextureTool texTool, ILogger logger)
        {
            if (textures.Count == 0 || textures.All(x => x is null))
            {
                return null;
            }

            var texArrayPixelFormat = PixelFormat.R8_UNorm;
            const bool isSRgb = false;
            var texImages = new List<TexImage>(capacity: textures.Count);
            for (int i = 0; i < textures.Count; i++)
            {
                TexImage texImage;

                var tex = textures[i];
                if (tex is not null)
                {
                    var texAssetItem = AssetFinder.FindAssetFromProxyObject(tex);
                    if (texAssetItem.Asset is not TextureAsset texAsset)
                    {
                        throw new Exception($"{Url} - TextureAsset not found at index {i}");
                    }
                    else if (texAsset.Source is null)
                    {
                        throw new Exception($"{Url} - TextureAsset file path not specified at index {i}");
                    }
                    texImage = texTool.Load(texAsset.Source, isSRgb);
                    texTool.Decompress(texImage, texImage.Format.IsSRgb());
                    if (texImage.Format != texArrayPixelFormat)
                    {
                        logger.Warning($"{Url} - Inconsistent greyscale texture pixel format at index {i}. Changing from {texImage.Format} to {texArrayPixelFormat}");
                        texTool.Convert(texImage, texArrayPixelFormat);
                    }
                    if (texImage.Width != textureSize.Width || texImage.Height != textureSize.Height)
                    {
                        logger.Warning($"{Url} - Inconsistent greyscale texture size at index {i}. Changing from ({texImage.Width},{texImage.Height}) to {textureSize}");
                        texTool.Resize(texImage, textureSize.Width, textureSize.Height, Filter.Rescaling.Nearest);
                    }
                }
                else
                {
                    dummyTexImage ??= CreateDummyTexture(textureSize, texArrayPixelFormat, defaultValue: 0, texTool);
                    texImage = dummyTexImage;
                }

                texImages.Add(texImage);
            }

            var texImageArray = texTool.CreateTextureArray(texImages);
            return texImageArray;
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
