using SceneEditorExtensionExample.Rendering.RenderTextures.Requests;
using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using System.Collections.Generic;

namespace SceneEditorExtensionExample.Rendering.RenderTextures;

// Adapted from Stride.Editor.Thumbnails.ThumbnailGenerator
public class RenderTextureJobSystem : GameSystem
{
    private readonly SceneSystem _renderTextureSceneSystem;

    /// <summary>
    /// The main scene used to render textures.
    /// </summary>
    private Scene _localScene;
    private readonly Queue<PendingRequestRenderTexture> _requestQueue = [];

    private PendingRequestRenderTexture? _activeRequest;

    private readonly GameTime _nullGameTime = new();

    public RenderTextureJobSystem(IServiceRegistry registry) : base(registry)
    {
        Enabled = false;    // Update call not needed
        Visible = true;     // Allows Draw method to be called
        UpdateOrder = -1000000;
        DrawOrder = -1000000;

        Services.AddService(this);

        _localScene = new Scene()
        {
            Name = "RenderTextureRootScene"
        };
        _renderTextureSceneSystem = new SceneSystem(Services)
        {
            Name = "RenderTextureSceneSystem",
            SceneInstance = new SceneInstance(Services, _localScene, ExecutionMode.Runtime),
            Enabled = false,    // This class will manually call Update/Draw on this system
            Visible = false,
        };
        Game.GameSystems.Add(_renderTextureSceneSystem);
    }

    protected override void Destroy()
    {
        base.Destroy();
        Game.GameSystems.Remove(_renderTextureSceneSystem);
        _renderTextureSceneSystem.Dispose();
    }

    public RenderTextureResult EnqueueRequest(IRenderTextureRequest request)
    {
        lock (_requestQueue)
        {
            var result = new RenderTextureResult
            {
                State = RenderTextureResultStateType.InProgress
            };
            var pendingRequest = new PendingRequestRenderTexture(request, result);
            _requestQueue.Enqueue(pendingRequest);
            return result;
        }
    }

    private bool TryEnqueueNextJob()
    {
        PendingRequestRenderTexture? pendingRequest = null;
        lock (_requestQueue)
        {
            if (!_requestQueue.TryDequeue(out pendingRequest))
            {
                return false;
            }
        }

        _activeRequest = pendingRequest;
        pendingRequest.Request.SetUpScene(_renderTextureSceneSystem, GraphicsDevice);
        return true;
    }

    public override void Draw(GameTime gameTime)
    {
        if (_activeRequest is null)
        {
            const int MaxProcessJobCount = 10;
            for (int i = 0; i < MaxProcessJobCount; i++)
            {
                if (!TryEnqueueNextJob())
                {
                    break;
                }
                _renderTextureSceneSystem.Update(_nullGameTime);
                _renderTextureSceneSystem.BeginDraw();
                _renderTextureSceneSystem.Draw(_nullGameTime);
                _renderTextureSceneSystem.EndDraw();
                _activeRequest!.Request.RenderCompleted(_renderTextureSceneSystem, GraphicsDevice, _activeRequest.Result);
                _activeRequest = null;
            }
        }
    }

    private record PendingRequestRenderTexture(IRenderTextureRequest Request, RenderTextureResult Result);
}
