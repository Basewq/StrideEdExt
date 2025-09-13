using Stride.Core.Mathematics;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.SharedData.Terrain3d;

public class TerrainChunk
{
    private Array2d<TerrainSubChunk> _chunkMeshArray2d = default!;

    public TerrainMap TerrainMap;

    public TerrainChunkIndex2d ChunkIndex;

    /// <summary>
    /// The total region the chunk covers.
    /// </summary>
    public Rectangle HeightmapTextureRegion;

    public TerrainChunk(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex)
    {
        TerrainMap = terrainMap;
        ChunkIndex = chunkIndex;

        HeightmapTextureRegion = CalculateChunkHeightmapRegion(terrainMap, chunkIndex);
        RebuildChunkMeshArray();
    }

    private static Rectangle CalculateChunkHeightmapRegion(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex)
    {
        var heightmapTextureSize = terrainMap.HeightmapTextureSize;
        var quadPerChunk = terrainMap.QuadPerChunk;
        int chunkX = chunkIndex.X;
        int chunkY = chunkIndex.Z;
        // +1 because it requires 2x2 cells/vertices to form a quad
        int cellIndexEndXExcl = Math.Min((chunkX + 1) * quadPerChunk.X + 1, heightmapTextureSize.Width);
        int cellIndexEndYExcl = Math.Min((chunkY + 1) * quadPerChunk.Y + 1, heightmapTextureSize.Height);
        int hmWidth = cellIndexEndXExcl - (chunkX * quadPerChunk.X);
        int hmHeight = cellIndexEndYExcl - (chunkY * quadPerChunk.Y);

        var heightmapTextureRegion = new Rectangle(chunkX * quadPerChunk.X, chunkY * quadPerChunk.Y, hmWidth, hmHeight);
        return heightmapTextureRegion;
    }

    private void RebuildChunkMeshArray()
    {
        var heightmapTextureSize = TerrainMap.HeightmapTextureSize;
        int meshPerChunkSingleAxisLength = TerrainMap.MeshPerChunk.GetSingleAxisLength();
        var quadsPerMesh = TerrainMap.QuadsPerMesh;
        var verticesPerMesh = quadsPerMesh + Int2.One;
        _chunkMeshArray2d = new Array2d<TerrainSubChunk>(meshPerChunkSingleAxisLength, meshPerChunkSingleAxisLength);
        for (int y = 0; y < meshPerChunkSingleAxisLength; y++)
        {
            var meshHeightmapTextureRegion = Rectangle.Empty;
            meshHeightmapTextureRegion.Y = HeightmapTextureRegion.Y + y * quadsPerMesh.Y;
            int cellIndexEndYExcl = Math.Min(meshHeightmapTextureRegion.Y + verticesPerMesh.Y, heightmapTextureSize.Height);
            meshHeightmapTextureRegion.Height = cellIndexEndYExcl - meshHeightmapTextureRegion.Y;

            for (int x = 0; x < meshPerChunkSingleAxisLength; x++)
            {
                meshHeightmapTextureRegion.X = HeightmapTextureRegion.X + x * quadsPerMesh.X;
                int cellIndexEndXExcl = Math.Min(meshHeightmapTextureRegion.X + verticesPerMesh.X, heightmapTextureSize.Width);
                meshHeightmapTextureRegion.Width = cellIndexEndXExcl - meshHeightmapTextureRegion.X;

                var chunkMesh = new TerrainSubChunk
                {
                    SubCellIndex = new(x, y),
                    HeightmapTextureRegion = meshHeightmapTextureRegion,
                    Mesh = null     // Don't generate the mesh immediately
                };
                _chunkMeshArray2d[x, y] = chunkMesh;
            }
        }
    }

    public TerrainSubChunk GetSubChunk(TerrainChunkSubCellIndex2d subCellIndex)
    {
        var subChunk = _chunkMeshArray2d[subCellIndex.X, subCellIndex.Z];
        return subChunk;
    }

    public bool TryGetSubChunk(TerrainChunkSubCellIndex2d subCellIndex, [NotNullWhen(true)] out TerrainSubChunk? subChunk)
    {
        bool isFound = _chunkMeshArray2d.TryGetValue((Int2)subCellIndex, out subChunk);
        return isFound;
    }
}

public class TerrainSubChunk
{
    public TerrainChunkSubCellIndex2d SubCellIndex;
    /// <summary>
    /// The region the chunk mesh covers.
    /// </summary>
    public Rectangle HeightmapTextureRegion;
    public Mesh? Mesh;
}
