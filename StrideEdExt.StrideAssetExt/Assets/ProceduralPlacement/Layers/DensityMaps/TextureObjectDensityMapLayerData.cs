using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using Half = System.Half;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.DensityMaps;

public class TextureObjectDensityMapLayerData : ObjectDensityMapLayerDataBase
{
    [Display(Browsable = false)]
    public DateTimeOffset? LastModifiedSourceFile { get; set; }

    public Vector2 ObjectDensityMapTextureScale { get; set; } = Vector2.One;

    [DataMemberIgnore]
    public Array2d<Half>? ObjectDensityMapData;

    protected override void OnSerializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, ILogger? logger)
    {
        if (ObjectDensityMapData is null)
        {
            logger?.Warning($"Could not serialize intermediate file because layer {LayerId} did not generate material data.");
            return;
        }

        string densityMapFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectDensityMapFileNameFormat);
        HeightmapSerializationHelper.SerializeHalfArray2dToHexFile(ObjectDensityMapData, densityMapFullFilePath);
        ObjectDensityMapRelativeFilePath = new UFile(densityMapFullFilePath).MakeRelative(objectPlacementMapAssetFullFolderPath);
    }

    protected override void OnDeserializeIntermediateFile(UDirectory intermediateFilesFullFolderPath, UDirectory objectPlacementMapAssetFullFolderPath, ObjectPlacementMapAsset objectPlacementMapAsset, Size2 terrainMapTextureSize, ILogger? logger)
    {
        if (ObjectDensityMapRelativeFilePath is null)
        {
            logger?.Info($"Intermediate file path for layer {LayerId} was not set.");
            return;
        }
        string densityMapFullFilePath = GetIntermediateFileFullFilePath(intermediateFilesFullFolderPath, IntermediateObjectDensityMapFileNameFormat);
        if (!File.Exists(densityMapFullFilePath))
        {
            logger?.Info($"Intermediate file for layer {LayerId} does not exist: {densityMapFullFilePath}");
            return;
        }
        if (HeightmapSerializationHelper.TryDeserializeHalfArray2dFromHexFile(densityMapFullFilePath, out var densityMapData, out var errorMessage))
        {
            ObjectDensityMapData = densityMapData;
            //EnsureCorrectMapSize(ObjectDensityMapData, terrainMapTextureSize);
        }
        else
        {
            logger?.Error(errorMessage);
        }
    }
}
