using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.Materials;
using Stride.Rendering.ProceduralModels;
using Stride.Rendering;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.Materials;

namespace StrideEdExt.Painting.Brushes;

public class PaintTextureBrushShape : PaintBrushShapeBase
{
    public override BrushModeType BrushMode => BrushModeType.TextureBrush;

    public Texture? Texture { get; set; }

    public override Model CreateCursorPreviewModel(IServiceRegistry serviceRegistry)
    {
        var graphicsDeviceService = serviceRegistry.GetSafeServiceAs<IGraphicsDeviceService>();
        var graphicsDevice = graphicsDeviceService.GraphicsDevice;

        var cursorModel = new Model();
        var procPlaneModel = new PlaneProceduralModel();
        var materialDescription = new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeShaderClassColor()
                {
                    MixinReference = "PaintingTextureBrushShader"
                }),
                DiffuseModel = new MaterialDiffuseUnlitFeature(),
                Transparency = new MaterialTransparencyBlendFeature(),
                CullMode = CullMode.None,
                DepthFunction = CompareFunction.Always,
            }
        };
        var material = Material.New(graphicsDevice, materialDescription);
        procPlaneModel.MaterialInstance.Material = material;
        procPlaneModel.MaterialInstance.IsShadowCaster = false;
        procPlaneModel.Generate(serviceRegistry, cursorModel);

        return cursorModel;
    }

    public override void UpdateCursorPreviewModel(ModelComponent modelComponent)
    {
        if (modelComponent.Materials.TryGetValue(0, out var material))
        {
            var materialParams = material.Passes[0].Parameters;

            materialParams.Set(PaintingTextureBrushShaderKeys.BrushTexture, Texture);
        }
    }
}
