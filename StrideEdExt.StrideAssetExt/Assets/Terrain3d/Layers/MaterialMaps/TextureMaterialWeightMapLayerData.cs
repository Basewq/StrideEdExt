using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d.Layers;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.MaterialMaps;

public class TextureMaterialWeightMapLayerData : TerrainMaterialMapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTimeOffset? LastModifiedSourceFile { get; set; }

    [DataMemberIgnore]
    public Array2d<Half>? MaterialWeightMapData;

    protected override void OnSerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger)
    {
        if (MaterialWeightMapData is null)
        {
            logger?.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate material data.");
            return;
        }

        string materialWeightMapFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateMaterialWeightMapFileNameFormat);
        HeightmapSerializationHelper.SerializeHalfArray2dToHexFile(MaterialWeightMapData, materialWeightMapFullFilePath);
        MaterialWeightMapRelativeFilePath = new UFile(materialWeightMapFullFilePath).MakeRelative(terrainMapAssetFullFolderPath);
    }

    protected override void OnDeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory terrainMapAssetFullFolderPath, TerrainMapAsset terrainMapAsset, ILogger? logger)
    {
        if (MaterialWeightMapRelativeFilePath is null)
        {
            logger?.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        string materialWeightMapFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateMaterialWeightMapFileNameFormat);
        if (!File.Exists(materialWeightMapFullFilePath))
        {
            logger?.Info($"Intermediate file for layer {LayerId} does not exist: {materialWeightMapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeHalfArray2dFromHexFile(materialWeightMapFullFilePath, out var materialWeightMapData, out var errorMessage))
        {
            MaterialWeightMapData = materialWeightMapData;
            EnsureCorrectMapSize(MaterialWeightMapData, terrainMapAsset.HeightmapTextureSize.ToSize2());
        }
        else
        {
            logger?.Error(errorMessage);
        }
    }

    public override void ApplyLayerMaterialMapModifications(Array2d<Half> materialWeightMapData, Array2d<byte> materialIndexMapData, List<TerrainMaterialLayerDefinitionAsset> materialLayers)
    {
        if (MaterialWeightMapData is not Array2d<Half> layerMaterialWeightMapData)
        {
            return;
        }
        if (!materialLayers.TryGetMaterialIndex(MaterialName, out byte materialIndex))
        {
            return;
        }

        var startingIndex = MaterialWeightMapTexturePixelStartPosition;
        TerrainMapLayerExtensions.UpdateMaterialWeightMapRegion(
            layerMaterialWeightMapData, materialIndex, startingIndex,
            materialWeightMapData, materialIndexMapData);
    }
}
