namespace StrideEdExt.SharedData.Terrain3d.Layers;

public interface ITerrainMapHeightmapLayer : ITerrainMapLayer
{
    TerrainHeightmapLayerBlendType LayerBlendType { get; set; }
}
