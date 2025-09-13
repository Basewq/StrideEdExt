using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using StrideEdExt.Painting.Brushes;
using StrideEdExt.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.Painting;

class PainterSystem : GameSystem, IPainterService
{
    private readonly Dictionary<PaintSessionId, PaintSessionData> _sessionIdToSessionData = [];
    private PaintSessionId _activePaintSessionId = default;

    public PainterSystem(IServiceRegistry registry) : base(registry)
    {
        Enabled = true;
        UpdateOrder = -1000000;
        DrawOrder = -1000000;

        Services.AddService<IPainterService>(this);
    }

    private static byte _nextSessionIdValue = 1;
    private static byte GetNextSessionIdValue()
    {
        byte idValue = _nextSessionIdValue;
        _nextSessionIdValue = unchecked((byte)(_nextSessionIdValue + 1));
        if (_nextSessionIdValue == 0)
        {
            _nextSessionIdValue = 1;    // Don't have zero as a valid ID
        }
        return idValue;
    }

    private PaintSessionId GetNextSessionId()
    {
        if (_sessionIdToSessionData.Count >= byte.MaxValue - 1)
        {
            throw new InvalidOperationException($"Cannot have more than {byte.MaxValue - 1} active sessions.");
        }
        var sessionId = new PaintSessionId(GetNextSessionIdValue());
        while (_sessionIdToSessionData.ContainsKey(sessionId))
        {
            sessionId = new PaintSessionId(GetNextSessionIdValue());
        }
        return sessionId;
    }

    public PaintSessionId BeginSession(Guid editorEntityId)
    {
        var editorEntity = Game.SceneSystem.SceneInstance.FirstOrDefault(x => x.Id == editorEntityId);
        if (editorEntity is null)
        {
            throw new ArgumentException($"Editor Entity ID not found: {editorEntityId}");
        }

        var editorScene = editorEntity.Scene;
        var painterEntity = new Entity(name: "Painter_" + editorEntityId);

        var sessionId = GetNextSessionId();

        var modelComp = new ModelComponent
        {
        };
        painterEntity.Add(modelComp);
        var paintBrushComp = new PaintBrushComponent
        {
            PaintSessionId = sessionId,
            PainterService = this,
        };
        painterEntity.Add(paintBrushComp);
        painterEntity.Scene = editorScene;

        var sessionData = new PaintSessionData(painterEntity, paintBrushComp, modelComp);
        _sessionIdToSessionData.Add(sessionId, sessionData);

        return sessionId;
    }

    public void EndSession(PaintSessionId paintSessionId)
    {
        if (_sessionIdToSessionData.Remove(paintSessionId, out var sessionData))
        {
            if (sessionData.PaintBrushComponent.SelectedPainterTool is not null)
            {
                sessionData.PaintBrushComponent.SelectedPainterTool.Deactivate();
                sessionData.PaintBrushComponent.SelectedPainterTool = null;
            }
            sessionData.PainterEntity.Scene = null;

            if (_activePaintSessionId == paintSessionId)
            {
                _activePaintSessionId = default;
            }
        }
        else
        {
            throw new InvalidOperationException($"Session key is not registered: {paintSessionId}");
        }
    }

    public bool TryGetActiveSessionId(out PaintSessionId paintSessionId)
    {
        paintSessionId = _activePaintSessionId;
        if (paintSessionId != default)
        {
            return true;
        }
        return false;
    }

    public void SetActiveSessionId(PaintSessionId paintSessionId)
    {
        _activePaintSessionId = paintSessionId;
    }

    public bool TryGetActiveTool(PaintSessionId paintSessionId, [NotNullWhen(true)] out IPainterTool? painterTool)
    {
        if (_sessionIdToSessionData.TryGetValue(paintSessionId, out var sessionData))
        {
            painterTool = sessionData.PaintBrushComponent.SelectedPainterTool;
            return painterTool is not null;
        }
        painterTool = null;
        return false;
    }

    public void SetActiveTool(PaintSessionId paintSessionId, IPainterTool painterTool)
    {
        if (_sessionIdToSessionData.TryGetValue(paintSessionId, out var sessionData))
        {
            if (sessionData.PaintBrushComponent.SelectedPainterTool is not null)
            {
                sessionData.PaintBrushComponent.SelectedPainterTool.Deactivate();
            }
            painterTool.Activate();
            var cursorModelComp = sessionData.PaintBrushCursorModelComponent;
            painterTool.SetCursorPreviewModel(cursorModelComp);
            sessionData.PaintBrushComponent.SelectedPainterTool = painterTool;
        }
    }

    public PaintBrushstrokeHandle BeginBrushstroke(PaintSessionId paintSessionId, Vector2 brushScreenPositionNormalized)
    {
        var brushstrokeHandle = new PaintBrushstrokeHandle(this, paintSessionId);
        if (_sessionIdToSessionData.TryGetValue(paintSessionId, out var sessionData))
        {
            AddPendingBrushstrokePoint(brushScreenPositionNormalized, sessionData);
        }
        else
        {
            throw new InvalidOperationException($"Session not registered: {paintSessionId}");
        }
        return brushstrokeHandle;
    }

    public void AddBrushstrokePoint(PaintBrushstrokeHandle brushstrokeHandle, Vector2 brushScreenPositionNormalized)
    {
        // Enqueue stroke data
        var paintSessionId = brushstrokeHandle.PaintSessionId;
        if (_sessionIdToSessionData.TryGetValue(paintSessionId, out var sessionData))
        {
            AddPendingBrushstrokePoint(brushScreenPositionNormalized, sessionData);
        }
        else
        {
            throw new InvalidOperationException($"Session not registered: {paintSessionId}");
        }
    }

    private static void AddPendingBrushstrokePoint(Vector2 brushScreenPositionNormalized, PaintSessionData sessionData)
    {
        sessionData.PendingScreenPositionsNormalized.Add(brushScreenPositionNormalized);
    }

    public void EndBrushstroke(PaintBrushstrokeHandle brushstrokeHandle)
    {
        var paintSessionId = brushstrokeHandle.PaintSessionId;
        if (_sessionIdToSessionData.TryGetValue(paintSessionId, out var sessionData))
        {
            sessionData.IsFinalBrushstroke = true;      // Will do callback in the next update
        }
        else
        {
            throw new InvalidOperationException($"Session not registered: {paintSessionId}");
        }
    }

    private IPaintRendererService? _paintRendererService;
    public override void Update(GameTime gameTime)
    {
        if (_activePaintSessionId == default
            || !_sessionIdToSessionData.TryGetValue(_activePaintSessionId, out var sessionData)
            || sessionData.PaintBrushComponent.SelectedPainterTool is not IPainterTool painterTool)
        {
            return;
        }

        _paintRendererService ??= Services.GetService<IPaintRendererService>();
        if (_paintRendererService is null)
        {
            return;
        }

        var brushSettings = painterTool.BrushSettings;

        int prevTotalBrushPointCount = sessionData.StrokeMapBrushPoints.Count;
        List<BrushPoint>? brushPoints = null;
        var screenPositionsNormalized = sessionData.PendingScreenPositionsNormalized;
        if (screenPositionsNormalized.Count > 0)
        {
            brushPoints = new List<BrushPoint>(capacity: screenPositionsNormalized.Count);
            for (int i = 0; i < screenPositionsNormalized.Count; i++)
            {
                var hitResult = _paintRendererService.GetCursorHitResult(screenPositionsNormalized[i]);
                if (hitResult.IsHit)
                {
                    var brushPoint = new BrushPoint
                    {
                        WorldPosition = hitResult.WorldPosition,
                        WorldNormal = hitResult.WorldNormal,
                        Strength = brushSettings.BrushStrength,
                    };
                    brushPoints.Add(brushPoint);
                    sessionData.StrokeMapBrushPoints.Add(brushPoint);
                }
            }
        }
        if (brushPoints?.Count > 0)
        {
            var targetEntityMeshStrokeMaps = new Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture>(sessionData.TargetEntityMeshToStrokeMapTextureMap);
            painterTool.GetAndPrepareTargetEntityMeshAndPaintRenderTargetMap(targetEntityMeshStrokeMaps);

            BrushFalloffType brushFalloffType = BrushFalloffType.NotSet;
            float falloffStartPercentage = 0;
            Texture? brushTexture = null;
            if (brushSettings.BrushShape is PaintCircularBrushShape circularBrushShape)
            {
                brushFalloffType = circularBrushShape.FalloffType;
                falloffStartPercentage = circularBrushShape.FalloffStartPercentage;
            }
            else if (brushSettings.BrushShape is PaintTextureBrushShape textureBrushShape)
            {
                brushTexture = textureBrushShape.Texture;
            }
            var brushRenderArgs = new BrushRenderArgs
            {
                BrushModeType = brushSettings.BrushShape?.BrushMode ?? default,
                BrushRadius = brushSettings.BrushDiameter * 0.5f,
                BrushOpacity = brushSettings.Opacity,
                BrushFalloffType = brushFalloffType,
                FalloffStartPercentage = falloffStartPercentage,
                BrushTexture = brushTexture,
                BrushPoints = brushPoints,
                TargetEntityMeshToStrokeMapTextureMap = targetEntityMeshStrokeMaps,
                RenderCompletedCallback = sessionData.IsFinalBrushstroke ? OnGeneratedStrokeMapsCallback : null
            };
            _paintRendererService.EnqueueBrushRender(brushRenderArgs);
            sessionData.IsFinalBrushstroke = false;
            if (prevTotalBrushPointCount == 0)
            {
                // This is the initial stroke
                var initialBrushPoint = sessionData.StrokeMapBrushPoints[0];
                painterTool.PaintStarted(initialBrushPoint);
            }

            screenPositionsNormalized.Clear();
        }
        else if (sessionData.IsFinalBrushstroke)
        {
            sessionData.IsFinalBrushstroke = false;
            painterTool.PaintCompleted(sessionData.StrokeMapBrushPoints);
            sessionData.StrokeMapBrushPoints.Clear();
        }
    }

    private void OnGeneratedStrokeMapsCallback(Dictionary<PaintTargetEntityMesh, Texture> generatedStrokeMaps)
    {
        if (_activePaintSessionId == default
            || !_sessionIdToSessionData.TryGetValue(_activePaintSessionId, out var sessionData))
        {
            // TODO throw error?
            return;
        }

        System.Diagnostics.Debug.WriteLineIf(generatedStrokeMaps.Count > 0, $"OnGeneratedStrokeMapsCallback: {generatedStrokeMaps.Count}");
        if (sessionData.PaintBrushComponent.SelectedPainterTool is IPainterTool painterTool)
        {
            painterTool.PaintCompleted(sessionData.StrokeMapBrushPoints);
            sessionData.StrokeMapBrushPoints.Clear();
        }
    }

    private record PaintSessionData(
        Entity PainterEntity,
        PaintBrushComponent PaintBrushComponent,
        ModelComponent PaintBrushCursorModelComponent)
    {
        public Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> TargetEntityMeshToStrokeMapTextureMap { get; } = [];

        public List<Vector2> PendingScreenPositionsNormalized { get; } = [];
        public List<BrushPoint> StrokeMapBrushPoints { get; } = [];
        public bool IsFinalBrushstroke = false;
    }
}
