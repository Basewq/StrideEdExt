using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.Spawners;

public class ManualPrefabSpawnerComponent : ObjectSpawnerComponentBase
{
    public override Type LayerDataType => typeof(ManualPrefabSpawnerData);

    // Note that none of the RNG spawner properties are relevant for this component.

    protected override void OnInitialize()
    {
        HasChanged = false;
    }
}
