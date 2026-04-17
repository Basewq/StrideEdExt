using Stride.Core.Diagnostics;
using Stride.Core.IO;

namespace StrideEdExt.StrideAssetExt.Assets;

public interface IStrideCustomAsset
{
    /// <summary>
    /// Called after this asset has been loaded from a file at location <paramref name="assetFullFilePath"/>.
    /// </summary>
    void OnAssetLoaded(UFile assetFullFilePath, ILogger? logger);

    /// <summary>
    /// Called before serializing this asset to a file.
    /// </summary>
    void OnAssetSaving(ILogger? logger);
}
