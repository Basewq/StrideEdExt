using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;

namespace StrideEdExt.Rendering.Materials;

[DataContract]
[Display("Terrain Edit Preview Heightmap")]
public class MaterialTerrainEditPreviewHeightmapFeature : MaterialFeature, IMaterialDisplacementFeature
{
    public Texture? TerrainHeightmap { get; internal set; }
    public Vector2 TerrainHeightRange { get; internal set; }
    public float MaxAdjustmentHeightValue { get; set; }
    public HeightmapPaintModeType PaintModeType { get; set; }

    public override void GenerateShader(MaterialGeneratorContext context)
    {
        context.Parameters.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmap, TerrainHeightmap);
        var heightmapSize = Vector2.One;
        if (TerrainHeightmap is not null)
        {
            heightmapSize = new(TerrainHeightmap.Width, TerrainHeightmap.Height);
        }
        context.Parameters.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmapSize, heightmapSize);
        context.Parameters.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightRange, TerrainHeightRange);
        context.Parameters.Set(MaterialTerrainEditPreviewHeightmapKeys.MaxAdjustmentHeightValue, MaxAdjustmentHeightValue);
        context.Parameters.Set(MaterialTerrainEditPreviewHeightmapKeys.HeightmapPaintModeType, (uint)PaintModeType);

        var mixin = new ShaderMixinSource();
        mixin.Mixins.Add(new ShaderClassSource("MaterialTerrainEditPreviewHeightmap"));

        context.AddShaderSource(MaterialShaderStage.Vertex, mixin);
    }
}
