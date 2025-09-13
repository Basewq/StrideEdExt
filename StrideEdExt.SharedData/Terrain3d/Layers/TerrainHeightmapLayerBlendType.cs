using System.Runtime.Serialization;

namespace StrideEdExt.SharedData.Terrain3d.Layers;

[DataContract]
public enum TerrainHeightmapLayerBlendType
{
    Average,
    Minimum,
    Maximum
}
