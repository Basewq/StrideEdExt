namespace StrideEdExt.SharedData;

public class StringInternPool
{
    private readonly HashSet<string> _strings;

    public StringInternPool(bool isCaseSensitive = false)
    {
        var stringComparer = isCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _strings = new(stringComparer);
    }

    public string GetPooled(string key)
    {
        if (_strings.TryGetValue(key, out var pooledKey))
        {
            //Debug.WriteLine($"GetPooled.Pooled found: {pooledKey}");
            return pooledKey;
        }
        //Debug.WriteLine($"GetPooled.Set new: {key}");
        _strings.Add(key);
        return key;
    }

    public string? GetPooledOrNull(string? key)
    {
        if (key is null)
        {
            return null;
        }
        return GetPooled(key);
    }

    public void Clear()
    {
        _strings.Clear();
    }
}
