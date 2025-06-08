using Stride.Core.Mathematics;
using Stride.Graphics;
using System;

namespace SceneEditorExtensionExample.Rendering.RenderTextures;

public class RenderTextureResult
{
    public RenderTextureResultStateType State;

    public Texture? Texture;
    /// <summary>
    /// The texture starting position (in pixel space) relative to its parent texture.
    /// </summary>
    public Int2 TexturePixelStartPosition;

    public Exception? ErrorException;
}

public enum RenderTextureResultStateType
{
    InProgress,
    Success,
    Failed
}
