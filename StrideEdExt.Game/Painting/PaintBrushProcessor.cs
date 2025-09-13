using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.PaintRenderer;
using StrideEdExt.StrideEditorExt;

namespace StrideEdExt.Painting;

class PaintBrushProcessor : EntityProcessor<PaintBrushComponent, PaintBrushProcessor.AssociatedData>, IEntityComponentRenderProcessor
{
    private GraphicsDevice _graphicsDevice = default!;
    private InputManager _inputManager = default!;
    private IPaintRendererService _pickService = default!;
    private IPainterService _painterService = default!;

    private IStrideEditorMouseService _editorMouseService = default!;

    public VisibilityGroup VisibilityGroup { get; set; } = default!;

    protected override void OnSystemAdd()
    {
        VisibilityGroup.Tags.Set(PaintingRenderFeature.PickableObjectEntityMeshSetKey, _paintableEntityMeshes);

        _graphicsDevice = Services.GetSafeServiceAs<IGraphicsDeviceService>().GraphicsDevice;
        _inputManager = Services.GetSafeServiceAs<InputManager>();
        _pickService = Services.GetSafeServiceAs<IPaintRendererService>();
        _painterService = Services.GetSafeServiceAs<IPainterService>();

        _editorMouseService = StrideEditorMouseService.GetOrCreate(Services);
    }

    protected override void OnSystemRemove()
    {
        VisibilityGroup.Tags.Remove(PaintingRenderFeature.PickableObjectEntityMeshSetKey);
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] PaintBrushComponent component)
    {
        return new AssociatedData
        {
            TransformComponent = entity.Transform,
        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] PaintBrushComponent component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] PaintBrushComponent component, [NotNull] AssociatedData data)
    {
        data.ModelComponent = entity.Get<ModelComponent>();
        data.Material = data.ModelComponent?.GetMaterial(0);
    }

    internal bool IsValidTargetEntityMesh(PaintTargetEntityMesh targetEntityMesh)
    {
        bool isValid = _paintableEntityMeshes.Contains(targetEntityMesh);
        return isValid;
    }

    private readonly Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> _paintableEntityMeshToRenderTargetMap = [];
    private readonly HashSet<PaintTargetEntityMesh> _paintableEntityMeshes = [];
    private PaintBrushstrokeHandle? _brushstrokeHandle;
    private bool _wasPreviousUpdateActiveSession = false;
    public override void Update(GameTime time)
    {
        if (_painterService.TryGetActiveSessionId(out var activePaintSessionId))
        {
            foreach (var (comp, data) in ComponentDatas)
            {
                if (comp.PaintSessionId != activePaintSessionId
                    || comp.SelectedPainterTool is not IPainterTool painterTool)
                {
                    continue;
                }
                ProcessActiveBrush(comp, data, painterTool);
                _wasPreviousUpdateActiveSession = true;

                // Set entity IDs for next rendering
                _paintableEntityMeshToRenderTargetMap.Clear();
                painterTool.GetAndPrepareTargetEntityMeshAndPaintRenderTargetMap(_paintableEntityMeshToRenderTargetMap);
                _paintableEntityMeshes.Clear();
                foreach (var (targetEntityMesh, _) in _paintableEntityMeshToRenderTargetMap)
                {
                    _paintableEntityMeshes.Add(targetEntityMesh);
                }

                break;
                // Only one brush should be active
            }
        }
        else if (_wasPreviousUpdateActiveSession)
        {
            _wasPreviousUpdateActiveSession = false;

            _paintableEntityMeshToRenderTargetMap.Clear();
            _paintableEntityMeshes.Clear();
        }
    }

    private void ProcessActiveBrush(PaintBrushComponent brushComp, AssociatedData data, IPainterTool painterTool)
    {
        if (!brushComp.TryGetActiveToolBrushSettings(out var brushSettings))
        {
            return;
        }

        var paintSessionId = brushComp.PaintSessionId;

        var normalizedMousePosition = _inputManager.MousePosition;
        var pickHitResult = _pickService.GetCursorHitResult(normalizedMousePosition);
        var leftMouseBtnState = MouseButtonStateExtensions.GetButtonState(_inputManager, MouseButton.Left);
        bool isLeftMouseBtnDown = leftMouseBtnState.IsDown();
        _editorMouseService.SetIsControllingMouse(isLeftMouseBtnDown, owner: this);
        if (leftMouseBtnState == MouseButtonState.JustPressed)
        {
            if (pickHitResult.IsHit && pickHitResult.PaintSessionId == paintSessionId)
            {
                _brushstrokeHandle = _painterService.BeginBrushstroke(paintSessionId, normalizedMousePosition);
                brushComp.IsBrushActionInProgress = true;

                var hitWorldPosValue = pickHitResult.WorldPosition;
                data.PreviousHitWorldPosition = hitWorldPosValue;
                data.PreviousHitNormalizedScreenPosition = normalizedMousePosition;
            }
        }
        else if (brushComp.IsBrushActionInProgress && leftMouseBtnState == MouseButtonState.HeldDown)
        {
            if (_brushstrokeHandle is null)
            {
                throw new InvalidOperationException("BrushstrokeHandle was not created.");
            }

            if (data.PreviousHitNormalizedScreenPosition != normalizedMousePosition)
            {
                if (pickHitResult.IsHit && pickHitResult.PaintSessionId == paintSessionId)
                {
                    var hitWorldPosValue = pickHitResult.WorldPosition;
                    var prevHitWorldPos = data.PreviousHitWorldPosition ?? hitWorldPosValue;
                    Vector3.Distance(in prevHitWorldPos, in hitWorldPosValue, out float dist);

                    float nextStampDistanceThreshold = brushSettings.BrushDiameter * brushSettings.StampSpacingPercentage / 100f;
                    if (dist >= nextStampDistanceThreshold)
                    {
                        int stampCount = (int)Math.Floor(dist / nextStampDistanceThreshold);
                        var cursorMoveDirection = Vector3.Normalize(hitWorldPosValue - prevHitWorldPos);
                        var nextStepHitWorldPos = cursorMoveDirection * nextStampDistanceThreshold;
                        var nextHitWorldPos = prevHitWorldPos;
                        for (int i = 0; i < stampCount; i++)
                        {
                            _painterService.AddBrushstrokePoint(_brushstrokeHandle, normalizedMousePosition);
                            nextHitWorldPos += nextStepHitWorldPos;
                        }
                        data.PreviousHitWorldPosition = nextHitWorldPos;
                        data.PreviousHitNormalizedScreenPosition = normalizedMousePosition;
                    }
                }
            }
        }
        else if (brushComp.IsBrushActionInProgress && leftMouseBtnState == MouseButtonState.JustReleased)
        {
            DisposableExtensions.DisposeAndNull(ref _brushstrokeHandle);
            brushComp.IsBrushActionInProgress = false;
        }

        //var pickHitResult = _pickService.GetPickHitResult();
        //if (!pickHitResult.IsHit && !data.IsBrushActionInProgress)
        //{
        //    return;
        //}

        // Update brush cursor position/orientation hit location
        if (pickHitResult.IsHit)
        {
            brushComp.Entity.Transform.Position = pickHitResult.WorldPosition;
            var orientation = Quaternion.BetweenDirections(Vector3.UnitY, pickHitResult.WorldNormal);
            brushComp.Entity.Transform.Rotation = orientation;
        }
        //else if (brushComp.IsBrushActionInProgress)
        //{
        //    // Hide cursor?
        //}
        // Update brush cursor visual
        float sizeXZ = brushSettings.BrushDiameter;
        var scale = new Vector3(sizeXZ, 1, sizeXZ);
        brushComp.Entity.Transform.Scale = scale;
        if (data.ModelComponent is ModelComponent modelComp)
        {
            painterTool.UpdateCursorPreviewModel(modelComp);
        }
    }

    internal class AssociatedData
    {
        //public bool IsShowing;
        public TransformComponent TransformComponent = default!;
        public ModelComponent? ModelComponent;
        public Material? Material;

        public Vector3? PreviousHitWorldPosition;
        public Vector2 PreviousHitNormalizedScreenPosition;

        public Vector3[]? PendingBrushStampWorldPositions;
    }
}
