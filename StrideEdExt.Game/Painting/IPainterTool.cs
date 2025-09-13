using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Rendering;

namespace StrideEdExt.Painting;

public readonly record struct PaintTargetEntityMesh
{
    public required Guid EntityId { get; init; }
    public required Mesh Mesh { get; init; }
}

public record PaintRenderTargetTexture
{
    public required Texture Texture { get; init; }
    public required bool IsNewTexture { get; set; }
}

public interface IPainterTool
{
    bool IsInitialized { get; }
    PaintBrushSettings BrushSettings { get; }

    void Initialize(IServiceRegistry services);
    void Deinitialize();

    void Activate();
    void Deactivate();

    void GetAndPrepareTargetEntityMeshAndPaintRenderTargetMap(Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> targetEntityMeshAndRenderTargetMapOutput);

    void SetCursorPreviewModel(ModelComponent modelComponent);
    void UpdateCursorPreviewModel(ModelComponent modelComponent);

    void PaintStarted(BrushPoint strokeMapBrushPoint);
    void PaintCompleted(List<BrushPoint> strokeMapBrushPoints);
}

public abstract class PainterToolBase : IPainterTool
{
    protected IServiceRegistry Services { get; private set; } = default!;
    protected IPainterService PainterService { get; private set; } = default!;
    protected IGraphicsDeviceService GraphicsDeviceService { get; private set; } = default!;

    public bool IsInitialized { get; internal set; }

    public abstract PaintBrushSettings BrushSettings { get; }

    public void Initialize(IServiceRegistry services)
    {
        Services = services;
        PainterService = services.GetSafeServiceAs<IPainterService>();
        GraphicsDeviceService = services.GetSafeServiceAs<IGraphicsDeviceService>();

        OnInitialize();
        IsInitialized = true;
    }
    protected virtual void OnInitialize() { }

    public void Deinitialize()
    {
        OnDeinitialize();
    }
    protected virtual void OnDeinitialize() { }

    public void Activate()
    {
        OnActivate();
    }
    protected virtual void OnActivate() { }

    public void Deactivate()
    {
        OnDeactivate();
    }
    protected virtual void OnDeactivate() { }

    public abstract void GetAndPrepareTargetEntityMeshAndPaintRenderTargetMap(Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> targetEntityMeshAndRenderTargetMapOutput);

    public virtual void SetCursorPreviewModel(ModelComponent modelComponent)
    {
        var cursorModel = BrushSettings.CreateCursorPreviewModel(Services);
        modelComponent.Model = cursorModel;
        modelComponent.IsShadowCaster = false;
        if (cursorModel.Materials.Count > 0)
        {
            var material = cursorModel.Materials[0].Material;
            modelComponent.Materials.Add(key: 0, material);
        }
        BrushSettings.UpdateCursorPreviewModel(modelComponent);
    }

    public void UpdateCursorPreviewModel(ModelComponent modelComponent)
    {
        BrushSettings.UpdateCursorPreviewModel(modelComponent);
    }

    public void PaintStarted(BrushPoint strokeMapBrushPoint)
    {
        OnPaintStarted(strokeMapBrushPoint);
    }
    protected virtual void OnPaintStarted(BrushPoint strokeMapBrushPoint) { }

    public void PaintCompleted(List<BrushPoint> strokeMapBrushPoints)
    {
        OnPaintCompleted(strokeMapBrushPoints);
    }
    protected abstract void OnPaintCompleted(List<BrushPoint> strokeMapBrushPoints);

}
