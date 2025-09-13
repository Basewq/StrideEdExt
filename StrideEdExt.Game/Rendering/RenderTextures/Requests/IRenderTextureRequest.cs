using Stride.Engine;
using Stride.Graphics;

namespace StrideEdExt.Rendering.RenderTextures.Requests;

public interface IRenderTextureRequest<TRenderTextureResult> where TRenderTextureResult : RenderTextureResult
{
    void SetUpScene(SceneSystem sceneSystem, GraphicsDevice graphicsDevice);
    TRenderTextureResult RenderCompleted(SceneSystem sceneSystem, GraphicsDevice graphicsDevice);
}
