using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using StrideEdExt.Rendering.RenderTextures.Requests;

namespace StrideEdExt.Rendering.RenderTextures;

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

    //public RenderTextureResult EnqueueRequest(IRenderTextureRequest request)
    //{
    //    lock (_requestQueue)
    //    {
    //        var result = new RenderTextureResult
    //        {
    //            State = RenderTextureResultStateType.InProgress
    //        };
    //        var pendingRequest = new PendingRequestRenderTexture(request, result);
    //        _requestQueue.Enqueue(pendingRequest);
    //        return result;
    //    }
    //}

    public Task<TRenderTextureResult> EnqueueRequest<TRenderTextureResult>(IRenderTextureRequest<TRenderTextureResult> request)
        where TRenderTextureResult : RenderTextureResult
    {
        lock (_requestQueue)
        {
            var tcs = new TaskCompletionSource<TRenderTextureResult>();
            Action<SceneSystem, GraphicsDevice> onCompletion = (sceneSystem, graphicsDevice) =>
            {
                var result = request.RenderCompleted(sceneSystem, graphicsDevice);
                tcs.SetResult(result);
            };
            var pendingRequest = new PendingRequestRenderTexture(request.SetUpScene, onCompletion);
            _requestQueue.Enqueue(pendingRequest);
            return tcs.Task;
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
        pendingRequest.SetUpSceneAction(_renderTextureSceneSystem, GraphicsDevice);
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
                _activeRequest!.RenderCompletedAction(_renderTextureSceneSystem, GraphicsDevice);
                _activeRequest = null;
            }
        }
    }

    private record PendingRequestRenderTexture
    {
        public PendingRequestRenderTexture(Action<SceneSystem, GraphicsDevice> setUpSceneAction, Action<SceneSystem, GraphicsDevice> renderCompletedAction)
        {
            SetUpSceneAction = setUpSceneAction;
            RenderCompletedAction = renderCompletedAction;
        }

        public Action<SceneSystem, GraphicsDevice> SetUpSceneAction { get; }
        public Action<SceneSystem, GraphicsDevice> RenderCompletedAction { get; }
    }
}
