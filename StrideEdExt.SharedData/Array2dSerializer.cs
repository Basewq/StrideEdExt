using Stride.Core.Serialization;

namespace StrideEdExt.SharedData;

// Code adapted from the following:
// Stride.Core.Serialization.Serializers.ArraySerializer<T>
[DataSerializerGlobal(typeof(Array2dSerializer<>), typeof(Array2d<>), DataSerializerGenericMode.GenericArguments)]
public class Array2dSerializer<T> : DataSerializer<Array2d<T>>, IDataSerializerGenericInstantiation
{
    private DataSerializer<T> _itemDataSerializer = default!;

    public override void Initialize(SerializerSelector serializerSelector)
    {
        _itemDataSerializer = MemberSerializer<T>.Create(serializerSelector);
    }

    public override void PreSerialize(ref Array2d<T> obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            stream.Write(obj.LengthX);
            stream.Write(obj.LengthY);
        }
        else
        {
            int lengthX = stream.ReadInt32();
            int lengthY = stream.ReadInt32();
            if (obj is not null)
            {
                obj.Clear();
                if (obj.LengthX != lengthX || obj.LengthY != lengthY)
                {
                    obj.Resize(lengthX, lengthY);
                }
            }
            else
            {
                obj = new Array2d<T>(lengthX, lengthY);
            }
        }
    }

    public override void Serialize(ref Array2d<T> obj, ArchiveMode mode, [Stride.Core.Annotations.NotNull] SerializationStream stream)
    {
        for (int y = 0; y < obj.LengthY; y++)
        {
            for (int x = 0; x < obj.LengthX; x++)
            {
                ref var item = ref obj[x, y];
                _itemDataSerializer.Serialize(ref item, mode, stream);
            }
        }
    }

    public void EnumerateGenericInstantiations(SerializerSelector serializerSelector, [Stride.Core.Annotations.NotNull] IList<Type> genericInstantiations)
    {
        genericInstantiations.Add(typeof(T));
    }
}
