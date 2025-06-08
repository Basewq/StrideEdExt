using Stride.Core;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

/// <summary>
/// The number of terrain meshes that a terrain chunk contains.
/// </summary>
[DataContract]
public enum TerrainMeshPerChunk
{
    Unknown = 0,
    Count1x1,
    Count2x2,
    Count3x3,
    Count4x4,
    Count5x5,
    Count6x6,
    Count7x7,
    Count8x8,
}

public static class TerrainMeshPerChunkExtensions
{
    public static int GetSingleAxisLength(this TerrainMeshPerChunk meshPerChunk) => (int)meshPerChunk;
}
