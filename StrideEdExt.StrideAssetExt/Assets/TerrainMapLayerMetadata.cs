using SceneEditorExtensionExample.SharedData.Rendering;
using SceneEditorExtensionExample.StrideAssetExt.Assets;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Half = System.Half;

namespace SceneEditorExtensionExample.SharedData;

[DataContract(Inherited = true)]
public abstract class TerrainMapLayerMetadata
{
    public required Guid LayerId { get; set; }
    public DateTime? LastModifiedIntermediateFile { get; set; }

    [DataMemberIgnore]
    public bool IsSerializationRequired { get; set; }

    public abstract void SerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger);

    public abstract void DeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger);
}

public class ModelHeightmapLayerMetadata : TerrainMapLayerMetadata
{
    private const string IntermediateHeightmapFileNameFormat = "layer_heightmap_{0}.txt";

    public DateTime? LastModifiedSourceFile { get; set; }
    public string? ModelAssetUrl { get; set; }

    [UPath(UPathRelativeTo.Package)]
    public UFile? HeightmapFilePath { get; set; }

    public Int2? HeightmapTexturePixelStartPosition;
    [DataMemberIgnore]
    public Array2d<Half?>? HeightmapData { get; set; }

    public override void SerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        string fileName = string.Format(IntermediateHeightmapFileNameFormat, LayerId.ToString("N"));
        string heightmapFullFilePath = Path.Combine(packageFolderPath, fileName);

        HeightmapSerializationHelper.SerializeMaskableHalfArray2dToFile(HeightmapData, heightmapFullFilePath);
        HeightmapFilePath =  heightmapFullFilePath;
    }

    public override void DeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var heightmapFullFilePath = UPath.Combine(packageFolderPath, HeightmapFilePath).ToOSPath();
        if (!File.Exists(heightmapFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {heightmapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeMaskableHalfArray2dFromFile(heightmapFullFilePath, out var heightmapData, out var errorMessage))
        {
            HeightmapData = heightmapData;
        }
        else
        {
            logger.Error(errorMessage);
        }
    }
}

public class TextureHeightmapLayerMetadata : TerrainMapLayerMetadata
{
    private const string IntermediateHeightmapFileNameFormat = "layer_heightmap_{0}.txt";

    public DateTime? LastModifiedSourceFile { get; set; }

    [UPath(UPathRelativeTo.Package)]
    public UFile? HeightmapFilePath { get; set; }

    public Int2? HeightmapTexturePixelStartPosition;
    [DataMemberIgnore]
    public Array2d<float>? HeightmapData { get; set; }

    public override void SerializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapData is null)
        {
            logger.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate heightmap data.");
            return;
        }

        string fileName = string.Format(IntermediateHeightmapFileNameFormat, LayerId.ToString("N"));
        string heightmapFullFilePath = Path.Combine(packageFolderPath, fileName);

        HeightmapSerializationHelper.SerializeFloatArray2dToFile(HeightmapData, heightmapFullFilePath);
        HeightmapFilePath = heightmapFullFilePath;
    }

    public override void DeserializeIntermediateFile(UDirectory packageFolderPath, TerrainMapAsset terrainMapAsset, ILogger logger)
    {
        if (HeightmapFilePath is null)
        {
            logger.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        var heightmapFullFilePath = UPath.Combine(packageFolderPath, HeightmapFilePath).ToOSPath();
        if (!File.Exists(heightmapFullFilePath))
        {
            logger.Info($"Intermediate file for layer {LayerId} does not exist: {heightmapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeFloatArray2dFromFile(heightmapFullFilePath, out var heightmapData, out var errorMessage))
        {
            HeightmapData = heightmapData;
        }
        else
        {
            logger.Error(errorMessage);
        }
    }
}
