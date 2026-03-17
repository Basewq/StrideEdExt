using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt;

public static class ListExtensions
{
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
}
