using StrideEdExt.SharedData.Terrain3d;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

[DataContract]
public enum TerrainMaterialTextureSize
{
    [Display("8 x 8")]
    Size8 = 8,
    [Display("16 x 16")]
    Size16 = 16,
    [Display("32 x 32")]
    Size32 = 32,
    [Display("64 x 64")]
    Size64 = 64,
    [Display("128 x 128")]
    Size128 = 128,
    [Display("256 x 256")]
    Size256 = 256,
    [Display("512 x 512")]
    Size512 = 512,
    [Display("1024 x 1024")]
    Size1024 = 1024,
    [Display("2048 x 2048")]
    Size2048 = 2048,
    [Display("4096 x 4096")]
    Size4096 = 4096,
}

public static class TerrainMaterialTextureSizeExtensions
{
    public static Size2 ToSize2(this TerrainMaterialTextureSize textureSize)
    {
        var size = new Size2(width: (int)textureSize, height: (int)textureSize);
        return size;
    }
}

/**
 * The asset as seen by Game Studio.
 * Refer to Stride's source could for additional asset options, eg. referencing a file.
 */
[DataContract]
[AssetDescription(".gttmat")]
[AssetContentType(typeof(TerrainMaterial))]
//[CategoryOrder(1000, "Terrain")]
[AssetFormatVersion(StrideEdExtConfig.PackageName, CurrentVersion)]
//[AssetUpgrader(StrideEdExtConfig.PackageName, "0.0.0.1", "1.0.0.0", typeof(TerrainMaterialAssetUpgrader))]    // Can be used to update an old asset format to a new format.
[Display(10000, "Terrain Material")]
public class TerrainMaterialAsset : Asset
{
    private const string CurrentVersion = "0.0.0.1";

    /// <summary>
    /// Gets or sets a value indicating whether to generate mipmaps.
    /// </summary>
    /// <value><c>true</c> if mipmaps are generated; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Generate mipmaps for the textures.
    /// </remarks>
    [DataMember(order: 10)]
    public bool GenerateMipmaps { get; set; } = true;

    /// <summary>
    /// Compress the final textures to a format based on the target platform and usage.
    /// The final textures must be a multiple of 4
    /// </summary>
    [DataMember(order: 20)]
    [DefaultValue(true)]
    public bool IsCompressed { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to stream the textures.
    /// </summary>
    /// <remarks>
    /// Stream the textures dynamically at runtime.
    /// This improves performance and loading times.
    /// </remarks>
    [DataMember(order: 30)]
    public bool IsStreamable { get; set; } = true;

    [DataMember(order: 40)]
    public TerrainMaterialTextureSize DiffuseTextureSize { get; set; } = TerrainMaterialTextureSize.Size1024;
    [DataMember(order: 40)]
    public TerrainMaterialTextureSize NormalMapTextureSize { get; set; } = TerrainMaterialTextureSize.Size1024;
    [DataMember(order: 40)]
    public TerrainMaterialTextureSize HeightBlendMapTextureSize { get; set; } = TerrainMaterialTextureSize.Size1024;

    [DataMember(order: 1000)]
    [Display(Expand = ExpandRule.Always)]
    [MemberCollection(CanReorderItems = true, NotNullItems = true)]
    public List<TerrainMaterialLayerDefinitionAsset> MaterialLayers { get; set; } = [];
}
