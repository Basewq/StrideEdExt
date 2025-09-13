using Stride.Core.Mathematics;
using Stride.Graphics;
using StrideEdExt.Painting;
using StrideEdExt.Painting.Brushes;

namespace StrideEdExt.Rendering;

public interface IPaintRendererService
{
    void EnqueueBrushRender(BrushRenderArgs brushRenderArgs);

    /// <summary>
    /// Returns the cursor hit result against the the last rendered frame.
    /// </summary>
    PaintingCursorHitResultData GetCursorHitResult(Vector2 screenPositionNormalized);
}

public enum BrushModeType : uint
{
    CircularBrush,
    TextureBrush,
}

public readonly struct BrushRenderArgs
{
    public required BrushModeType BrushModeType { get; init; }
    public required float BrushRadius { get; init; }
    public required float BrushOpacity { get; init; }
    public required BrushFalloffType BrushFalloffType { get; init; }
    public required float FalloffStartPercentage { get; init; }
    public required Texture? BrushTexture { get; init; }
    public required List<BrushPoint> BrushPoints { get; init; }
    public required Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> TargetEntityMeshToStrokeMapTextureMap { get; init; }

    public required Action<Dictionary<PaintTargetEntityMesh, Texture>>? RenderCompletedCallback { get; init; }
}

public struct BrushPoint
{
    public Vector3 WorldPosition;
    public Vector3 WorldNormal;
    public float Strength;
}
