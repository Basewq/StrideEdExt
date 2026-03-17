using StrideEdExt.SharedData.Terrain3d;

namespace StrideEdExt.WorldTerrain.Terrain3d;

public record struct DisplayTerrainChunkIndex(TerrainChunkIndex2d ChunkIndex, TerrainChunkSubCellIndex2d ChunkSubCellIndex);
