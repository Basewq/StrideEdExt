using Stride.Core.Mathematics;

namespace StrideEdExt;

public static class MathExt
{
    public static int ToIntFloor(float value) => (int)MathF.Floor(value);

    /// <param name="fracValue">The positive fraction where the returned value + <paramref name="fracValue"/> is equal to <paramref name="value"/>.</param>
    public static int ToIntFloor(float value, out float fracValue)
    {
        int intValue = (int)MathF.Floor(value);
        if (value >= 0)
        {
            fracValue = value - intValue;
        }
        else
        {
            fracValue = intValue - value;
        }
        return intValue;
    }

    public static Int2 ToInt2Floor(Vector2 vec) => new Int2(ToIntFloor(vec.X), ToIntFloor(vec.Y));

    /// <param name="fracVec">The positive fractions where the returned value + <paramref name="fracVec"/> is equal to <paramref name="vec"/>.</param>
    public static Int2 ToInt2Floor(Vector2 vec, out Vector2 fracVec)
    {
        int intValueX = ToIntFloor(vec.X, out float fracValueX);
        int intValueY = ToIntFloor(vec.Y, out float fracValueY);
        var intValue = new Int2(intValueX, intValueY);
        fracVec = new Vector2(fracValueX, fracValueY);
        return intValue;
    }

    public static Int3 ToInt3Floor(Vector3 vec) => new Int3(ToIntFloor(vec.X), ToIntFloor(vec.Y), ToIntFloor(vec.Z));

    /// <param name="fracVec">The positive fractions where the returned value + <paramref name="fracVec"/> is equal to <paramref name="vec"/>.</param>
    public static Int3 ToInt3Floor(Vector3 vec, out Vector3 fracVec)
    {
        int intValueX = ToIntFloor(vec.X, out float fracValueX);
        int intValueY = ToIntFloor(vec.Y, out float fracValueY);
        int intValueZ = ToIntFloor(vec.Z, out float fracValueZ);
        var intValue = new Int3(intValueX, intValueY, intValueZ);
        fracVec = new Vector3(fracValueX, fracValueY, fracValueZ);
        return intValue;
    }

    public static Vector3 ToVec3(Int3 int3) => new Vector3(int3.X, int3.Y, int3.Z);

    public static int ToIndex1d(int x, int y, int rowWidth)
    {
        int index1d = (y * rowWidth) + x;
        return index1d;
    }

    public static int ToIndex1d(this Int2 int2, int rowWidth) => ToIndex1d(int2.X, int2.Y, rowWidth);

    public static BoundingBox ShiftPosition(this BoundingBox boundingBox, Vector3 offset)
    {
        var newBoundingBox = new BoundingBox(boundingBox.Minimum + offset, boundingBox.Maximum + offset);
        return newBoundingBox;
    }

    public static BoundingSphere ShiftPosition(this BoundingSphere boundingSphere, Vector3 offset)
    {
        var newBoundingSphere = new BoundingSphere(boundingSphere.Center + offset, boundingSphere.Radius);
        return newBoundingSphere;
    }

    public static void GetMinMax(int value1, int value2, out int minValue, out int maxValue)
    {
        if (value1 <= value2)
        {
            minValue = value1;
            maxValue = value2;
        }
        else
        {
            minValue = value2;
            maxValue = value1;
        }
    }

    public static void MinMax(float value1, float value2, out float minValue, out float maxValue)
    {
        if (value1 <= value2)
        {
            minValue = value1;
            maxValue = value2;
        }
        else
        {
            minValue = value2;
            maxValue = value1;
        }
    }
}
