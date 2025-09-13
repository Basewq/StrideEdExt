using Stride.Core.Mathematics;

namespace StrideEdExt;

public static class Size2Ext
{
    public static Size2 Add(this Size2 left, Size2 right)
    {
        return new Size2(left.Width + right.Width, left.Height + right.Height);
    }

    public static Size2 Add(this Size2 size, Int2 amount)
    {
        return new Size2(size.Width + amount.X, size.Height + amount.Y);
    }

    public static Size2 Subtract(this Size2 left, Size2 right)
    {
        return new Size2(left.Width - right.Width, left.Height - right.Height);
    }

    public static Size2 Subtract(this Size2 size, Int2 amount)
    {
        return new Size2(size.Width - amount.X, size.Height - amount.Y);
    }

    public static Vector2 ToVector2(this Size2 size)
    {
        return new Vector2(size.Width, size.Height);
    }

    public static Int2 ToInt2(this Size2 size)
    {
        return new Int2(size.Width, size.Height);
    }

    public static Size2 ToSize2(this Int2 size)
    {
        return new Size2(size.X, size.Y);
    }
}
