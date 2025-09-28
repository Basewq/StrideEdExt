using Stride.Core.IO;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers;

public static class ObjectPlacementLayerDataExtensions
{
    public static string GetFilePathOrDefaultPath(
        this ObjectPlacementLayerDataBase layerData,
        string? existingFilePath,
        UDirectory packageFolderPath, string defaultFileNameFormat)
    {
        string? layerFullFilePath = existingFilePath;
        if (string.IsNullOrEmpty(layerFullFilePath))
        {
            string fileName = string.Format(defaultFileNameFormat, layerData.LayerId.ToString("N"));
            layerFullFilePath = UDirectory.Combine(packageFolderPath, fileName).ToOSPath();
        }
        return layerFullFilePath;
    }
}
