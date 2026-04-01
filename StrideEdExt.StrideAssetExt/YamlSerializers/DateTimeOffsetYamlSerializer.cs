using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Reflection;
using Stride.Core.Yaml;
using Stride.Core.Yaml.Events;
using Stride.Core.Yaml.Serialization;

namespace StrideEdExt.StrideAssetExt.YamlSerializers;

// Code adapted from the following:
// Stride.Core.Yaml.GuidSerializer
/// <summary>
/// A Yaml serializer for <see cref="DateTimeOffset"/>
/// </summary>
[YamlSerializerFactory(YamlSerializerFactoryAttribute.Default)]
internal class DateTimeOffsetYamlSerializer : AssetScalarSerializerBase
{
    static DateTimeOffsetYamlSerializer()
    {
        TypeDescriptorFactory.Default.AttributeRegistry.Register(typeof(DateTimeOffset), new DataContractAttribute(aliasName: "DateTimeOffset"));
    }

    public override bool CanVisit(Type type)
    {
        return type == typeof(DateTimeOffset);
    }

    [NotNull]
    public override object ConvertFrom(ref ObjectContext context, [NotNull] Scalar fromScalar)
    {
        DateTimeOffset dto;
        _ = DateTimeOffset.TryParse(fromScalar.Value, out dto);
        return dto;
    }

    [NotNull]
    public override string ConvertTo(ref ObjectContext objectContext)
    {
        DateTime utcDateTime = ((DateTimeOffset)objectContext.Instance).UtcDateTime;    // Ensures the string output will have a 'Z' suffix
        string yamlValue = utcDateTime.ToString("O");  // ISO 8601
        return yamlValue;
    }
}
