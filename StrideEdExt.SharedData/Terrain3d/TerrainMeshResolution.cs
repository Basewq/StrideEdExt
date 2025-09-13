using Stride.Core;

namespace StrideEdExt.SharedData.Terrain3d;

/// <summary>
/// The resolution of the height map texture for each terrain mesh.
/// </summary>
[DataContract]
public enum TerrainMeshResolution
{
    Unknown = 0,
    Size8x8 = 8,
    Size16x16 = 16,
    Size32x32 = 32,
    Size64x64 = 64,
    Size128x128 = 128,
    Size256x256 = 256,
}

public static class TerrainMeshQuadResolutionExtensions
{
    public static int GetSingleAxisResolution(this TerrainMeshResolution quadResolution) => (int)quadResolution;
}
