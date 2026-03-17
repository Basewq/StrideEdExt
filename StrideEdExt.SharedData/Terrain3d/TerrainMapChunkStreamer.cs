using Stride.Core;
using Stride.Engine;

namespace StrideEdExt.SharedData.Terrain3d;

[DataContract]
public class TerrainMapChunkStreamer : EntityComponent
{
    public float StreamRadius { get; set; } = 30f;
}
