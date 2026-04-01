using Stride.Core.Serialization;

namespace StrideEdExt.SharedData;

// Code adapted from the following:
// Stride.Core.Serialization.Serializers.PrimitiveTypeSerializers
[DataSerializerGlobal(typeof(DateTimeOffsetDataSerializer))]
public class DateTimeOffsetDataSerializer : DataSerializer<DateTimeOffset>
{
    /// <inheritdoc/>
    public override void Serialize(ref DateTimeOffset obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            stream.Write(obj.Ticks);
        }
        else if (mode == ArchiveMode.Deserialize)
        {
            obj = new DateTimeOffset(stream.ReadInt64(), TimeSpan.Zero);
        }
    }
}
