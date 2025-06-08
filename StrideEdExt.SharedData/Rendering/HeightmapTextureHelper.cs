using Stride.Core.Mathematics;
using Stride.Graphics;
using Half = System.Half;

namespace SceneEditorExtensionExample.SharedData.Rendering;

public static class HeightmapTextureHelper
{
    // float/Half values are normalized in range [0...1].
    public static readonly Func<byte, float> ByteToNormalizedFloat = byteValue => byteValue / (float)byte.MaxValue;
    public static readonly Func<byte, Half> ByteToNormalizedHalf = byteValue => (Half)ByteToNormalizedFloat(byteValue);
    public static readonly Func<ushort, float> Int16ToNormalizedFloat = int16Value => int16Value / (float)ushort.MaxValue;

    public static readonly Func<float, ushort> NormalizedFloatToInt16 = floatValue => (ushort)Math.Clamp(MathF.Round((float)floatValue * ushort.MaxValue), 0, ushort.MaxValue);
    public static readonly Func<Half, ushort> NormalizedHalfToInt16 = halfValue => NormalizedFloatToInt16((float)halfValue);

    public static readonly Func<byte, ushort> ExpandByteToInt16 = byteValue => (ushort)(byteValue * (ushort.MaxValue / byte.MaxValue));

    /// <summary>
    /// Returns 2D array with normalized <see cref="float"/> values in range [0...1].
    /// </summary>
    public unsafe static Array2d<float> ConvertToArray2dDataFloat(Image heightmapImage)
    {
        var heightmapDataGenerator = GetHeightmapDataGeneratorFloat(heightmapImage.Description.Format);
        var arrayData = heightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    /// <summary>
    /// Returns 2D array with normalized <see cref="float"/> values in range [0...1], or null.
    /// </summary>
    public unsafe static Array2d<float?> ConvertToMaskableArray2dDataFloat(Image heightmapImage)
    {
        var maskableHeightmapDataGenerator = GetMaskableHeightmapDataGeneratorFloat(heightmapImage.Description.Format);
        var arrayData = maskableHeightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    /// <summary>
    /// Returns 2D array with normalized <see cref="Half"/> values in range [0...1].
    /// </summary>
    public unsafe static Array2d<Half> ConvertToArray2dDataHalf(Image heightmapImage)
    {
        var heightmapDataGenerator = GetHeightmapDataGeneratorHalf(heightmapImage.Description.Format);
        var arrayData = heightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    /// <summary>
    /// Returns 2D array with normalized <see cref="Half"/> values in range [0...1], or null.
    /// </summary>
    public unsafe static Array2d<Half?> ConvertToMaskableArray2dDataHalf(Image heightmapImage)
    {
        var maskableHeightmapDataGenerator = GetMaskableHeightmapDataGeneratorHalf(heightmapImage.Description.Format);
        var arrayData = maskableHeightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    /// <summary>
    /// Returns 2D array with normalized <see cref="ushort"/> values in range [0...<see cref="ushort.MaxValue"/>], or null.
    /// </summary>
    public unsafe static Array2d<ushort> ConvertToArray2dDataInt16(Image heightmapImage)
    {
        var heightmapDataGenerator = GetHeightmapDataGeneratorInt16(heightmapImage.Description.Format);
        var arrayData = heightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    /// <summary>
    /// Returns 2D array with normalized <see cref="ushort"/> values in range [0...<see cref="ushort.MaxValue"/>], or null.
    /// </summary>
    public unsafe static Array2d<ushort?> ConvertToMaskableArray2dDatauInt16(Image heightmapImage)
    {
        var maskableHeightmapDataGenerator = GetMaskableHeightmapDataGeneratorInt16(heightmapImage.Description.Format);
        var arrayData = maskableHeightmapDataGenerator.GenerateFromImage(heightmapImage);
        return arrayData;
    }

    private static int ToIndex1d(int x, int y, int rowWidth)
    {
        int index1d = (y * rowWidth) + x;
        return index1d;
    }

    private static IHeightmapDataGenerator<float> GetHeightmapDataGeneratorFloat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R32_Float => new HeightmapDataGenerator<float, float>(valuePickerFunc: x => x),
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, float>(valuePickerFunc: x => (float)x),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, float>(valuePickerFunc: x => ByteToNormalizedFloat(x.R)),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, float>(valuePickerFunc: x => ByteToNormalizedFloat(x.R)),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        }; ;
    }

    private static IHeightmapDataGenerator<Half> GetHeightmapDataGeneratorHalf(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R32_Float => new HeightmapDataGenerator<float, Half>(valuePickerFunc: x => (Half)x),
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, Half>(valuePickerFunc: x => x),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, Half>(valuePickerFunc: x => ByteToNormalizedHalf(x.R)),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, Half>(valuePickerFunc: x => ByteToNormalizedHalf(x.R)),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        }; ;
    }

    private static IHeightmapDataGenerator<ushort> GetHeightmapDataGeneratorInt16(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, ushort>(valuePickerFunc: x => NormalizedHalfToInt16(x)),
            PixelFormat.R16_UNorm => new HeightmapDataGenerator<ushort, ushort>(valuePickerFunc: x => x),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, ushort>(valuePickerFunc: x => ExpandByteToInt16(x.R)),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, ushort>(valuePickerFunc: x => ExpandByteToInt16(x.R)),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        }; ;
    }

    private interface IHeightmapDataGenerator<TArrayValue>
    {
        Array2d<TArrayValue> GenerateFromImage(Image heightmapImage);
    }

    private class HeightmapDataGenerator<TImageValue, TArrayValue> : IHeightmapDataGenerator<TArrayValue>
    {
        private readonly Func<TImageValue, TArrayValue> _valuePickerFunc;

        public HeightmapDataGenerator(Func<TImageValue, TArrayValue> valuePickerFunc)
        {
            _valuePickerFunc = valuePickerFunc;
        }

        public unsafe Array2d<TArrayValue> GenerateFromImage(Image heightmapImage)
        {
            int imgWidth = heightmapImage.Description.Width;
            int imgHeight = heightmapImage.Description.Height;

            var imageDataSpan = new Span<TImageValue>((void*)heightmapImage.DataPointer, imgWidth * imgHeight);

            var arrayData = new Array2d<TArrayValue>(imgWidth, imgHeight);
            for (int y = 0; y < imgHeight; y++)
            {
                for (int x = 0; x < imgWidth; x++)
                {
                    int index1d = ToIndex1d(x, y, imgWidth);
                    arrayData[x, y] = _valuePickerFunc(imageDataSpan[index1d]);
                }
            }
            return arrayData;
        }
    }

    private static IHeightmapDataGenerator<float?> GetMaskableHeightmapDataGeneratorFloat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R16G16_Float => new HeightmapDataGenerator<(Half R, Half G), float?>(valuePickerFunc: x => x.G != Half.Zero ? (float)x.R : null),
            PixelFormat.R32_Float => new HeightmapDataGenerator<float, float?>(valuePickerFunc: x => x),
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, float?>(valuePickerFunc: x => (float)x),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, float?>(valuePickerFunc: x => x.A > 0 ? ByteToNormalizedFloat(x.R) : null),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, float?>(valuePickerFunc: x => x.A > 0 ? ByteToNormalizedFloat(x.R) : null),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        };
    }

    private static IHeightmapDataGenerator<Half?> GetMaskableHeightmapDataGeneratorHalf(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R16G16_Float => new HeightmapDataGenerator<(Half R, Half G), Half?>(valuePickerFunc: x => x.G != Half.Zero ? x.R : null),
            PixelFormat.R32_Float => new HeightmapDataGenerator<float, Half?>(valuePickerFunc: x => (Half)x),
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, Half?>(valuePickerFunc: x => x),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, Half?>(valuePickerFunc: x => x.A > 0 ? ByteToNormalizedHalf(x.R) : null),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, Half?>(valuePickerFunc: x => x.A > 0 ? ByteToNormalizedHalf(x.R) : null),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        };
    }

    private static IHeightmapDataGenerator<ushort?> GetMaskableHeightmapDataGeneratorInt16(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R16G16_Float => new HeightmapDataGenerator<(Half R, Half G), ushort?>(valuePickerFunc: x => x.G != Half.Zero ? NormalizedHalfToInt16(x.R) : null),
            PixelFormat.R32_Float => new HeightmapDataGenerator<float, ushort?>(valuePickerFunc: x => NormalizedFloatToInt16(x)),
            PixelFormat.R16_Float => new HeightmapDataGenerator<Half, ushort?>(valuePickerFunc: x => NormalizedHalfToInt16(x)),
            PixelFormat.R8G8B8A8_UNorm => new HeightmapDataGenerator<Color, ushort?>(valuePickerFunc: x => x.A > 0 ? ExpandByteToInt16(x.R) : null),
            PixelFormat.B8G8R8A8_UNorm => new HeightmapDataGenerator<Color, ushort?>(valuePickerFunc: x => x.A > 0 ? ExpandByteToInt16(x.R) : null),
            _ => throw new NotSupportedException($"PixelFormat '{format}' not supported.")
        };
    }
}
