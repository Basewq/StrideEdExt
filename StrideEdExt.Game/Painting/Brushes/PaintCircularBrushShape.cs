using Stride.Core;
using Stride.Core.Annotations;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.Materials;
using Stride.Rendering.ProceduralModels;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.Materials;
using Stride.Engine;

namespace StrideEdExt.Painting.Brushes;

public class PaintCircularBrushShape : PaintBrushShapeBase
{
    public override BrushModeType BrushMode => BrushModeType.CircularBrush;

    public BrushFalloffType FalloffType { get; set; } = BrushFalloffType.SmoothStep;

    [Display("Brush Falloff Start %")]
    [DataMemberRange(minimum: 0.0, maximum: 100.0, smallStep: 0.1, largeStep: 1.0, decimalPlaces: 2)]
    public float FalloffStartPercentage { get; set; } = 75;

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
                    MixinReference = "PaintingDefaultBrushShader"
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

            float falloffStartPercentage = FalloffType == BrushFalloffType.NotSet ? 100 : FalloffStartPercentage;
            materialParams.Set(PaintingDefaultBrushShaderKeys.FalloffStartPercentage, falloffStartPercentage);
        }
    }
}
