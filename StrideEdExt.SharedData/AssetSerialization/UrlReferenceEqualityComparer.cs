using Stride.Core.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.SharedData.AssetSerialization;

public class UrlReferenceEqualityComparer : IEqualityComparer<UrlReferenceBase>
{
    private static UrlReferenceEqualityComparer? _instance;
    public static UrlReferenceEqualityComparer Instance
    {
        get
        {
            _instance ??= new();
            return _instance;
        }
    }

    public int Compare(UrlReferenceBase? x, UrlReferenceBase? y)
    {
        int urlCompareResult = string.Compare(x?.Url, y?.Url, StringComparison.OrdinalIgnoreCase);
        return urlCompareResult;
    }

    public bool Equals(UrlReferenceBase? x, UrlReferenceBase? y) => IsSame(x, y);

    public int GetHashCode([DisallowNull] UrlReferenceBase obj)
    {
        int hashCode = obj.Url?.GetHashCode() ?? 0;
        return hashCode;
    }

    public static bool IsSame(UrlReferenceBase? x, UrlReferenceBase? y)
    {
        bool isSame = string.Equals(x?.Url, y?.Url, StringComparison.OrdinalIgnoreCase);
        return isSame;
    }
}
