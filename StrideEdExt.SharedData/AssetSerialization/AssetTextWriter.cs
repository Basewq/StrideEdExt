using Stride.Core.Mathematics;
using System.Globalization;

namespace StrideEdExt.SharedData.AssetSerialization;

// HACK: StreamWriter always uses CultureInfo.CurrentCulture so this class bypasses this issue.
// Used for serializing things like numbers in a machine-independent way.
public class AssetTextWriter : StreamWriter
{
    public AssetTextWriter(string filePath)
        : base(filePath)
    {
    }

    public override IFormatProvider FormatProvider => CultureInfo.InvariantCulture;

    public void WriteSpace()
    {
        Write(' ');
    }

    public void WriteTab()
    {
        Write('\t');
    }

    public void WriteTabDelimited(Vector2 vec2)
    {
        Write(MathExt.EnsurePositiveZero(vec2.X));
        Write('\t');
        Write(MathExt.EnsurePositiveZero(vec2.Y));
    }

    public void WriteTabDelimited(Vector3 vec3)
    {
        Write(MathExt.EnsurePositiveZero(vec3.X));
        Write('\t');
        Write(MathExt.EnsurePositiveZero(vec3.Y));
        Write('\t');
        Write(MathExt.EnsurePositiveZero(vec3.Z));
    }
}
