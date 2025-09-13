namespace StrideEdExt.SharedData.Terrain3d.Layers;

public interface ITerrainMapLayer
{
    Guid LayerId { get; }
    Type LayerDataType { get; }
}
