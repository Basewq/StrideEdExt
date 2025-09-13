using Stride.Core.Serialization;
using Half = System.Half;

namespace StrideEdExt.SharedData;

// Code adapted from the following:
// Stride.Core.Serialization.Serializers.PrimitiveTypeSerializers
[DataSerializerGlobal(typeof(HalfSerializer))]
public class HalfSerializer : DataSerializer<Half>
{
    /// <inheritdoc/>
    public override void Serialize(ref Half obj, ArchiveMode mode, SerializationStream stream)
    {
        stream.Serialize(ref obj);
    }
}
