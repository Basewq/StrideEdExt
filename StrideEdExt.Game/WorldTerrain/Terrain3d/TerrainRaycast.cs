using Stride.Core.Mathematics;
using StrideEdExt.Rasterization;
using StrideEdExt.Rendering.Meshes;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d;

namespace StrideEdExt.WorldTerrain.Terrain3d;

public static class TerrainRaycast
{
    public static bool TryRaycast(TerrainMap terrainMap, Ray ray, Vector3 mapStartPosition, out RaycastHitResult hitResult)
    {
        var heightmapData = terrainMap.HeightmapData;
        var heightRange = terrainMap.HeightRange;
        var meshQuadSize = terrainMap.MeshQuadSize;
        bool raycastResult = TryRaycast(heightmapData, heightRange, meshQuadSize, ray, mapStartPosition, out hitResult);
        return raycastResult;
    }

    public static bool TryRaycast(
        Array2d<float>? heightmapData, Vector2 heightRange, Vector2 meshQuadSize,
        Ray ray, Vector3 mapStartPosition,
        out RaycastHitResult hitResult)
    {
        hitResult = default;
        if (heightmapData is null)
        {
            return false;
        }

        var mapWorldSize = meshQuadSize * heightmapData.Length2d.ToVector2();

        var mapMinPos = new Vector3(0, heightRange.X, 0);
        var mapMaxPos = new Vector3(mapWorldSize.X, heightRange.Y, mapWorldSize.Y);
        var mapBoundingBox = new BoundingBox(mapMinPos, mapMaxPos);

        float scaleX = meshQuadSize.X;
        float scaleZ = meshQuadSize.Y;

        var localRayOrigin = ray.Position - mapStartPosition;
        localRayOrigin.X /= scaleX;
        localRayOrigin.Z /= scaleZ;

        var localRayDir = ray.Direction;
        localRayDir.X /= scaleX;
        localRayDir.Z /= scaleZ;

        var localRay = new Ray(localRayOrigin, localRayDir);     // Move the ray to be relative to the heightmap space
        // Find the line that is contained in the terrain's bounds
        if (!GetIntersection(localRay, mapBoundingBox, out float tMinOutput, out float tMaxOutput))
        {
            return false;
        }

        var localPosFrom = localRay.Position + (localRay.Direction * tMinOutput);
        var localPosTo = localRay.Position + (localRay.Direction * tMaxOutput);

        var visitor = new Grid2dLineScannerVisitor(heightmapData, heightRange, localRay);
        Grid2dLineScanner.ScanLine(localPosFrom.XZ(), localPosTo.XZ(), ref visitor);

        if (visitor.HasHit)
        {
            hitResult = visitor.HitResult;
            // Move hit result back to world space
            hitResult.HitPosition.X *= scaleX;
            hitResult.HitPosition.Z *= scaleZ;
            hitResult.HitPosition += mapStartPosition;
        }
        return visitor.HasHit;
    }

    private static bool GetIntersection(Ray ray, BoundingBox box, out float tMinOutput, out float tMaxOutput)
    {
        // Adapted from https://tavianator.com/2022/ray_box_boundary.html
        // Alternative link: https://github.com/tavianator/ray_box/blob/main/ray_box.c
        float tMin = 0.0f;
        float tMax = float.PositiveInfinity;

        var rayOrigin = ray.Position;
        var rayDirInv = 1f / ray.Direction;
        var boxMin = box.Minimum;
        var boxMax = box.Maximum;

        for (int d = 0; d < 3; ++d)
        {
            float t1 = (boxMin[d] - rayOrigin[d]) * rayDirInv[d];
            float t2 = (boxMax[d] - rayOrigin[d]) * rayDirInv[d];

            tMin = Math.Max(tMin, Math.Min(t1, t2));
            tMax = Math.Min(tMax, Math.Max(t1, t2));
        }

        tMinOutput = tMin;
        tMaxOutput = tMax;

        return tMin <= tMax;    // Includes just touching the box
    }

    public struct RaycastHitResult
    {
        public Int2 HeightmapCellIndex;
        public Vector3 HitPosition;
    }

    private struct Grid2dLineScannerVisitor : IGrid2dCellTraversalVisitor
    {
        private readonly Array2d<float> _heightmap;
        private readonly Vector2 _heightRange;
        private readonly Ray _ray;

        public bool HasHit;
        public RaycastHitResult HitResult;

        public Grid2dLineScannerVisitor(Array2d<float> heightmap, Vector2 heightRange, Ray ray)
            : this()
        {
            _heightmap = heightmap;
            _heightRange = heightRange;
            _ray = ray;
        }

        public bool Visit(int x, int y)
        {
            // End bounds -1 because we need to form a quad
            if (x < 0 || x >= _heightmap.LengthX - 1
                || y < 0 || y >= _heightmap.LengthY - 1)
            {
                //System.Diagnostics.Debug.WriteLine($"Line scanner out of bounds: {x}, {y}");
                return true;
            }

            var heightmapStartIndex = new Int2(x, y);
            var quad = CreateQuadAtLocation(heightmapStartIndex, _heightmap, _heightRange);
            if (quad.Triangle0.TryRaycast(_ray, out var rayTriPoint))
            {
                HitResult = new RaycastHitResult
                {
                    HeightmapCellIndex = new(x, y),
                    HitPosition = rayTriPoint
                };
                HasHit = true;
                return false;
            }
            if (quad.Triangle1.TryRaycast(_ray, out rayTriPoint))
            {
                HitResult = new RaycastHitResult
                {
                    HeightmapCellIndex = new(x, y),
                    HitPosition = rayTriPoint
                };
                HasHit = true;
                return false;
            }

            // No hit, continue scanning
            return true;
        }

        private static MeshQuadData CreateQuadAtLocation(Int2 heightmapStartIndex, Array2d<float> normalizedHeightmapData, Vector2 heightRange)
        {
            var IndexOffsetRight = new Int2(1, 0);
            var IndexOffsetDown = new Int2(0, 1);

            var indexTL = heightmapStartIndex;
            var indexTR = heightmapStartIndex + IndexOffsetRight;
            var indexBL = heightmapStartIndex + IndexOffsetDown;
            var indexBR = heightmapStartIndex + IndexOffsetDown + IndexOffsetRight;

            var vertPosTL = CreatePoint(indexTL, normalizedHeightmapData, heightRange);
            var vertPosTR = CreatePoint(indexTR, normalizedHeightmapData, heightRange);
            var vertPosBL = CreatePoint(indexBL, normalizedHeightmapData, heightRange);
            var vertPosBR = CreatePoint(indexBR, normalizedHeightmapData, heightRange);

            var localIndexTL = indexTL;
            var localIndexTR = indexTR;
            var localIndexBL = indexBL;
            var localIndexBR = indexBR;
            var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
            return quad;

            static Vector3 CreatePoint(Int2 heightmapIndex, Array2d<float> normalizedHeightmapData, Vector2 heightRange)
            {
                var (minHeightPosition, maxHeightPosition) = heightRange;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndex];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);
                var pos = new Vector3(heightmapIndex.X, height, heightmapIndex.Y);
                return pos;
            }
        }
    }
}
