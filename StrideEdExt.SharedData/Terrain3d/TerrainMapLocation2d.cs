using Stride.Core.Mathematics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct TerrainMapLocation2d : IEquatable<TerrainMapLocation2d>
{
    public Vector3 TerrainOriginWorldPosition;

    public TerrainChunkIndex2d ChunkIndex;
    /// <summary>
    /// The tile index relative to the chunk it belongs to.
    /// </summary>
    public TerrainChunkSubCellIndex2d TileCellIndex;

    public TerrainMapLocation2d(TerrainChunkIndex2d chunkIndex, TerrainChunkSubCellIndex2d tileCellIndex)
    {
        ChunkIndex = chunkIndex;
        TileCellIndex = tileCellIndex;
    }

    public override readonly string ToString() => $"Chunk: {ChunkIndex} - Cell: {TileCellIndex}";

    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(ChunkIndex.GetHashCode(), TileCellIndex.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TerrainMapLocation2d other && Equals(other);

    public readonly bool Equals(TerrainMapLocation2d other)
    {
        return ChunkIndex == other.ChunkIndex
            && TileCellIndex == other.TileCellIndex;
    }

    public static bool operator ==(TerrainMapLocation2d left, TerrainMapLocation2d right) => left.Equals(right);

    public static bool operator !=(TerrainMapLocation2d left, TerrainMapLocation2d right) => !left.Equals(right);

    /// <summary>
    /// Returns the center world position of a tile.
    /// </summary>
    public readonly Vector3 ToWorldPosition(Vector2 chunkSize, float height = 0)
    {
        float x = ChunkIndex.X * chunkSize.X + TileCellIndex.X + 0.5f;
        float z = ChunkIndex.Z * chunkSize.Y + TileCellIndex.Z + 0.5f;

        return new Vector3(x, height, z) + TerrainOriginWorldPosition;
    }

    /// <summary>
    /// Returns the minimum world position of a tile.
    /// </summary>
    public readonly Vector3 ToMinimumWorldPosition(Vector2 chunkSize, float height = 0)
    {
        float x = ChunkIndex.X * chunkSize.X + TileCellIndex.X;
        float z = ChunkIndex.Z * chunkSize.Y + TileCellIndex.Z;

        return new Vector3(x, height, z);
    }

    /// <summary>
    /// Returns the <see cref="TerrainMapCellIndex2d"/> from this tile location.
    /// </summary>
    //public readonly TerrainMapCellIndex2d ToTerrainCellIndex(Vector2 chunkSize)
    //{
    //    int x = ChunkIndex.X * chunkSize.X + TileCellIndex.X;
    //    int y = ChunkIndex.Z * chunkSize.Y + TileCellIndex.Z;

    //    return new TerrainMapCellIndex2d(x, y);
    //}

    /// <summary>
    /// Returns the <see cref="TerrainMapLocation2d"/> that <paramref name="worldPosition"/> is contained in.
    /// </summary>
    public static TerrainMapLocation2d FromWorldPosition(in Vector3 worldPosition, Vector2 chunkSize)
    {
        var mapLoc = new TerrainMapLocation2d();

        int chunkIndexX = (int)MathF.Floor(worldPosition.X / chunkSize.X);
        int chunkIndexY = (int)MathF.Floor(worldPosition.Z / chunkSize.Y);
        mapLoc.ChunkIndex = new TerrainChunkIndex2d(chunkIndexX, chunkIndexY);

        int tileCellIndexX = (int)MathF.Floor(worldPosition.X - chunkIndexX * chunkSize.X);
        int tileCellIndexY = (int)MathF.Floor(worldPosition.Z - chunkIndexY * chunkSize.Y);
        mapLoc.TileCellIndex = new TerrainChunkSubCellIndex2d(tileCellIndexX, tileCellIndexY);

        return mapLoc;
    }

    /// <summary>
    /// Returns the <see cref="TerrainMapLocation2d"/> from a <see cref="TerrainMapCellIndex2d"/>.
    /// </summary>
    public static TerrainMapLocation2d FromTerrainCellIndex(in TerrainMapCellIndex2d terrainCellIndex, Vector2 chunkSize)
    {
        var mapLoc = new TerrainMapLocation2d();

        int chunkIndexX = (int)MathF.Floor(terrainCellIndex.X / chunkSize.X);
        int chunkIndexY = (int)MathF.Floor(terrainCellIndex.Z / chunkSize.Y);
        mapLoc.ChunkIndex = new Int2(chunkIndexX, chunkIndexY);

        int tileCellIndexX = (int)MathF.Floor(terrainCellIndex.X - chunkIndexX * chunkSize.X);
        int tileCellIndexY = (int)MathF.Floor(terrainCellIndex.Z - chunkIndexY * chunkSize.Y);
        mapLoc.TileCellIndex = new Int2(tileCellIndexX, tileCellIndexY);

        return mapLoc;
    }

    /// <summary>
    /// Returns the <see cref="TerrainMapLocation2d"/> from <paramref name="terrainCellIndex"/>.
    /// </summary>
    //public static TerrainMapLocation2d FromTerrainCellIndex(in Int2 terrainCellIndex, Vector2 chunkSize) => FromTerrainCellIndex((TerrainMapCellIndex2d)terrainCellIndex, chunkSize);

    public static TerrainMapCellIndex2d FromWorldPositionToTerrainCellIndex(in Vector3 worldPosition)
    {
        // Y position is discarded
        int tileCellIndexX = (int)MathF.Floor(worldPosition.X);
        int tileCellIndexZ = (int)MathF.Floor(worldPosition.Z);
        var tileCellIndex = new TerrainMapCellIndex2d(tileCellIndexX, tileCellIndexZ);
        return tileCellIndex;
    }

    /// <summary>
    /// Returns the center world position of the tile index.
    /// </summary>
    public static Vector3 FromTerrainCellIndexToWorldPosition(in TerrainMapCellIndex2d terrainCellIndex, float height)
    {
        float x = terrainCellIndex.X + 0.5f;
        float z = terrainCellIndex.Z + 0.5f;
        var worldPosition = new Vector3(x, height, z);
        return worldPosition;
    }
}
