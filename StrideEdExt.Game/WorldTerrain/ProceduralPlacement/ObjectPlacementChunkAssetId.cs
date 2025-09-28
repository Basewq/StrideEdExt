using StrideEdExt.SharedData.Terrain3d;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement;

public record struct ObjectPlacementChunkAssetId(TerrainChunkIndex2d ChunkIndex, string AssetUrl) : IEquatable<ObjectPlacementChunkAssetId>
{
    public override readonly int GetHashCode()
    {
        int hashCode = HashCode.Combine(
            ChunkIndex.GetHashCode(),
            string.GetHashCode(AssetUrl, StringComparison.OrdinalIgnoreCase)
        );
        return hashCode;
    }

    public readonly bool Equals(ObjectPlacementChunkAssetId other)
    {
        bool isEqual = ChunkIndex.Equals(other.ChunkIndex)
            && string.Equals(AssetUrl, other.AssetUrl, StringComparison.OrdinalIgnoreCase);
        return isEqual;
    }
}
