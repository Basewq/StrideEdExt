namespace StrideEdExt;

public static class ListExtensions
{
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

    public static bool TryFindIndex<TElement>(this Span<TElement> list, Func<TElement, bool> isMatchPredicate, out int index)
    {
        for (int i = 0; i < list.Length; i++)
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
}
