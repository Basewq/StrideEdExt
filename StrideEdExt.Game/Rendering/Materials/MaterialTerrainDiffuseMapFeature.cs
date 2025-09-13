using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Shaders;

namespace StrideEdExt.Rendering.Materials;

[DataContract]
[Display("Terrain Diffuse Map")]
public class MaterialTerrainDiffuseMapFeature : MaterialFeature, IMaterialDiffuseFeature, IMaterialStreamProvider
{
    // Taken from Stride.Rendering.Materials.MaterialNormalMapFeature
    //private static readonly MaterialStreamDescriptor NormalStream = new MaterialStreamDescriptor("Normal (tangent)", "matNormal", MaterialKeys.NormalValue.PropertyType, remapSigned: true);
    //private static readonly MaterialStreamDescriptor NormalStreamWorld = new MaterialStreamDescriptor("Normal (world)", "NormalStream.normalWS", new ShaderClassSource("MaterialSurfaceNormalStreamShading"));

    public Texture? MaterialIndexMap { get; set; }
    public Vector2 MaterialIndexMapSize { get; set; }
    public Texture? DiffuseMapTextureArray { get; set; }
    public Texture? NormalMapTextureArray { get; set; }
    public Texture? HeightBlendMapTextureArray { get; set; }

    public bool IsEditable { get; set; }

    public override void GenerateShader(MaterialGeneratorContext context)
    {
        context.Parameters.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMap, MaterialIndexMap);
        context.Parameters.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMapSize, MaterialIndexMapSize);
        context.Parameters.Set(MaterialTerrainDiffuseMapKeys.DiffuseMap, DiffuseMapTextureArray);
        context.Parameters.Set(MaterialTerrainDiffuseMapKeys.NormalMap, NormalMapTextureArray);
        context.Parameters.Set(MaterialTerrainDiffuseMapKeys.HeightBlendMap, HeightBlendMapTextureArray);

        context.Parameters.Set(MaterialKeys.HasNormalMap, true);

        var mixin = new ShaderMixinSource();
        string shaderName = IsEditable ? "MaterialTerrainEditPreviewDiffuseMap" : "MaterialTerrainDiffuseMap";
        mixin.Mixins.Add(new ShaderClassSource(shaderName));

        context.UseStreamWithCustomBlend(MaterialShaderStage.Pixel, "matNormal", new ShaderClassSource("MaterialStreamNormalBlend"));
        //context.UseStream(MaterialShaderStage.Pixel, NormalStream.Stream);

        context.UseStream(MaterialShaderStage.Pixel, MaterialDiffuseMapFeature.DiffuseStream.Stream);
        context.UseStream(MaterialShaderStage.Pixel, MaterialDiffuseMapFeature.ColorBaseStream.Stream);
        context.AddShaderSource(MaterialShaderStage.Pixel, mixin);
    }

    public IEnumerable<MaterialStreamDescriptor> GetStreams()
    {
        //yield return NormalStream;
        //yield return NormalStreamWorld;
        yield return MaterialDiffuseMapFeature.ColorBaseStream;
        yield return MaterialDiffuseMapFeature.DiffuseStream;
    }
}
