using Stride.Core.Mathematics;
using StrideEdExt.Rendering.Meshes;
using StrideEdExt.SharedData;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StrideEdExt.WorldTerrain.TerrainMesh;

record struct TerrainMeshData
{
    public required TerrainVertex[] Vertices;
    public required ushort[] VertexIndices;
    public required int TriangleCount;

    public static TerrainMeshData Generate(Rectangle heightmapTextureRegion, Size2 heightmapTextureSize, Array2d<float> normalizedHeightmapData, Vector2 quadSize, Vector2 heightRange)
    {
        int vertexRowCount = heightmapTextureRegion.Width;
        int vertexColumnCount = heightmapTextureRegion.Height;
        int quadCountX = vertexRowCount - 1;
        int quadCountY = vertexColumnCount - 1;
        Debug.Assert(quadCountX <= byte.MaxValue && quadCountY <= byte.MaxValue, $"Quad length must be 255 or less to fit on ushort array.");
        const int TrianglesPerQuad = 2;
        const int VerticesPerTriangle = 3;

        int totalUniqueVertices = vertexRowCount * vertexColumnCount;
        int totalQuads = quadCountX * quadCountY;
        int totalVertexIndices = totalQuads * TrianglesPerQuad * VerticesPerTriangle;   // Mesh will be TriangleList

        var mesh = new TerrainMeshData
        {
            Vertices = new TerrainVertex[totalUniqueVertices],
            VertexIndices = new ushort[totalVertexIndices],
            TriangleCount = totalQuads * TrianglesPerQuad
        };

        // Build the vertices
        var (minHeightPosition, maxHeightPosition) = heightRange;
        var textureMapSizeVec2 = heightmapTextureSize.ToVector2();
        var uvStart = new Vector2(heightmapTextureRegion.X, heightmapTextureRegion.Y) / textureMapSizeVec2;
        for (int y = 0; y < vertexColumnCount; y++)
        {
            int heightmapIndexY = heightmapTextureRegion.Y + y;
            for (int x = 0; x < vertexRowCount; x++)
            {
                int vertIdx = MathExt.ToIndex1d(x, y, vertexRowCount);

                int heightmapIndexX = heightmapTextureRegion.X + x;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                mesh.Vertices[vertIdx] = new TerrainVertex
                {
                    Position = new Vector3(quadSize.X * x, height, quadSize.Y * y),
                    Color = Color.White,
                    TextureCoords0 = new Vector2(x / (float)quadCountX, y / (float)quadCountY),
                    TextureCoords1 = uvStart + (new Vector2(x, y) / textureMapSizeVec2),
                    // Normal/Tangent vectors are calculated in SetVertexNormals
                    Normal = Vector3.Zero,
                    Tangent = Vector3.Zero,
                };
            }
        }

        // Build the vertex indices
        int nextVertexIndex = 0;
        for (int y = 0; y < quadCountY; y++)
        {
            for (int x = 0; x < quadCountX; x++)
            {
                /* Quad points
                 * 0---1
                 * |   |
                 * 2---3
                 */
                int vertIdx0 = MathExt.ToIndex1d(x, y, vertexRowCount); // Top-Left
                int vertIdx1 = vertIdx0 + 1;                            // Top-Right
                int vertIdx2 = vertIdx0 + vertexRowCount;               // Bottom-Left
                int vertIdx3 = vertIdx2 + 1;                            // Bottom-Right

                float height0 = mesh.Vertices[vertIdx0].Position.Y;
                float height1 = mesh.Vertices[vertIdx1].Position.Y;
                float height2 = mesh.Vertices[vertIdx2].Position.Y;
                float height3 = mesh.Vertices[vertIdx3].Position.Y;

                var vertexIndices = mesh.VertexIndices;
                float diff_TL_BR = MathF.Abs(height0 - height3);
                float diff_TR_BL = MathF.Abs(height1 - height2);
                bool isQuadSplitForward = diff_TL_BR < diff_TR_BL;
                // The quad should be split where the diagonal edge has the least height difference
                if (isQuadSplitForward)
                {
                    /* Quad to Triangle (clockwise winding for DirectX):
                     * 0---1
                     * | \ |
                     * 2---3
                     */
                    // Upper triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    // Lower triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                }
                else
                {
                    /* Quad to Triangle (clockwise winding for DirectX):
                     * 0---1
                     * | / |
                     * 2---3
                     */
                    // Upper triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                    // Lower triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                }
            }
        }

        SetVertexNormalsAndTangents(normalizedHeightmapData, heightmapTextureRegion, quadSize, heightRange, mesh.Vertices, mesh.VertexIndices);

        //DebugPrintMeshData(mesh, quadCountX, quadCountY, vertexRowCount);

        return mesh;
    }

    [Conditional("DEBUG")]
    private static void DebugPrintMeshData(TerrainMeshData mesh, int quadCountX, int quadCountY, int vertexRowCount)
    {
        bool printDebug = true;
        if (printDebug)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Terrain Mesh: ({quadCountX}, {quadCountY})");
            for (int y = 0; y <= quadCountY; y++)
            {
                for (int x = 0; x <= quadCountX; x++)
                {
                    int vertIdx = MathExt.ToIndex1d(x, y, vertexRowCount);
                    if (x > 0)
                    {
                        sb.Append('\t');
                    }
                    PrintVert(sb, mesh.Vertices[vertIdx].Position);
                }
                sb.AppendLine();
            }
            Debug.WriteLine(sb.ToString());

            static void PrintVert(StringBuilder sb, Vector3 vec)
            {
                sb.Append($"{vec.X} {vec.Y} {vec.Z}");
            }
        }
    }

    public static bool GenerateMeshDataForPhysics(
        Rectangle heightmapTextureRegion, Array2d<float> normalizedHeightmapData, Vector2 quadSize, Vector2 heightRange,
        [NotNullWhen(true)] out Vector3[] vertexPositions, [NotNullWhen(true)] out int[] vertexIndices)
    {
        int vertexRowCount = heightmapTextureRegion.Width;
        int vertexColumnCount = heightmapTextureRegion.Height;
        int quadCountX = vertexRowCount - 1;
        int quadCountY = vertexColumnCount - 1;
        const int TrianglesPerQuad = 2;
        const int VerticesPerTriangle = 3;

        int totalUniqueVertices = vertexRowCount * vertexColumnCount;
        int totalQuads = quadCountX * quadCountY;
        int totalVertexIndices = totalQuads * TrianglesPerQuad * VerticesPerTriangle;   // Mesh will be TriangleList

        vertexPositions = new Vector3[totalUniqueVertices];
        vertexIndices = new int[totalVertexIndices];

        // Build the vertices
        var positionOffset = new Vector3(quadSize.X * heightmapTextureRegion.X, 0, quadSize.X * heightmapTextureRegion.Y);
        var (minHeightPosition, maxHeightPosition) = heightRange;
        for (int y = 0; y < vertexColumnCount; y++)
        {
            int heightmapIndexY = heightmapTextureRegion.Y + y;
            for (int x = 0; x < vertexRowCount; x++)
            {
                int vertIdx = MathExt.ToIndex1d(x, y, vertexRowCount);

                int heightmapIndexX = heightmapTextureRegion.X + x;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                vertexPositions[vertIdx] = new Vector3(quadSize.X * x, height, quadSize.Y * y) + positionOffset;
            }
        }

        // Build the vertex indices
        int nextVertexIndex = 0;
        for (int y = 0; y < quadCountY; y++)
        {
            for (int x = 0; x < quadCountX; x++)
            {
                /* Quad points
                 * 0---1
                 * |   |
                 * 2---3
                 */
                int vertIdx0 = MathExt.ToIndex1d(x, y, vertexRowCount); // Top-Left
                int vertIdx1 = vertIdx0 + 1;                            // Top-Right
                int vertIdx2 = vertIdx0 + vertexRowCount;               // Bottom-Left
                int vertIdx3 = vertIdx2 + 1;                            // Bottom-Right

                float height0 = vertexPositions[vertIdx0].Y;
                float height1 = vertexPositions[vertIdx1].Y;
                float height2 = vertexPositions[vertIdx2].Y;
                float height3 = vertexPositions[vertIdx3].Y;

                float diff_TL_BR = MathF.Abs(height0 - height3);
                float diff_TR_BL = MathF.Abs(height1 - height2);
                bool isQuadSplitForward = diff_TL_BR < diff_TR_BL;
                // The quad should be split where the diagonal edge has the least height difference
                if (isQuadSplitForward)
                {
                    /* Quad to Triangle (clockwise winding for DirectX):
                     * 0---1
                     * | \ |
                     * 2---3
                     */
                    // Upper triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    // Lower triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                }
                else
                {
                    /* Quad to Triangle (clockwise winding for DirectX):
                     * 0---1
                     * | / |
                     * 2---3
                     */
                    // Upper triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                    // Lower triangle
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                    vertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                }
            }
        }

        CheckVerticesBounds(vertexPositions, vertexIndices);

        return true;
    }

    [Conditional("DEBUG")]
    private static void CheckVerticesBounds(Vector3[] vertexPositions, int[] vertexIndices)
    {
        for (int i = 0; i < vertexIndices.Length; i++)
        {
            int idx = vertexIndices[i];
            Debug.Assert(idx < vertexPositions.Length);
        }
    }

    private static unsafe void SetVertexNormalsAndTangents(
        Array2d<float> normalizedHeightmapData, Rectangle heightmapTextureRegion, Vector2 quadSize, Vector2 heightRange,
        TerrainVertex[] vertices, ushort[] vertexIndices)
    {
        // Calculate normals adapted from https://github.com/simondarksidej/XNAGameStudio/wiki/Riemers3DXNA1Terrain13buffers
        // and Triangle mesh tangent space calculation (Martin Mittring) https://media.gdcvault.com/gdc07/slides/S3701i1.pdf

        // The main idea of the code is the following:
        // 1. For every triangle, calculate the normal & tangent vectors of that triangle.
        // 2. Add the calculated normal & tangent vectors directly to each vertices of that triangle.
        // 3. Once all triangles have been processed, normalize the normal & tangent vectors on each vertices.
        //
        // If you only have a single mesh, the first part and the last part of the code is sufficient.
        // However, if you are dividing the heightmap into smaller meshes (like we are), then after you've
        // calculated the normals/tangents within the region, you need to build the triangles that directly
        // touch the boundary of the region and add the normals/tangents where the vertices overlap.
        // Without this step, the mesh will not appear smooth when crossing over to the adjacent meshes.

        //Debug.WriteLine($"Build normals: {heightmapTextureRegion}");

        const int VerticesPerTriangle = 3;
        int totalTriangles = vertexIndices.Length / VerticesPerTriangle;
        for (int i = 0; i < totalTriangles; i++)
        {
            /* Triangle (clockwise winding for DirectX):
             * 0---1
             * | /
             * 2
             */
            int vertIndicesIndexStart = i * VerticesPerTriangle;
            int vertIndex0 = vertexIndices[vertIndicesIndexStart];
            int vertIndex1 = vertexIndices[vertIndicesIndexStart + 1];
            int vertIndex2 = vertexIndices[vertIndicesIndexStart + 2];

            ref var vertPosition0 = ref vertices[vertIndex0].Position;
            ref var vertPosition1 = ref vertices[vertIndex1].Position;
            ref var vertPosition2 = ref vertices[vertIndex2].Position;

            MeshQuadTriangleData.GetNormalAndTangent(vertPosition0, vertPosition1, vertPosition2, out var normalVec, out var tangentVec);
            vertices[vertIndex0].Normal += normalVec;
            vertices[vertIndex1].Normal += normalVec;
            vertices[vertIndex2].Normal += normalVec;

            vertices[vertIndex0].Tangent += tangentVec;
            vertices[vertIndex1].Tangent += tangentVec;
            vertices[vertIndex2].Tangent += tangentVec;
        }

        #region Normal/Tangent from neighboring chunks
        // Edge case: due to chunking we also need the normal/tangents from the neighboring triangles
        // that are outside of this chunk to ensure the terrain is seamless at the boundaries.
        var IndexOffsetRight = new Int2(1, 0);
        var IndexOffsetDown = new Int2(0, 1);
        var (minHeightPosition, maxHeightPosition) = heightRange;
        var regionSize = heightmapTextureRegion.Size;
        // Top edge
        if (heightmapTextureRegion.Y > 0)
        {
            int vertexRowCount = heightmapTextureRegion.Width;
            int quadCountX = vertexRowCount - 1;

            var arrayPool = ArrayPool<Vector3>.Shared;
            var edgeVertPosArray = arrayPool.Rent(minimumLength: vertexRowCount);

            int edgeLocalY = -1;
            int heightmapIndexY = heightmapTextureRegion.Y - 1;
            for (int localX = 0; localX < vertexRowCount; localX++)
            {
                int heightmapIndexX = heightmapTextureRegion.X + localX;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                edgeVertPosArray[localX] = new Vector3(quadSize.X * localX, height, quadSize.Y * edgeLocalY);
            }

            int localY = edgeLocalY + 1;
            for (int localX = 0; localX < quadCountX; localX++)
            {
                ref var vertPosTL = ref edgeVertPosArray[localX];
                ref var vertPosTR = ref edgeVertPosArray[localX + 1];
                int vertIndex = MathExt.ToIndex1d(localX, localY, vertexRowCount);
                int vertIndexRight = MathExt.ToIndex1d(localX + 1, localY, vertexRowCount);
                ref var vertPosBL = ref vertices[vertIndex].Position;
                ref var vertPosBR = ref vertices[vertIndexRight].Position;

                var localIndexTL = new Int2(localX, Math.Min(localY, edgeLocalY));
                var localIndexTR = localIndexTL + IndexOffsetRight;
                var localIndexBL = localIndexTL + IndexOffsetDown;
                var localIndexBR = localIndexTL + IndexOffsetDown + IndexOffsetRight;

                var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
                AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
            }

            arrayPool.Return(edgeVertPosArray);
        }
        // Bottom edge
        if (heightmapTextureRegion.Bottom < normalizedHeightmapData.LengthY - 1)
        {
            int vertexRowCount = heightmapTextureRegion.Width;
            int quadCountX = vertexRowCount - 1;

            var arrayPool = ArrayPool<Vector3>.Shared;
            var edgeVertPosArray = arrayPool.Rent(minimumLength: vertexRowCount);

            int edgeLocalY = heightmapTextureRegion.Height;
            int heightmapIndexY = heightmapTextureRegion.Bottom;
            for (int localX = 0; localX < vertexRowCount; localX++)
            {
                int heightmapIndexX = heightmapTextureRegion.X + localX;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                edgeVertPosArray[localX] = new Vector3(quadSize.X * localX, height, quadSize.Y * edgeLocalY);
            }

            int localY = edgeLocalY - 1;
            for (int localX = 0; localX < quadCountX; localX++)
            {
                int vertIndex = MathExt.ToIndex1d(localX, localY, vertexRowCount);
                int vertIndexRight = MathExt.ToIndex1d(localX + 1, localY, vertexRowCount);
                ref var vertPosTL = ref vertices[vertIndex].Position;
                ref var vertPosTR = ref vertices[vertIndexRight].Position;

                ref var vertPosBL = ref edgeVertPosArray[localX];
                ref var vertPosBR = ref edgeVertPosArray[localX + 1];

                var localIndexTL = new Int2(localX, Math.Min(localY, edgeLocalY));
                var localIndexTR = localIndexTL + IndexOffsetRight;
                var localIndexBL = localIndexTL + IndexOffsetDown;
                var localIndexBR = localIndexTL + IndexOffsetDown + IndexOffsetRight;

                var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
                AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
            }

            arrayPool.Return(edgeVertPosArray);
        }
        // Left edge
        if (heightmapTextureRegion.X > 0)
        {
            int vertexRowCount = heightmapTextureRegion.Width;
            int vertexColumnCount = heightmapTextureRegion.Height;
            int quadCountX = vertexRowCount - 1;
            int quadCountY = vertexColumnCount - 1;

            var arrayPool = ArrayPool<Vector3>.Shared;
            var edgeVertPosArray = arrayPool.Rent(minimumLength: vertexColumnCount);

            int edgeLocalX = -1;
            int heightmapIndexX = heightmapTextureRegion.X - 1;
            for (int localY = 0; localY < vertexColumnCount; localY++)
            {
                int heightmapIndexY = heightmapTextureRegion.Y + localY;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                edgeVertPosArray[localY] = new Vector3(quadSize.X * edgeLocalX, height, quadSize.Y * localY);
            }

            int localX = edgeLocalX + 1;
            for (int localY = 0; localY < quadCountY; localY++)
            {
                ref var vertPosTL = ref edgeVertPosArray[localY];
                ref var vertPosBL = ref edgeVertPosArray[localY + 1];
                int vertIndex = MathExt.ToIndex1d(localX, localY, vertexRowCount);
                int vertIndexDown = MathExt.ToIndex1d(localX, localY + 1, vertexRowCount);
                ref var vertPosTR = ref vertices[vertIndex].Position;
                ref var vertPosBR = ref vertices[vertIndexDown].Position;

                var localIndexTL = new Int2(Math.Min(localX, edgeLocalX), localY);
                var localIndexTR = localIndexTL + IndexOffsetRight;
                var localIndexBL = localIndexTL + IndexOffsetDown;
                var localIndexBR = localIndexTL + IndexOffsetDown + IndexOffsetRight;

                var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
                AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
            }

            arrayPool.Return(edgeVertPosArray);
        }
        // Right edge
        if (heightmapTextureRegion.Right < normalizedHeightmapData.LengthX - 1)
        {
            int vertexRowCount = heightmapTextureRegion.Width;
            int vertexColumnCount = heightmapTextureRegion.Height;
            int quadCountX = vertexRowCount - 1;
            int quadCountY = vertexColumnCount - 1;

            var arrayPool = ArrayPool<Vector3>.Shared;
            var edgeVertPosArray = arrayPool.Rent(minimumLength: vertexColumnCount);

            int edgeLocalX = heightmapTextureRegion.Width;
            int heightmapIndexX = heightmapTextureRegion.Right;
            for (int localY = 0; localY < vertexColumnCount; localY++)
            {
                int heightmapIndexY = heightmapTextureRegion.Y + localY;
                float normalizedHeightValue = normalizedHeightmapData[heightmapIndexX, heightmapIndexY];
                float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);

                edgeVertPosArray[localY] = new Vector3(quadSize.X * edgeLocalX, height, quadSize.Y * localY);
            }

            int localX = edgeLocalX - 1;
            for (int localY = 0; localY < quadCountY; localY++)
            {
                int vertIndex = MathExt.ToIndex1d(localX, localY, vertexRowCount);
                int vertIndexDown = MathExt.ToIndex1d(localX, localY + 1, vertexRowCount);
                ref var vertPosTL = ref vertices[vertIndex].Position;
                ref var vertPosBL = ref vertices[vertIndexDown].Position;

                ref var vertPosTR = ref edgeVertPosArray[localY];
                ref var vertPosBR = ref edgeVertPosArray[localY + 1];

                var localIndexTL = new Int2(Math.Min(localX, edgeLocalX), localY);
                var localIndexTR = localIndexTL + IndexOffsetRight;
                var localIndexBL = localIndexTL + IndexOffsetDown;
                var localIndexBR = localIndexTL + IndexOffsetDown + IndexOffsetRight;

                var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
                AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
            }

            arrayPool.Return(edgeVertPosArray);
        }
        // Corner cases:
        var heightmapIndexOffsetToLocalIndex = new Int2(-heightmapTextureRegion.X, -heightmapTextureRegion.Y);
        // Top-Left corner
        if (heightmapTextureRegion.Left > 0 && heightmapTextureRegion.Top > 0)
        {
            var heightmapStartIndex = new Int2(heightmapTextureRegion.Left - 1, heightmapTextureRegion.Top - 1);
            var quad = CreateQuadAtLocation(heightmapStartIndex, normalizedHeightmapData, heightRange, quadSize, heightmapIndexOffsetToLocalIndex);
            AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
        }
        // Top-Right corner
        if (heightmapTextureRegion.Right < normalizedHeightmapData.LengthX - 1 && heightmapTextureRegion.Top > 0)
        {
            var heightmapStartIndex = new Int2(heightmapTextureRegion.Right - 1, heightmapTextureRegion.Top - 1);
            var quad = CreateQuadAtLocation(heightmapStartIndex, normalizedHeightmapData, heightRange, quadSize, heightmapIndexOffsetToLocalIndex);
            AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
        }
        // Bottom-Left corner
        if (heightmapTextureRegion.Left > 0 && heightmapTextureRegion.Bottom < normalizedHeightmapData.LengthY - 1)
        {
            var heightmapStartIndex = new Int2(heightmapTextureRegion.Left - 1, heightmapTextureRegion.Bottom - 1);
            var quad = CreateQuadAtLocation(heightmapStartIndex, normalizedHeightmapData, heightRange, quadSize, heightmapIndexOffsetToLocalIndex);
            AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
        }
        // Bottom-Right corner
        if (heightmapTextureRegion.Right < normalizedHeightmapData.LengthX - 1 && heightmapTextureRegion.Bottom < normalizedHeightmapData.LengthY - 1)
        {
            var heightmapStartIndex = new Int2(heightmapTextureRegion.Right - 1, heightmapTextureRegion.Bottom - 1);
            var quad = CreateQuadAtLocation(heightmapStartIndex, normalizedHeightmapData, heightRange, quadSize, heightmapIndexOffsetToLocalIndex);
            AccumulateNormalAndTangentsByQuad(quad, vertices, regionSize);
        }
        #endregion End of Normal/Tangent from neighboring chunks

        for (int i = 0; i < vertices.Length; i++)
        {
            // Normalize the Normal vectors
            {
                float length = vertices[i].Normal.Length();
                if (length > MathUtil.ZeroTolerance)
                {
                    float inv = 1.0f / length;
                    vertices[i].Normal *= inv;
                }
                else
                {
                    vertices[i].Normal = Vector3.UnitY;
                }
            }
            // Normalize the Tangent vectors
            {
                float length = vertices[i].Tangent.Length();
                if (length > MathUtil.ZeroTolerance)
                {
                    float inv = 1.0f / length;
                    vertices[i].Tangent *= inv;
                }
                else
                {
                    vertices[i].Tangent = Vector3.UnitX;
                }
            }
        }

        //Debug.WriteLine($"End of Build normals: {heightmapTextureRegion}");
    }

    private static MeshQuadData CreateQuadAtLocation(Int2 heightmapStartIndex, Array2d<float> normalizedHeightmapData, Vector2 heightRange, Vector2 quadSize, Int2 heightmapIndexOffsetToLocalIndex)
    {
        var IndexOffsetRight = new Int2(1, 0);
        var IndexOffsetDown = new Int2(0, 1);

        var indexTL = heightmapStartIndex;
        var indexTR = heightmapStartIndex + IndexOffsetRight;
        var indexBL = heightmapStartIndex + IndexOffsetDown;
        var indexBR = heightmapStartIndex + IndexOffsetDown + IndexOffsetRight;

        var vertPosTL = CreatePoint(indexTL, normalizedHeightmapData, heightRange, quadSize);
        var vertPosTR = CreatePoint(indexTR, normalizedHeightmapData, heightRange, quadSize);
        var vertPosBL = CreatePoint(indexBL, normalizedHeightmapData, heightRange, quadSize);
        var vertPosBR = CreatePoint(indexBR, normalizedHeightmapData, heightRange, quadSize);

        var localIndexTL = indexTL + heightmapIndexOffsetToLocalIndex;
        var localIndexTR = indexTR + heightmapIndexOffsetToLocalIndex;
        var localIndexBL = indexBL + heightmapIndexOffsetToLocalIndex;
        var localIndexBR = indexBR + heightmapIndexOffsetToLocalIndex;
        var quad = MeshQuadData.CreateQuad(vertPosTL, vertPosTR, vertPosBL, vertPosBR, localIndexTL, localIndexTR, localIndexBL, localIndexBR);
        return quad;

        static Vector3 CreatePoint(Int2 heightmapIndex, Array2d<float> normalizedHeightmapData, Vector2 heightRange, Vector2 quadSize)
        {
            var (minHeightPosition, maxHeightPosition) = heightRange;
            float normalizedHeightValue = normalizedHeightmapData[heightmapIndex];
            float height = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);
            var pos = new Vector3(quadSize.X * heightmapIndex.X, height, quadSize.Y * heightmapIndex.Y);
            return pos;
        }
    }

    private static void AccumulateNormalAndTangentsByQuad(in MeshQuadData quad, TerrainVertex[] vertices, Size2 regionSize)
    {
        TryAdd(quad.Triangle0, quad.Triangle0.LocalIndex0, vertices, regionSize);
        TryAdd(quad.Triangle0, quad.Triangle0.LocalIndex1, vertices, regionSize);
        TryAdd(quad.Triangle0, quad.Triangle0.LocalIndex2, vertices, regionSize);
        TryAdd(quad.Triangle1, quad.Triangle1.LocalIndex0, vertices, regionSize);
        TryAdd(quad.Triangle1, quad.Triangle1.LocalIndex1, vertices, regionSize);
        TryAdd(quad.Triangle1, quad.Triangle1.LocalIndex2, vertices, regionSize);

        static bool TryAdd(in MeshQuadTriangleData triangle, Int2 localIndex, TerrainVertex[] vertices, Size2 regionSize)
        {
            if (0 <= localIndex.X && localIndex.X < regionSize.Width
                && 0 <= localIndex.Y && localIndex.Y < regionSize.Height)
            {
                int vertIndex = localIndex.ToIndex1d(regionSize.Width);
                triangle.GetNormalAndTangent(out var normalVec, out var tangentVec);
                vertices[vertIndex].Normal += normalVec;
                vertices[vertIndex].Tangent += tangentVec;
                return true;
            }
            return false;
        }
    }
}
