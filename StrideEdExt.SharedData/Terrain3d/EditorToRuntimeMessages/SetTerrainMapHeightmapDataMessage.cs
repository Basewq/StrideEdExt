using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetTerrainMapHeightmapDataMessage : TerrainMapMessageBase
{
    /// <summary>
    /// True if this is sent as the initial data (eg. editor ready),
    /// false if the data changed due to modifications.
    /// </summary>
    public required bool IsInitialData { get; init; }
    public required Size2 HeightmapTextureSize { get; init; }
    public required Array2d<float> HeightmapData { get; init; }
}
