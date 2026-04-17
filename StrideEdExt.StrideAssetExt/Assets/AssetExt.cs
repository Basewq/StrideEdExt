using Stride.Core.Diagnostics;

namespace StrideEdExt.StrideAssetExt.Assets;

public static class AssetExt
{
    public static bool TryDeleteFile(string? fullFilePath, ILogger? logger)
    {
        if (!File.Exists(fullFilePath))
        {
            return false;
        }
        try
        {
            File.Delete(fullFilePath);
            return true;
        }
        catch (Exception ex)
        {
            logger?.Warning($"Failed to delete file: {fullFilePath}\r\n{ex}");
        }
        return false;
    }
}
