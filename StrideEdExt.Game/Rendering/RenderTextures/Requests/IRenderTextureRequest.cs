using Stride.Engine;
using Stride.Graphics;

namespace SceneEditorExtensionExample.Rendering.RenderTextures.Requests;

public interface IRenderTextureRequest
{
    void SetUpScene(SceneSystem sceneSystem, GraphicsDevice graphicsDevice);
    void RenderCompleted(SceneSystem sceneSystem, GraphicsDevice graphicsDevice, RenderTextureResult result);
}
