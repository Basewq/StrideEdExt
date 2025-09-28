using Stride.Core.Serialization;
using StrideEdExt.SharedData.Terrain3d;

namespace StrideEdExt.SharedData;

// Code adapted from the following:
// Stride.Core.Serialization.Serializers.PrimitiveTypeSerializers
[DataSerializerGlobal(typeof(TerrainChunkIndex2dSerializer))]
public class TerrainChunkIndex2dSerializer : DataSerializer<TerrainChunkIndex2d>
{
    /// <inheritdoc/>
    public override void Serialize(ref TerrainChunkIndex2d obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            stream.Write(obj.X);
            stream.Write(obj.Z);
        }
        else if (mode == ArchiveMode.Deserialize)
        {
            int x = stream.ReadInt32();
            int z = stream.ReadInt32();
            obj = new TerrainChunkIndex2d(x, z);
        }
    }
}
