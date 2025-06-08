using SceneEditorExtensionExample.SharedData;
using Stride.Core.Mathematics;
using System.Diagnostics;

namespace SceneEditorExtensionExample.WorldTerrain.TerrainMesh;

record struct TerrainMeshData
{
    public required TerrainVertex[] Vertices;
    public required ushort[] VertexIndices;
    public required int TriangleCount;

    public static TerrainMeshData Generate(Rectangle heightmapTextureRegion, Int2 mapSize, Array2d<float>? normalizedHeightmapData, Vector2 quadSize, Vector2 heightRange)
    {
        var quadCountX = heightmapTextureRegion.Width - 1;
        var quadCountY = heightmapTextureRegion.Height - 1;
        Debug.Assert(quadCountX <= byte.MaxValue && quadCountY <= byte.MaxValue, $"Quad length must be 255 or less to fit on ushort array.");
        const int TrianglesPerQuad = 2;
        const int VerticesPerTriangle = 3;

        int totalUniqueTriangles = (quadCountX + 1) * (quadCountY + 1);
        int totalQuads = quadCountX * quadCountY;
        int totalVertexIndices = totalQuads * TrianglesPerQuad * VerticesPerTriangle;   // Mesh will be TriangleList

        var mesh = new TerrainMeshData
        {
            Vertices = new TerrainVertex[totalUniqueTriangles],
            VertexIndices = new ushort[totalVertexIndices],
            TriangleCount = totalQuads * TrianglesPerQuad
        };

        int vertexRowCount = quadCountX + 1;
        // Build the vertices
        var mapSizeVec2 = (Vector2)mapSize;
        var uvStart = new Vector2(heightmapTextureRegion.X, heightmapTextureRegion.Y) / mapSizeVec2;
        for (int y = 0; y <= quadCountY; y++)
        {
            for (int x = 0; x <= quadCountX; x++)
            {
                int vertIdx = MathExt.ToIndex1d(x, y, vertexRowCount);
                float height = 0;               // Set in SetVertexHeights
                mesh.Vertices[vertIdx] = new TerrainVertex
                {
                    Position = new Vector3(quadSize.X * x, height, quadSize.Y * y),
                    Normal = Vector3.Zero,      // Calculated in SetVertexNormals
                    Color = Color.White,
                    TextureCoords0 = uvStart + (new Vector2(x, y) / mapSizeVec2),
                };
            }
        }
        if (normalizedHeightmapData is not null)
        {
            SetVertexHeights(normalizedHeightmapData, heightmapTextureRegion, heightRange, mesh.Vertices);
        }
        SetVertexNormals(mesh.Vertices, mesh.VertexIndices);

        // Build the vertex indices
        int nextVertexIndex = 0;
        for (int y = 0; y < quadCountY; y++)
        {
            for (int x = 0; x < quadCountX; x++)
            {
                int vertIdx0 = MathExt.ToIndex1d(x, y, vertexRowCount); // Top left
                int vertIdx1 = vertIdx0 + 1;                            // Top right
                int vertIdx2 = vertIdx0 + vertexRowCount;               // Bottom left
                int vertIdx3 = vertIdx2 + 1;                            // Bottom right

                /* Quad to Triangle (clockwise winding):
                 * 0---1
                 * | / |
                 * 2---3
                 */
                // Upper triangle
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx0;
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
                // Lower triangle
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx1;
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx3;
                mesh.VertexIndices[nextVertexIndex++] = (ushort)vertIdx2;
            }
        }

        return mesh;
    }

    private static unsafe void SetVertexHeights(
        Array2d<float> normalizedHeightmapData, Rectangle heightmapTextureRegion, Vector2 heightRange,
        TerrainVertex[] vertices)
    {
        float minHeightPosition = heightRange.X;
        float maxHeightPosition = heightRange.Y;

        var vertCountX = heightmapTextureRegion.Width;
        var vertCountY = heightmapTextureRegion.Height;
        for (int y = 0; y < vertCountY; y++)
        {
            int texIndexY = heightmapTextureRegion.Y + y;
            for (int x = 0; x < vertCountX; x++)
            {
                int texIndexX = heightmapTextureRegion.X + x;
                float normalizedHeightValue = normalizedHeightmapData[texIndexX, texIndexY];

                int vertIndex1d = MathExt.ToIndex1d(x, y, vertCountX);
                vertices[vertIndex1d].Position.Y = MathUtil.Lerp(minHeightPosition, maxHeightPosition, normalizedHeightValue);
            }
        }
    }

    private static unsafe void SetVertexNormals(
        TerrainVertex[] vertices, ushort[] vertexIndices)
    {
        // Calculate normals adapted from https://github.com/simondarksidej/XNAGameStudio/wiki/Riemers3DXNA1Terrain13buffers

        const int VerticesPerTriangle = 3;
        for (int i = 0; i < vertexIndices.Length / VerticesPerTriangle; i++)
        {
            int vertIndex1 = vertexIndices[i * VerticesPerTriangle];
            int vertIndex2 = vertexIndices[i * VerticesPerTriangle + 1];
            int vertIndex3 = vertexIndices[i * VerticesPerTriangle + 2];

            Vector3 posDelta1 = vertices[vertIndex1].Position - vertices[vertIndex3].Position;
            Vector3 posDelta2 = vertices[vertIndex1].Position - vertices[vertIndex2].Position;
            Vector3 normal = Vector3.Cross(posDelta1, posDelta2);

            vertices[vertIndex1].Normal += normal;
            vertices[vertIndex2].Normal += normal;
            vertices[vertIndex3].Normal += normal;
        }
        for (int i = 0; i < vertices.Length; i++)
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
    }
}
