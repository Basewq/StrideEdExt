using Stride.Core.IO;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers;

public static class TerrainMapLayerDataExtensions
{
    public static string GetFilePathOrDefaultPath(
        this TerrainMapLayerDataBase layerData,
        string? existingFilePath,
        UDirectory packageFolderPath, string defaultFileNameFormat)
    {
        string? heightmapFullFilePath = existingFilePath;
        if (string.IsNullOrEmpty(heightmapFullFilePath))
        {
            string fileName = string.Format(defaultFileNameFormat, layerData.LayerId.ToString("N"));
            heightmapFullFilePath = UDirectory.Combine(packageFolderPath, fileName).ToOSPath();
        }
        return heightmapFullFilePath;
    }

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
