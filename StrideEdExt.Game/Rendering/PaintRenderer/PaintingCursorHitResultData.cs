using Stride.Core.Mathematics;
using StrideEdExt.Painting;

namespace StrideEdExt.Rendering;

public struct PaintingCursorHitResultData
{
    public PaintSessionId PaintSessionId;
    public bool IsHit;
    public Vector3 WorldPosition;
    public Vector3 WorldNormal;
}
