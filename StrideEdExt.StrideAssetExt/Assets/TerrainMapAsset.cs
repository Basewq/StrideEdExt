using SceneEditorExtensionExample.SharedData;
using SceneEditorExtensionExample.SharedData.Rendering;
using SceneEditorExtensionExample.SharedData.Terrain3d;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample.StrideAssetExt.Assets;

/**
 * The asset as seen by Game Studio.
 * Refer to Stride's source could for additional asset options, eg. referencing a file.
 */
[DataContract]
[AssetDescription(".gttrr")]
[AssetContentType(typeof(TerrainMap))]
//[CategoryOrder(1000, "Terrain")]
[AssetFormatVersion(SceneEditorExtensionExampleConfig.PackageName, CurrentVersion)]
//[AssetUpgrader(SceneEditorExtensionExampleConfig.PackageName, "0.0.0.1", "1.0.0.0", typeof(TerrainMapAssetUpgrader))]    // Can be used to update an old asset format to a new format.
[Display(10000, "Terrain Map 3D")]
public class TerrainMapAsset : Asset
{
    private const string CurrentVersion = "0.0.0.1";

    private const string IntermediateHeightmapFileName = "terrain_heightmap.txt";

    private readonly object _serializationLock = new();

    public static string LayerMetadataListName => nameof(LayerMetadataList);    // HACK: we need the editor to detect changes to list in the editor, but we don't want to expose this list directly.

    [Display(Browsable = false)]
    [DataMember]
    internal List<TerrainMapLayerMetadata> LayerMetadataList = [];

    [DataMemberIgnore]
    public bool HasLayerMetadataListChanged { get; set; }

    /// <remarks>
    /// Folder path should be unique for each <see cref="TerrainMapAsset"/> due to hardcoded file names for generated intermediate files.
    /// </remarks>
    [UPath(UPathRelativeTo.Package)]
    public UDirectory? ResourceFolderPath { get; set; }

    /// <summary>
    /// Normalized heightmap data.
    /// </summary>
    [DataMemberIgnore]
    public Array2d<float>? HeightmapData { get; set; }

    /// <inheritdoc cref="TerrainMap.MapSize"/>
    [DataMemberRange(minimum: 1, maximum: 4095, smallStep: 8, largeStep: 32, decimalPlaces: 0)]
    public Int2 MapSize { get; set; } = new Int2(63, 63);

    /// <inheritdoc cref="TerrainMap.QuadPerMesh"/>
    [DataMemberRange(minimum: 1, maximum: 4095, smallStep: 2, largeStep: 8, decimalPlaces: 0)]
    public Int2 QuadPerMesh { get; set; } = new Int2(16, 16);

    /// <summary>
    /// The size of a single terrain mesh quad.
    /// </summary>
    public Vector2 MeshQuadSize { get; set; } = Vector2.One;

    public TerrainMeshPerChunk MeshPerChunk { get; set; } = TerrainMeshPerChunk.Count4x4;

    public Vector2 HeightRange { get; set; } = new(-100, 100);

    public bool TryGetLayerMetadata<TLayerMetadata>(Guid layerId, [NotNullWhen(true)] out TLayerMetadata? layerMetadata)
        where TLayerMetadata : TerrainMapLayerMetadata
    {
        layerMetadata = LayerMetadataList.Find(x => x.LayerId == layerId) as TLayerMetadata;
        return layerMetadata is not null;
    }

    public void RegisterLayerMetadata<TLayerMetadata>(TLayerMetadata layerMetadata)
        where TLayerMetadata : TerrainMapLayerMetadata
    {
        LayerMetadataList.RemoveAll(x => x.LayerId == layerMetadata.LayerId);
        LayerMetadataList.Add(layerMetadata);
        HasLayerMetadataListChanged = true;
    }

    public void UnregisterLayerMetadata(Guid layerId)
    {
        int removedCount = LayerMetadataList.RemoveAll(x => x.LayerId == layerId);
        HasLayerMetadataListChanged = HasLayerMetadataListChanged || removedCount > 0;
    }

    public IEnumerable<TerrainMapLayerMetadata> GetAllLayerMetadata()
    {
        return LayerMetadataList;
    }

    private bool _hasCalledFinalizeContentDeserialization = false;
    public void EnsureFinalizeContentDeserialization(ILogger logger, UDirectory packageFolderPath)
    {
        lock (_serializationLock)
        {
            if (_hasCalledFinalizeContentDeserialization)
            {
                return;
            }
            
            if (ResourceFolderPath is not null )
            {
                var resourceFolderPath = UPath.Combine(packageFolderPath, ResourceFolderPath)?.ToOSPath();
                if (resourceFolderPath is not null)
                {
                    Directory.CreateDirectory(resourceFolderPath);
                    if (HeightmapData is null)
                    {
                        string heightmapFilePath = Path.Combine(resourceFolderPath, IntermediateHeightmapFileName);
                        logger.Info($"Deserializing heightmap: {heightmapFilePath}");
                        if (HeightmapSerializationHelper.TryDeserializeFloatArray2dFromFile(heightmapFilePath, out var heightmapData, out var errorMessage))
                        {
                            var expectedDataSize = MapSize + Int2.One;
                            if (heightmapData.Length2d != expectedDataSize)
                            {
                                logger.Warning($"Deserialized heightmap size mismatch with asset map size: RawFile: {heightmapData.Length2d} - Asset: {expectedDataSize}. Resizing the array.");
                                heightmapData.Resize(expectedDataSize.X, expectedDataSize.Y);
                            }
                            HeightmapData = heightmapData;
                            logger.Info($"Deserialized heightmap size: {HeightmapData.Length2d}");
                        }
                        else
                        {
                            logger.Error($"Failed to deserialize intermediate heightmap file: {heightmapFilePath}\r\n{errorMessage}");
                        }
                    }

                    foreach (var layerMetadata in LayerMetadataList)
                    {
                        layerMetadata.DeserializeIntermediateFile(resourceFolderPath, this, logger);
                    }
                }
            }

            _hasCalledFinalizeContentDeserialization = true;
        }
    }

    public void PrepareContentSerialization()
    {
        lock (_serializationLock)
        {
        }
    }

    public void SerializeIntermediateFiles(ILogger logger, UDirectory packageFolderPath)
    {
        lock (_serializationLock)
        {
            if (ResourceFolderPath is not null)
            {
                var resourceFolderFullPath = UPath.Combine(packageFolderPath, ResourceFolderPath)?.ToOSPath();
                if (resourceFolderFullPath is not null)
                {
                    //var assetDirPath = assetFileFullPath.GetFullDirectory();                
                    //ResourceFolderPath.MakeRelative(assetDirPath)
                    Directory.CreateDirectory(resourceFolderFullPath);
                    if (HeightmapData is not null)
                    {
                        string heightmapFilePath = Path.Combine(resourceFolderFullPath, IntermediateHeightmapFileName);
                        HeightmapSerializationHelper.SerializeFloatArray2dToFile(HeightmapData, heightmapFilePath);
                    }

                    foreach (var layerMetadata in LayerMetadataList)
                    {
                        if (layerMetadata.IsSerializationRequired)
                        {
                            layerMetadata.SerializeIntermediateFile(resourceFolderFullPath, this, logger);
                            layerMetadata.LastModifiedIntermediateFile = DateTime.UtcNow;
                            layerMetadata.IsSerializationRequired = false;
                            HasLayerMetadataListChanged = true;
                        }
                    }
                }
            }
        }
    }

    public void TerrainPropertiesCopyTo(TerrainMap terrainMap) => TerrainPropertiesCopyToInternal(terrainMap, reuseHeightmapData: false);
    
    private void TerrainPropertiesCopyToInternal(TerrainMap terrainMap, bool reuseHeightmapData)
    {
        terrainMap.MapSize = MapSize;
        terrainMap.QuadPerMesh = QuadPerMesh;
        terrainMap.MeshQuadSize = MeshQuadSize;
        terrainMap.MeshPerChunk = MeshPerChunk;
        terrainMap.HeightRange = HeightRange;

        if (reuseHeightmapData)
        {
            terrainMap.HeightmapData = HeightmapData;
        }
        else if (HeightmapData is not null)
        {
            if (terrainMap.HeightmapData is { } destHeightmapData)
            {
                if (destHeightmapData.Length2d != HeightmapData.Length2d)
                {
                    destHeightmapData.Resize(HeightmapData.Length2d);
                }
            }
            else
            {
                destHeightmapData = new(MapSize + Int2.One);   // Map size is quad count, so +1 to get vertices count
                terrainMap.HeightmapData = destHeightmapData;
            }
            // Copy the data over
            for (int y = 0; y < HeightmapData.LengthY; y++)
            {
                for (int x = 0; x < HeightmapData.LengthX; x++)
                {
                    destHeightmapData[x, y] = HeightmapData[x, y];
                }
            }
        }
    }

    public TerrainMap ToTerrain(ILogger? logger = null)
    {
        lock (_serializationLock)
        {
            var terrainMap = new TerrainMap();
            TerrainPropertiesCopyToInternal(terrainMap, reuseHeightmapData: true);
            return terrainMap;
        }
    }
}
