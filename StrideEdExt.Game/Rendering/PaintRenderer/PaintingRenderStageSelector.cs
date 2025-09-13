using Stride.Engine;
using Stride.Rendering;
using StrideEdExt.Painting;
using System.ComponentModel;

namespace StrideEdExt.Rendering.PaintRenderer;

public class PaintingRenderStageSelector : RenderStageSelector
{
    [DefaultValue(RenderGroupMask.All)]
    public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;

    [DefaultValue(null)]
    public RenderStage? RenderStage { get; set; }

    public string? EffectName { get; set; } = "PaintingPickOutputEffect";

    internal Game Game = default!;

    private PaintBrushProcessor? _paintBrushProcessor;
    public override void Process(RenderObject renderObject)
    {
        if (((RenderGroupMask)(1U << (int)renderObject.RenderGroup) & RenderGroup) == 0)
        {
            return;
        }
        //var renderMesh = (RenderMesh)renderObject;
        // TODO ignore renderMesh.MaterialPass.HasTransparency?
        var renderStage = RenderStage;
        if (renderStage is null)
        {
            return;
        }

        if (_paintBrushProcessor is null)
        {
            var processors = Game.SceneSystem?.SceneInstance?.Processors;
            if (processors is null)
            {
                return;
            }
            _paintBrushProcessor = processors.FirstOrDefault(x => x is PaintBrushProcessor) as PaintBrushProcessor;
            if (_paintBrushProcessor is null)
            {
                return;
            }
        }

        if (renderObject is not RenderMesh renderMesh)
        {
            return;
        }

        if (renderMesh.Source is not ModelComponent modelComponent)
        {
            return;
        }

        var key = new PaintTargetEntityMesh
        {
            EntityId = modelComponent.Entity.Id,
            Mesh = renderMesh.Mesh
        };
        bool isVisible = _paintBrushProcessor.IsValidTargetEntityMesh(key);
        if (isVisible)
        {
            renderObject.ActiveRenderStages[renderStage.Index] = new ActiveRenderStage(EffectName);
        }
    }
}
