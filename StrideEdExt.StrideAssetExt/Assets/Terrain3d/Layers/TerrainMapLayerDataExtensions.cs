namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers;

public static class TerrainMapLayerDataExtensions
{
    public static bool TryGetMaterialIndex(
        this List<TerrainMaterialLayerDefinitionAsset> materialLayers,
        string? materialName,
        out byte materialIndex)
    {
        if (!string.IsNullOrEmpty(materialName))
        {
            for (int i = 0; i < materialLayers.Count; i++)
            {
                if (string.Equals(materialName, materialLayers[i].MaterialName, StringComparison.OrdinalIgnoreCase))
                {
                    materialIndex = (byte)i;
                    return true;
                }
            }
        }
        materialIndex = 0;
        return false;
    }
}
