using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.Assets.Yaml;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.IO;
using StrideEdExt.StrideAssetExt.Assets;
using System.Reflection;

namespace StrideEdExt.StrideAssetExt.YamlSerializers;

internal class StrideCustomAssetSerializer : IAssetSerializer, IAssetSerializerFactory
{
    private static readonly object ValidFileExtensionsLock = new object();
    private static HashSet<string>? ValidFileExtensions;

    public static readonly StrideCustomAssetSerializer Default = new();

    public object Load(Stream stream, UFile? filePath, ILogger log, bool clearBrokenObjectReferences, out bool aliasOccurred, out AttachedYamlAssetMetadata yamlMetadata)
    {
        var defaultSerializer = AssetFileSerializer.Default;
        var assetObject = defaultSerializer.Load(stream, filePath, log, clearBrokenObjectReferences, out aliasOccurred, out yamlMetadata);
        // WARNING: cannot call OnAssetLoaded here because this specific object is discarded by
        // the editor and compiler, so we need to call OnAssetLoaded in the *AssetViewModel & *AssetCompiler
        return assetObject;
    }

    public void Save(Stream stream, object asset, AttachedYamlAssetMetadata yamlMetadata, ILogger? log = null)
    {
        lock (asset)
        {
            var defaultSerializer = AssetFileSerializer.Default;
            if (asset is IStrideCustomAsset customAsset)
            {
                customAsset.OnAssetSaving(log);
            }
            defaultSerializer.Save(stream, asset, yamlMetadata, log);
        }
    }

    public IAssetSerializer? TryCreate(string assetFileExtension)
    {
        var validFileExtensions = CreateOrGetValidFileExtensions();
        if (validFileExtensions.Contains(assetFileExtension))
        {
            return this;
        }
        return null;
    }

    private static HashSet<string> CreateOrGetValidFileExtensions()
    {
        lock (ValidFileExtensionsLock)
        {
            if (ValidFileExtensions is not null)
            {
                return ValidFileExtensions;
            }

            var validFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var assetAssemblies = AssetRegistry.AssetAssemblies;
            var assemblies = assetAssemblies.Where(x => CanIncludeAssembly(x.FullName!));

            var customAssetType = typeof(IStrideCustomAsset);
            foreach (var assembly in assemblies)
            {
                var allTypes = assembly.GetTypes();
                foreach (var type in allTypes)
                {
                    if (!type.GetInterfaces().Contains(customAssetType))
                    {
                        continue;
                    }
                    var assetDescAttr = type.GetCustomAttribute<AssetDescriptionAttribute>();
                    if (assetDescAttr?.FileExtensions is null)
                    {
                        continue;
                    }
                    var extensions = FileUtility.GetFileExtensions(assetDescAttr.FileExtensions);
                    validFileExtensions.AddRange(extensions);
                }
            }

            ValidFileExtensions = validFileExtensions;
            return ValidFileExtensions;
        }
    }

    private static bool CanIncludeAssembly(string assemblyName)
    {
        Span<string> includeAssemblyNames = [
            "StrideEdExt"
        ];
        foreach (string asmName in includeAssemblyNames)
        {
            if (assemblyName.StartsWith(asmName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
