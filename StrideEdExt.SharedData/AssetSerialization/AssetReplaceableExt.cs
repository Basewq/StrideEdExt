using System.Runtime.InteropServices;

namespace StrideEdExt.SharedData.AssetSerialization;

public static class AssetReplaceableExt
{
    /// <summary>
    /// Update an existing <see cref="List{T}"/> with the items from another <see cref="List{T}"/>
    /// and reuse the current items in the List where possible.
    /// Used to provide a more stable YAML file when changing the contents of the list.
    /// </summary>
    public static void ReplaceList<T>(List<T> sourceList, List<T> destinationList)
        where T : IAssetReplaceable<T>
    {
        int replaceableCount = Math.Min(sourceList.Count, destinationList.Count);
        var srcSpan = CollectionsMarshal.AsSpan(destinationList);
        var destSpan = CollectionsMarshal.AsSpan(destinationList);
        for (int i = 0; i < replaceableCount; i++)
        {
            srcSpan[i].CopyContentsTo(ref destSpan[i]);
        }

        int remainingSourceItemCount = sourceList.Count - replaceableCount;
        if (remainingSourceItemCount > 0)
        {
            for (int i = replaceableCount; i < remainingSourceItemCount; i++)
            {
                destinationList.Add(srcSpan[i]);
            }
        }
        else if (remainingSourceItemCount < 0)
        {
            int removeItemCount = -remainingSourceItemCount;
            destinationList.RemoveRange(replaceableCount, removeItemCount);
        }
    }

    /// <summary>
    /// Update an existing <see cref="List{string}"/> with the items from another <see cref="List{string}"/>.
    /// Used to provide a more stable YAML file when changing the contents of the list.
    /// </summary>
    public static void ReplaceList(List<string> sourceList, List<string> destinationList)
    {
        int replaceableCount = Math.Min(sourceList.Count, destinationList.Count);
        var srcSpan = CollectionsMarshal.AsSpan(destinationList);
        var destSpan = CollectionsMarshal.AsSpan(destinationList);
        for (int i = 0; i < replaceableCount; i++)
        {
            destSpan[i] = srcSpan[i];
        }

        int remainingSourceItemCount = sourceList.Count - replaceableCount;
        if (remainingSourceItemCount > 0)
        {
            for (int i = replaceableCount; i < remainingSourceItemCount; i++)
            {
                destinationList.Add(srcSpan[i]);
            }
        }
        else if (remainingSourceItemCount < 0)
        {
            int removeItemCount = -remainingSourceItemCount;
            destinationList.RemoveRange(replaceableCount, removeItemCount);
        }
    }
}
