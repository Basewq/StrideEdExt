using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Half = System.Half;

namespace StrideEdExt.SharedData.Rendering;

public static class HeightmapSerializationHelper
{
    private const string NullTokenValue = "NULL";

    /// <summary>
    /// Serializes the normalized <paramref name="heightmapData"/> (in range [0...1]) to the file in <see cref="ushort"/> hex values.
    /// </summary>
    public static void SerializeFloatArray2dToHexFile(Array2d<float> heightmapData, string outputHeightmapFilePath)
    {
        using var writer = new StreamWriter(outputHeightmapFilePath);  // Will overwrite the file if it already exists
        // Write the array dimensions
        writer.Write(heightmapData.LengthX);
        writer.Write(' ');
        writer.Write(heightmapData.LengthY);
        writer.WriteLine();

        for (int y = 0; y < heightmapData.LengthY; y++)
        {
            for (int x = 0; x < heightmapData.LengthX; x++)
            {
                if (x > 0)
                {
                    writer.Write('\t');
                }
                float normalizedValue = heightmapData[x, y];
                ushort int16Value = HeightmapTextureHelper.NormalizedFloatToInt16(normalizedValue);
                string valueHex = int16Value.ToString("X4", provider: NumberFormatInfo.InvariantInfo);
                writer.Write(valueHex);
            }
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Deserializes a file with <see cref="ushort"/> hex values to <see cref="Array2d{T}"/> <see cref="float"/> normalized in range [0...1].
    /// </summary>
    public static bool TryDeserializeFloatArray2dFromHexFile(string heightmapFilePath, [NotNullWhen(true)] out Array2d<float>? heightmapData, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        using var reader = new StreamReader(heightmapFilePath);  // Will overwrite the file if it already exists
        // Read the array dimensions
        var arrayDimensions = reader.ReadLine();
        if (arrayDimensions is null)
        {
            heightmapData = null;
            errorMessage = "Array dimensions line missing.";
            return false;
        }
        var arrayDimValues = arrayDimensions.Split(' ');
        if (arrayDimValues.Length < 2
            || !int.TryParse(arrayDimValues[0], out int lengthX)
            || !int.TryParse(arrayDimValues[1], out int lengthY))
        {
            heightmapData = null;
            errorMessage = "Invalid array dimensions line.";
            return false;
        }

        heightmapData = new Array2d<float>(lengthX, lengthY);

        const int HexCharCount = 4;
        Span<char> valueHex = stackalloc char[HexCharCount];
        for (int y = 0; y < lengthY; y++)
        {
            for (int x = 0; x < lengthX; x++)
            {
                int charReadCount = reader.Read(valueHex);
                if (charReadCount != HexCharCount)
                {
                    heightmapData = null;
                    errorMessage = $"Failed to read value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                if (!ushort.TryParse(valueHex, NumberStyles.HexNumber, provider: NumberFormatInfo.InvariantInfo, out ushort int16Value))
                {
                    heightmapData = null;
                    errorMessage = $"Failed to parse value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                float normalizedValue = HeightmapTextureHelper.Int16ToNormalizedFloat(int16Value);
                heightmapData[x, y] = normalizedValue;
                if (x < lengthX - 1)
                {
                    int separatorChar = reader.Read();
                }
                else
                {
                    // Read \r\n or \n
                    int peekChar = reader.Peek();
                    if ((char)peekChar == '\r')
                    {
                        int rnChar = reader.Read();
                        int newLineChar = reader.Read();
                    }
                    else if ((char)peekChar == '\n')
                    {
                        int newLineChar = reader.Read();
                    }
                    else if (peekChar == -1)
                    {
                        // End of the file
                    }
                    else
                    {
                        heightmapData = null;
                        errorMessage = $"Unexpected character at line: {(y + 1)} - Char was '{(char)peekChar}' when new line was expected.";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Serializes the normalized <paramref name="heightmapData"/> (in range [0...1]) to the file in <see cref="byte"/> hex values.
    /// </summary>
    public static void SerializeHalfArray2dToHexFile(Array2d<Half> heightmapData, string outputHeightmapFilePath)
    {
        using var writer = new StreamWriter(outputHeightmapFilePath);  // Will overwrite the file if it already exists
        // Write the array dimensions
        writer.Write(heightmapData.LengthX);
        writer.Write(' ');
        writer.Write(heightmapData.LengthY);
        writer.WriteLine();

        for (int y = 0; y < heightmapData.LengthY; y++)
        {
            for (int x = 0; x < heightmapData.LengthX; x++)
            {
                if (x > 0)
                {
                    writer.Write('\t');
                }
                var normalizedValue = heightmapData[x, y];
                byte byteValue = HeightmapTextureHelper.NormalizedHalfToByte(normalizedValue);
                string valueHex = byteValue.ToString("X2", provider: NumberFormatInfo.InvariantInfo);
                writer.Write(valueHex);
            }
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Deserializes a file with <see cref="byte"/> hex values to <see cref="Array2d{T}"/> <see cref="byte"/> normalized in range [0...1].
    /// </summary>
    public static bool TryDeserializeHalfArray2dFromHexFile(string heightmapFilePath, [NotNullWhen(true)] out Array2d<Half>? heightmapData, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        using var reader = new StreamReader(heightmapFilePath);  // Will overwrite the file if it already exists
        // Read the array dimensions
        var arrayDimensions = reader.ReadLine();
        if (arrayDimensions is null)
        {
            heightmapData = null;
            errorMessage = "Array dimensions line missing.";
            return false;
        }
        var arrayDimValues = arrayDimensions.Split(' ');
        if (arrayDimValues.Length < 2
            || !int.TryParse(arrayDimValues[0], out int lengthX)
            || !int.TryParse(arrayDimValues[1], out int lengthY))
        {
            heightmapData = null;
            errorMessage = "Invalid array dimensions line.";
            return false;
        }

        heightmapData = new Array2d<Half>(lengthX, lengthY);

        const int HexCharCount = 2;
        Span<char> valueHex = stackalloc char[HexCharCount];
        for (int y = 0; y < lengthY; y++)
        {
            for (int x = 0; x < lengthX; x++)
            {
                int charReadCount = reader.Read(valueHex);
                if (charReadCount != HexCharCount)
                {
                    heightmapData = null;
                    errorMessage = $"Failed to read value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                if (!byte.TryParse(valueHex, NumberStyles.HexNumber, provider: NumberFormatInfo.InvariantInfo, out byte byteValue))
                {
                    heightmapData = null;
                    errorMessage = $"Failed to parse value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                Half normalizedValue = HeightmapTextureHelper.ByteToNormalizedHalf(byteValue);
                heightmapData[x, y] = normalizedValue;
                if (x < lengthX - 1)
                {
                    int separatorChar = reader.Read();
                }
                else
                {
                    // Read \r\n or \n
                    int peekChar = reader.Peek();
                    if ((char)peekChar == '\r')
                    {
                        int rnChar = reader.Read();
                        int newLineChar = reader.Read();
                    }
                    else if ((char)peekChar == '\n')
                    {
                        int newLineChar = reader.Read();
                    }
                    else if (peekChar == -1)
                    {
                        // End of the file
                    }
                    else
                    {
                        heightmapData = null;
                        errorMessage = $"Unexpected character at line: {(y + 1)} - Char was '{(char)peekChar}' when new line was expected.";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Serializes the <paramref name="heightmapData"/> to the file in <see cref="byte"/> hex values.
    /// </summary>
    public static void SerializeByteArray2dToHexFile(Array2d<byte> heightmapData, string outputHeightmapFilePath)
    {
        using var writer = new StreamWriter(outputHeightmapFilePath);  // Will overwrite the file if it already exists
        // Write the array dimensions
        writer.Write(heightmapData.LengthX);
        writer.Write(' ');
        writer.Write(heightmapData.LengthY);
        writer.WriteLine();

        for (int y = 0; y < heightmapData.LengthY; y++)
        {
            for (int x = 0; x < heightmapData.LengthX; x++)
            {
                if (x > 0)
                {
                    writer.Write('\t');
                }
                byte byteValue = heightmapData[x, y];
                string valueHex = byteValue.ToString("X2", provider: NumberFormatInfo.InvariantInfo);
                writer.Write(valueHex);
            }
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Deserializes a file with <see cref="byte"/> hex values to <see cref="Array2d{T}"/> <see cref="byte"/>.
    /// </summary>
    public static bool TryDeserializeByteArray2dFromHexFile(string heightmapFilePath, [NotNullWhen(true)] out Array2d<byte>? heightmapData, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        using var reader = new StreamReader(heightmapFilePath);  // Will overwrite the file if it already exists
        // Read the array dimensions
        var arrayDimensions = reader.ReadLine();
        if (arrayDimensions is null)
        {
            heightmapData = null;
            errorMessage = "Array dimensions line missing.";
            return false;
        }
        var arrayDimValues = arrayDimensions.Split(' ');
        if (arrayDimValues.Length < 2
            || !int.TryParse(arrayDimValues[0], out int lengthX)
            || !int.TryParse(arrayDimValues[1], out int lengthY))
        {
            heightmapData = null;
            errorMessage = "Invalid array dimensions line.";
            return false;
        }

        heightmapData = new Array2d<byte>(lengthX, lengthY);

        const int HexCharCount = 2;
        Span<char> valueHex = stackalloc char[HexCharCount];
        for (int y = 0; y < lengthY; y++)
        {
            for (int x = 0; x < lengthX; x++)
            {
                int charReadCount = reader.Read(valueHex);
                if (charReadCount != HexCharCount)
                {
                    heightmapData = null;
                    errorMessage = $"Failed to read value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                if (!byte.TryParse(valueHex, NumberStyles.HexNumber, provider: NumberFormatInfo.InvariantInfo, out byte byteValue))
                {
                    heightmapData = null;
                    errorMessage = $"Failed to parse value at line: {(y + 1)} - Value was {valueHex}";
                    return false;
                }
                heightmapData[x, y] = byteValue;
                if (x < lengthX - 1)
                {
                    int separatorChar = reader.Read();
                }
                else
                {
                    // Read \r\n or \n
                    int peekChar = reader.Peek();
                    if ((char)peekChar == '\r')
                    {
                        int rnChar = reader.Read();
                        int newLineChar = reader.Read();
                    }
                    else if ((char)peekChar == '\n')
                    {
                        int newLineChar = reader.Read();
                    }
                    else if (peekChar == -1)
                    {
                        // End of the file
                    }
                    else
                    {
                        heightmapData = null;
                        errorMessage = $"Unexpected character at line: {(y + 1)} - Char was '{(char)peekChar}' when new line was expected.";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public static void SerializeMaskableHalfArray2dToFile(Array2d<Half?> heightmapData, string outputHeightmapFilePath)
    {
        using var writer = new StreamWriter(outputHeightmapFilePath);  // Will overwrite the file if it already exists
        // Write the array dimensions
        writer.Write(heightmapData.LengthX);
        writer.Write(' ');
        writer.Write(heightmapData.LengthY);
        writer.WriteLine();

        for (int y = 0; y < heightmapData.LengthY; y++)
        {
            for (int x = 0; x < heightmapData.LengthX; x++)
            {
                if (x > 0)
                {
                    writer.Write('\t');
                }
                var value = heightmapData[x, y];
                if (value is Half halfValue)
                {
                    writer.Write(halfValue.ToString("G5"));    // G5 will guarantee the Half string will roundtrip
                }
                else
                {
                    writer.Write(NullTokenValue);
                }
            }
            writer.WriteLine();
        }
    }

    public static bool TryDeserializeMaskableHalfArray2dFromFile(string heightmapFilePath, [NotNullWhen(true)] out Array2d<Half?>? heightmapData, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        using var reader = new StreamReader(heightmapFilePath);  // Will overwrite the file if it already exists
        // Read the array dimensions
        var arrayDimensions = reader.ReadLine();
        if (arrayDimensions is null)
        {
            heightmapData = null;
            errorMessage = "Array dimensions line missing.";
            return false;
        }
        var arrayDimValues = arrayDimensions.Split(' ');
        if (arrayDimValues.Length < 2
            || !int.TryParse(arrayDimValues[0], out int lengthX)
            || !int.TryParse(arrayDimValues[1], out int lengthY))
        {
            heightmapData = null;
            errorMessage = "Invalid array dimensions line.";
            return false;
        }

        heightmapData = new Array2d<Half?>(lengthX, lengthY);

        for (int y = 0; y < lengthY; y++)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                heightmapData = null;
                errorMessage = $"Failed to read line: {(y + 1)} - Line is missing.";
                return false;
            }
            var lineValues = line.Split('\t');
            if (lineValues.Length < lengthX)
            {
                heightmapData = null;
                errorMessage = $"Failed to read line: {(y + 1)} - Expected {lengthX} values, but only encounted {lineValues.Length}";
                return false;
            }
            for (int x = 0; x < lengthX; x++)
            {
                var valueString = lineValues[x];
                if (valueString == NullTokenValue)
                {
                    // Null
                }
                else
                {
                    if (!Half.TryParse(valueString, provider: NumberFormatInfo.InvariantInfo, out Half halfValue))
                    {
                        heightmapData = null;
                        errorMessage = $"Failed to parse value at line: {(y + 1)} - Value was {valueString}";
                        return false;
                    }
                    heightmapData[x, y] = halfValue;
                }
            }
        }

        return true;
    }
}
