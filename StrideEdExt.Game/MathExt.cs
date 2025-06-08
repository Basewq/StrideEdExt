using Stride.Core.Mathematics;
using System;

namespace SceneEditorExtensionExample;

public static class MathExt
{
    public static int ToIntFloor(float value) => (int)MathF.Floor(value);

    public static Int2 ToInt2Floor(Vector2 vec) => new Int2(ToIntFloor(vec.X), ToIntFloor(vec.Y));

    public static Int3 ToInt3Floor(Vector3 vec) => new Int3(ToIntFloor(vec.X), ToIntFloor(vec.Y), ToIntFloor(vec.Z));

    public static Vector3 ToVec3(Int3 int3) => new Vector3(int3.X, int3.Y, int3.Z);

    public static int ToIndex1d(int x, int y, int rowWidth)
    {
        int index1d = (y * rowWidth) + x;
        return index1d;
    }

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

    public static void MinMax(int value1, int value2, out int minValue, out int maxValue)
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
