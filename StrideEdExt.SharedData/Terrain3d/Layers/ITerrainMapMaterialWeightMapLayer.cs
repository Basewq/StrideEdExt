namespace StrideEdExt.SharedData.Terrain3d.Layers;

public interface ITerrainMapMaterialWeightMapLayer : ITerrainMapLayer
{
    /// <summary>
    /// Target Material Name. Must match <see cref="TerrainMaterialLayerDefinitionAsset.MaterialName"/>.
    /// </summary>
    string? MaterialName { get; }
}
