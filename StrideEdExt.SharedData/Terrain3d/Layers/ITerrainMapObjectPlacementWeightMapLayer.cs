namespace StrideEdExt.SharedData.Terrain3d.Layers;

public interface ITerrainMapObjectPlacementWeightMapLayer : ITerrainMapLayer
{
    /// <summary>
    /// Target Object Asset Name. Must match <see cref="TerrainObjectPlacementLayerDefinitionAsset.ObjectAssetName"/>.
    /// </summary>
    string? ObjectAssetName { get; }
}
