using Stride.Core.Mathematics;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample.Rendering.RenderTextures;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct TextureWorldMeasurement : IEquatable<TextureWorldMeasurement>
{
    public Vector2 TextureOriginWorldPosition;
    public Vector2 TexelWorldSize;

    public TextureWorldMeasurement(Vector2 textureOriginWorldPosition, Vector2 texelWorldSize)
    {
        TextureOriginWorldPosition = textureOriginWorldPosition;
        TexelWorldSize = texelWorldSize;
    }

    public override readonly string ToString() => $"OriginPosition: {TextureOriginWorldPosition} - Size: {TexelWorldSize}";
    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(TextureOriginWorldPosition.GetHashCode(), TexelWorldSize.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TextureWorldMeasurement other && Equals(other);

    public readonly bool Equals(TextureWorldMeasurement other)
    {
        return TextureOriginWorldPosition == other.TextureOriginWorldPosition
            && TexelWorldSize == other.TexelWorldSize;
    }

    public static bool operator ==(TextureWorldMeasurement left, TextureWorldMeasurement right) => left.Equals(right);

    public static bool operator !=(TextureWorldMeasurement left, TextureWorldMeasurement right) => !left.Equals(right);

    /// <summary>
    /// Returns the rectangle that covers the entire area of <paramref name="textureCoords"/>.
    /// </summary>
    public readonly RectangleF GetWorldViewRegionXZ(in Int2 textureCoords)
    {
        // Subtract half TexelWorldSize because the origin sits in the center of the texel
        float x = TextureOriginWorldPosition.X + (textureCoords.X * TexelWorldSize.X) - (0.5f * TexelWorldSize.X);
        float y = TextureOriginWorldPosition.Y + (textureCoords.Y * TexelWorldSize.Y) - (0.5f * TexelWorldSize.Y);
        float width = TexelWorldSize.X;
        float height = TexelWorldSize.Y;
        return new RectangleF(x, y, width, height);
    }

    /// <summary>
    /// Returns the rectangle that covers the entire area from <paramref name="textureCoords1"/> to <paramref name="textureCoords2"/>.
    /// </summary>
    public readonly RectangleF GetWorldViewRegionXZ(in Int2 textureCoords1, in Int2 textureCoords2)
    {
        MathExt.MinMax(textureCoords1.X, textureCoords2.X, out int minCoordsX, out int maxCoordsX);
        MathExt.MinMax(textureCoords1.Y, textureCoords2.Y, out int minCoordsY, out int maxCoordsY);

        // Subtract half TexelWorldSize because the origin sits in the center of the texel
        float x = TextureOriginWorldPosition.X + (minCoordsX * TexelWorldSize.X) - (0.5f * TexelWorldSize.X);
        float y = TextureOriginWorldPosition.Y + (minCoordsY * TexelWorldSize.Y) - (0.5f * TexelWorldSize.Y);
        float width = (maxCoordsX - minCoordsX + 1) * TexelWorldSize.X;
        float height = (maxCoordsY - minCoordsY + 1) * TexelWorldSize.Y;
        return new RectangleF(x, y, width, height);
    }

    /// <summary>
    /// Returns the texture coordinate the point <paramref name="worldPosition"/> is sitting in.
    /// </summary>
    public readonly Int2 GetTextureCoordsXZ(in Vector3 worldPosition)
    {
        // Add half TexelWorldSize because the origin sits in the center of the texel
        float minPosX = worldPosition.X - TextureOriginWorldPosition.X + (0.5f * TexelWorldSize.X);
        float minPosZ = worldPosition.Z - TextureOriginWorldPosition.Y + (0.5f * TexelWorldSize.Y);
        int x = (int)Math.Floor(minPosX / TexelWorldSize.X);
        int y = (int)Math.Floor(minPosZ / TexelWorldSize.Y);
        return new Int2(x, y);
    }

    /// <summary>
    /// Returns the region of texture coordinates the points <paramref name="worldPosition1"/> to <paramref name="worldPosition1"/> are sitting in.
    /// </summary>
    public readonly Rectangle GetTextureCoordsRegionXZ(in Vector3 worldPosition1, in Vector3 worldPosition2)
    {
        MathExt.MinMax(worldPosition1.X, worldPosition2.X, out float minPosX, out float maxPosX);
        MathExt.MinMax(worldPosition1.Z, worldPosition2.Z, out float minPosZ, out float maxPosZ);
        // Add half TexelWorldSize because the origin sits in the center of the texel
        minPosX += 0.5f * TexelWorldSize.X;
        maxPosX += 0.5f * TexelWorldSize.X;
        minPosZ += 0.5f * TexelWorldSize.Y;
        maxPosZ += 0.5f * TexelWorldSize.Y;
        int x = (int)Math.Floor(minPosX / TexelWorldSize.X);
        int y = (int)Math.Floor(minPosZ / TexelWorldSize.Y);
        int width = (int)Math.Floor((maxPosX - minPosX) / TexelWorldSize.X);
        int height = (int)Math.Floor((maxPosZ - minPosZ) / TexelWorldSize.Y);
        return new Rectangle(x, y, width, height);
    }
}
