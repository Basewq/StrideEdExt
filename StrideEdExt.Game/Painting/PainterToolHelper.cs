using Stride.Core.Mathematics;
using Stride.Graphics;
using StrideEdExt.SharedData;

namespace StrideEdExt.Painting;

public static class PainterToolHelper
{
    public static Texture CreateNewRenderTarget(GraphicsDevice graphicsDevice, Size2 textureSize)
    {
        var texture = Texture.New2D(graphicsDevice, width: textureSize.Width, height: textureSize.Height,
            format: PixelFormat.R32_Float,   // Allow negative values
            textureFlags: TextureFlags.ShaderResource | TextureFlags.RenderTarget,
            arraySize: 1, usage: GraphicsResourceUsage.Default);
        return texture;
    }

    public static unsafe Array2d<float> RenderTargetToArray2dData(CommandList commandList, Texture texture)
    {
        using var image = texture.GetDataAsImage(commandList);

        int imgWidth = image.Description.Width;
        int imgHeight = image.Description.Height;

        var imageDataSpan = new Span<float>((void*)image.DataPointer, imgWidth * imgHeight);

        var arrayData = new Array2d<float>(imgWidth, imgHeight);
        for (int y = 0; y < imgHeight; y++)
        {
            for (int x = 0; x < imgWidth; x++)
            {
                int index1d = MathExt.ToIndex1d(x, y, imgWidth);
                arrayData[x, y] = imageDataSpan[index1d];
            }
        }
        return arrayData;
    }
}
