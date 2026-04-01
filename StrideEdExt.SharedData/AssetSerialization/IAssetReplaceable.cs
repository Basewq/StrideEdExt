namespace StrideEdExt.SharedData.AssetSerialization;

public interface IAssetReplaceable<T>
{
    void CopyContentsTo(ref T obj);
}
