using Stride.Core.Mathematics;
using Stride.Engine;

namespace StrideEdExt.Rendering;

internal static class CameraExtensions
{
    private static readonly Vector3[] FrustumPointsClipSpace = [
        // Far plane
        new Vector3(-1, +1, 1),
        new Vector3(+1, +1, 1),
        new Vector3(-1, -1, 1),
        new Vector3(+1, -1, 1),
        // Near plane
        new Vector3(-1, +1, 0),
        new Vector3(+1, +1, 0),
        new Vector3(-1, -1, 0),
        new Vector3(+1, -1, 0),
    ];

    public static void CreateBoundingShapesFromCamera(
        CameraComponent cameraComponent, float? maxInstancingRenderDistance,
        out BoundingFrustum boundingFrustum,
        out BoundingBox frustumBoundingBox)
    {
        // Assume we're always using Perspective camera
        float fovRadians = MathUtil.DegreesToRadians(cameraComponent.VerticalFieldOfView);
        float aspectRatio = cameraComponent.AspectRatio;
        float zNear = cameraComponent.NearClipPlane;
        float zFar = maxInstancingRenderDistance ?? cameraComponent.FarClipPlane;

        Matrix.PerspectiveFovRH(fovRadians, aspectRatio, zNear, zFar, out var projMatrix);

        Matrix.Multiply(in cameraComponent.ViewMatrix, in projMatrix, out var viewProjMatrix);
        boundingFrustum = new BoundingFrustum(in viewProjMatrix);

        // Determine the AABB bounds of the frustum based off the corners of the frustum
        Matrix.Invert(in viewProjMatrix, out var viewProjInverseMatrix);
        Span<Vector3> frustumPointsWorldSpace = stackalloc Vector3[FrustumPointsClipSpace.Length];
        for (int i = 0; i < FrustumPointsClipSpace.Length; i++)
        {
            var vec4 = Vector3.Transform(FrustumPointsClipSpace[i], viewProjInverseMatrix);
            frustumPointsWorldSpace[i] = vec4.XYZ() / vec4.W;
        }
        frustumBoundingBox = CreateBoundingBoxFromPoints(frustumPointsWorldSpace);
    }

    private static BoundingBox CreateBoundingBoxFromPoints(Span<Vector3> points)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (int i = 0; i < points.Length; ++i)
        {
            Vector3.Min(ref min, ref points[i], out min);
            Vector3.Max(ref max, ref points[i], out max);
        }

        return new BoundingBox(min, max);
    }
}
