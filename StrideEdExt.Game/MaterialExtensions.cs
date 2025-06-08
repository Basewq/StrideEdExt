using Stride.Rendering;

namespace SceneEditorExtensionExample;

public static class MaterialExtensions
{
    public static Material CloneMaterial(this Material sourceMaterial)
    {
        var destMaterial = new Material();
        foreach (var pass in sourceMaterial.Passes)
        {
            var newPass = new MaterialPass()
            {
                CullMode = pass.CullMode,
                BlendState = pass.BlendState,
                TessellationMethod = pass.TessellationMethod,
                HasTransparency = pass.HasTransparency,
                AlphaToCoverage = pass.AlphaToCoverage,
                IsLightDependent = pass.IsLightDependent,
                PassIndex = pass.PassIndex,
                Parameters = new ParameterCollection(pass.Parameters)
            };
            destMaterial.Passes.Add(newPass);
        }
        return destMaterial;
    }
}
