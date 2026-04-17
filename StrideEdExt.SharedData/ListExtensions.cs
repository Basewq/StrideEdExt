using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt;

public static class ListExtensions
{
    public static bool AddDistinct<TElement>(this List<TElement> list, TElement item, Predicate<TElement> isSameItem)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (isSameItem(list[i]))
            {
                return false;
            }
        }
        list.Add(item);
        return true;
    }

    public static bool TryGetValue<TElement>(this IReadOnlyList<TElement> list, int index, [NotNullWhen(true)] out TElement? value)
    {
        if (index < 0 || index >= list.Count)
        {
            value = default;
            return false;
        }

        value = list[index]!;
        return true;
    }

    public static bool TryGetValue<TElement>(this List<TElement> list, int index, [NotNullWhen(true)] out TElement? value)
    {
        if (index < 0 || index >= list.Count)
        {
            value = default;
            return false;
        }

        value = list[index]!;
        return true;
    }

    public static bool TryFindIndex<TElement>(this IReadOnlyList<TElement> list, Func<TElement, bool> isMatchPredicate, out int index)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (isMatchPredicate(list[i]))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    public static bool TryFindIndex<TElement>(this Span<TElement> list, Func<TElement, bool> equalityComparer, out int index)
    {
        for (int i = 0; i < list.Length; i++)
        {
            if (equalityComparer(list[i]))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    public static bool TryFindIndex<TElement>(this IReadOnlyList<TElement> list, TElement element, IEqualityComparer<TElement> equalityComparer, out int index)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (equalityComparer.Equals(element, list[i]))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    public static bool TryFindIndex<TElement>(this Span<TElement> list, TElement element, IEqualityComparer<TElement> equalityComparer, out int index)
    {
        for (int i = 0; i < list.Length; i++)
        {
            if (equalityComparer.Equals(element, list[i]))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    public static bool TryFindItem<TElement>(this List<TElement> list, Func<TElement, bool> isMatchPredicate, [NotNullWhen(true)] out TElement? foundItem)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var currentItem = list[i];
            if (isMatchPredicate(currentItem))
            {
                foundItem = currentItem!;
                return true;
            }
        }
        foundItem = default;
        return false;
    }

    /// <summary>
    /// Remove the first item found by <paramref name="isMatchPredicate"/>
    /// and copies the element to <paramref name="removedItem"/>.
    /// </summary>
    public static bool TryRemoveItem<TElement>(this List<TElement> list, Func<TElement, bool> isMatchPredicate, [NotNullWhen(true)] out TElement? removedItem)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var currentItem = list[i];
            if (isMatchPredicate(currentItem))
            {
                list.RemoveAt(i);
                removedItem = currentItem!;
                return true;
            }
        }
        removedItem = default;
        return false;
    }
}
