using Stride.Core;
using Stride.Graphics;

namespace StrideEdExt.StrideAssetExt.Assets.Terrain3d;

[DataContract]
public class TerrainMaterialLayerDefinitionAsset
{
    public string? MaterialName { get; set; }
    public Texture? DiffuseMap { get; set; }
    public Texture? NormalMap { get; set; }
    /// <summary>
    /// Use <c>true</c> for DirectX textures (ie. green down textures), <c>false</c> for OpenGL textures (ie. green up textures).
    /// </summary>
    public bool NormalMapInvertY { get; set; } = true;
    public Texture? HeightBlendMap { get; set; }
}
