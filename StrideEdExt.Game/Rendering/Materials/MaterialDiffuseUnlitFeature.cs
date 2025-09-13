using Stride.Core;
using Stride.Rendering.Materials;
using Stride.Shaders;

namespace StrideEdExt.Rendering.Materials;

[DataContract]
[Display("Unlit")]
public class MaterialDiffuseUnlitFeature : MaterialFeature, IMaterialDiffuseModelFeature, IEnergyConservativeDiffuseModelFeature
{
    [DataMemberIgnore]
    bool IEnergyConservativeDiffuseModelFeature.IsEnergyConservative { get; set; }

    private bool IsEnergyConservative => ((IEnergyConservativeDiffuseModelFeature)this).IsEnergyConservative;

    public override void GenerateShader(MaterialGeneratorContext context)
    {
        var shaderBuilder = context.AddShading(this);
        shaderBuilder.LightDependentSurface = new ShaderClassSource("MaterialSurfaceShadingDiffuseUnlit", IsEnergyConservative);
    }

    public bool Equals(MaterialDiffuseUnlitFeature? other)
    {
        return IsEnergyConservative == other?.IsEnergyConservative;
    }

    public bool Equals(IMaterialShadingModelFeature? other) => Equals(other as MaterialDiffuseUnlitFeature);

    public override bool Equals(object? obj) => Equals(obj as MaterialDiffuseUnlitFeature);

    public override int GetHashCode()
    {
        var hashCode = IsEnergyConservative.GetHashCode();
        return hashCode;
    }
}
