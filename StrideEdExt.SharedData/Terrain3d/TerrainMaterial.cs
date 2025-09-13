using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d;

/**
 * This is the custom data as seen at run-time.
 */
[DataContract]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<TerrainMaterial>), Profile = "Content")]
[ContentSerializer(typeof(DataContentSerializer<TerrainMaterial>))]
public class TerrainMaterial
{
    public Texture? DiffuseMapTextureArray { get; set; }
    public Texture? NormalMapTextureArray { get; set; }
    public Texture? HeightBlendMapTextureArray { get; set; }

    public static Texture CreateMaterialIndexMapTexture(Array2d<byte> materialIndexMapData, GraphicsDevice graphicsDevice)
    {
        byte[] texData = materialIndexMapData.ToArray();
        var texture = Texture.New2D(
            graphicsDevice,
            width: materialIndexMapData.LengthX, height: materialIndexMapData.LengthY,
            PixelFormat.R8_UNorm,
            texData);
        return texture;
    }

    public static Texture CreateMaterialWeightMapTexture(Array2d<Half> materialWeightMapData, GraphicsDevice graphicsDevice)
    {
        Half[] texData = materialWeightMapData.ToArray();
        var texture = Texture.New2D(
            graphicsDevice,
            width: materialWeightMapData.LengthX, height: materialWeightMapData.LengthY,
            PixelFormat.R16_Float,
            texData);
        return texture;
    }
}
