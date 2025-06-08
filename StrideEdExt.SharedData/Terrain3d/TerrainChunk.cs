using Stride.Core.Mathematics;
using Stride.Rendering;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

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
        var mapSize = terrainMap.MapSize;
        var quadPerChunk = terrainMap.QuadPerChunk;
        int x = chunkIndex.X;
        int y = chunkIndex.Z;

        // +1 because it requires 2x2 cells/vertices to form a quad
        int cellIndexEndX = Math.Min(x + quadPerChunk.X + 1, mapSize.X - 1);
        int cellIndexEndY = Math.Min(y + quadPerChunk.Y + 1, mapSize.Y - 1);
        int hmWidth = cellIndexEndX - x;
        int hmHeight = cellIndexEndY - y;

        var heightmapTextureRegion = new Rectangle(x * quadPerChunk.X, y * quadPerChunk.Y, hmWidth, hmHeight);
        return heightmapTextureRegion;
    }

    private void RebuildChunkMeshArray()
    {
        int meshPerChunkSingleAxisLength = TerrainMap.MeshPerChunk.GetSingleAxisLength();
        var quadPerMesh = TerrainMap.QuadPerMesh;
        _chunkMeshArray2d = new Array2d<TerrainSubChunk>(meshPerChunkSingleAxisLength, meshPerChunkSingleAxisLength);
        for (int y = 0; y < meshPerChunkSingleAxisLength; y++)
        {
            var meshHeightmapTextureRegion = Rectangle.Empty;
            meshHeightmapTextureRegion.Y = HeightmapTextureRegion.Y + y * quadPerMesh.Y;
            int cellIndexEndY = Math.Min(meshHeightmapTextureRegion.Y + quadPerMesh.Y, TerrainMap.MapSize.Y);
            meshHeightmapTextureRegion.Height = cellIndexEndY - meshHeightmapTextureRegion.Y + 1;

            for (int x = 0; x < meshPerChunkSingleAxisLength; x++)
            {
                meshHeightmapTextureRegion.X = HeightmapTextureRegion.X + x * quadPerMesh.X;
                int cellIndexEndX = Math.Min(meshHeightmapTextureRegion.X + quadPerMesh.X, TerrainMap.MapSize.X);
                meshHeightmapTextureRegion.Width = cellIndexEndX - meshHeightmapTextureRegion.X + 1;

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
