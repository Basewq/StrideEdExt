using Stride.Core.Mathematics;
using System;

namespace StrideEdExt.Rendering.Meshes;

public record struct MeshQuadData
{
    public MeshQuadTriangleData Triangle0;
    public MeshQuadTriangleData Triangle1;

    public static MeshQuadData CreateQuad(
        in Vector3 posTL, in Vector3 posTR, in Vector3 posBL, in Vector3 posBR,
        Int2 localIndexTL, Int2 localIndexTR, Int2 localIndexBL, Int2 localIndexBR)
    {
        MeshQuadData quadData = new();

        float height0 = posTL.Y;
        float height1 = posTR.Y;
        float height2 = posBL.Y;
        float height3 = posBR.Y;

        float diff_TL_BR = MathF.Abs(height0 - height3);
        float diff_TR_BL = MathF.Abs(height1 - height2);
        bool isQuadSplit0 = diff_TL_BR < diff_TR_BL;
        // The quad should be split where the diagonal edge has the least height difference
        if (isQuadSplit0)
        {
            /* Quad to Triangle (clockwise winding for DirectX):
             * 0---1
             * | \ |
             * 2---3
             */
            quadData.Triangle0 = new MeshQuadTriangleData
            {
                Position0 = posTL,
                Position1 = posTR,
                Position2 = posBR,

                LocalIndex0 = localIndexTL,
                LocalIndex1 = localIndexTR,
                LocalIndex2 = localIndexBR,
            };
            quadData.Triangle1 = new MeshQuadTriangleData
            {
                Position0 = posTL,
                Position1 = posBR,
                Position2 = posBL,

                LocalIndex0 = localIndexTL,
                LocalIndex1 = localIndexBR,
                LocalIndex2 = localIndexBL,
            };
        }
        else
        {
            /* Quad to Triangle (clockwise winding for DirectX):
             * 0---1
             * | / |
             * 2---3
             */
            quadData.Triangle0 = new MeshQuadTriangleData
            {
                Position0 = posTL,
                Position1 = posTR,
                Position2 = posBL,

                LocalIndex0 = localIndexTL,
                LocalIndex1 = localIndexTR,
                LocalIndex2 = localIndexBL,
            };
            quadData.Triangle1 = new MeshQuadTriangleData
            {
                Position0 = posTR,
                Position1 = posBR,
                Position2 = posBL,

                LocalIndex0 = localIndexTR,
                LocalIndex1 = localIndexBR,
                LocalIndex2 = localIndexBL,
            };
        }
        return quadData;
    }
}

public record struct MeshQuadTriangleData
{
    /// <summary>
    /// Triangle Vertex is in clockwise winding:
    /// <code>
    ///  0---1
    ///  | /
    ///  2
    /// </code>
    /// </summary>
    public required Vector3 Position0;
    /// <inheritdoc cref="Position0"/>
    public required Vector3 Position1;
    /// <inheritdoc cref="Position0"/>
    public required Vector3 Position2;

    public required Int2 LocalIndex0;
    public required Int2 LocalIndex1;
    public required Int2 LocalIndex2;

    public readonly Vector3 MidPoint => (Position0 + Position1 + Position2) / 3f;

    public readonly void GetNormalAndTangent(out Vector3 normalVec, out Vector3 tangentVec)
    {
        GetNormalAndTangent(in Position0, in Position1, in Position2, out normalVec, out tangentVec);
    }

    public static void GetNormalAndTangent(
        in Vector3 vertPosition0, in Vector3 vertPosition1, in Vector3 vertPosition2,
        out Vector3 normalVec, out Vector3 tangentVec)
    {
        /* Triangle (clockwise winding for DirectX):
         * 0---1
         * | /
         * 2
         */

        Vector3 vec0ToVec2 = vertPosition0 - vertPosition2;
        Vector3 vec0ToVec1 = vertPosition0 - vertPosition1;
        normalVec = Vector3.Cross(vec0ToVec2, vec0ToVec1);      // Don't normalize until the end, larger triangles should influence the normals/tangents more than smaller triangles

        // Note due to how heightmap works, normalVec can never be exactly Vector3.UnitZ
        // so we don't need to handle this kind of edge case.
        tangentVec = Vector3.Cross(normalVec, Vector3.UnitZ);
    }

    public readonly bool TryRaycast(Ray ray, out Vector3 rayTriPoint)
    {
        if (CollisionHelper.RayIntersectsTriangle(in ray, in Position0, in Position1, in Position2, out float rayTriPointDistance))
        {
            rayTriPoint = ray.Position + (ray.Direction * rayTriPointDistance);
            return true;
        }

        rayTriPoint = default;
        return false;
    }
}
