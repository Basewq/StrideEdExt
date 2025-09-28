using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers;

[ComponentCategory("Procedural Spawners")]
[DataContract(Inherited = true)]
[DefaultEntityComponentProcessor(typeof(ObjectSpawnerProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
public abstract class ObjectSpawnerComponentBase : EntityComponent, IObjectSpawnerLayer
{
#if GAME_EDITOR
    private static readonly TimeSpan NextRetryTime = TimeSpan.FromSeconds(3);
    private DateTime _getObjectDensityMapNextRetryTime = DateTime.MinValue;
#endif

    protected IServiceRegistry Services { get; private set; } = default!;
    protected internal ObjectPlacementMapEditorComponent? EditorComponent { get; set; }

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    [DataMemberIgnore]
    public Guid LayerId => Id;      // Use the entity component's ID as the layer ID

    [DataMemberIgnore]
    public abstract Type LayerDataType { get; }

    private float _objectSpacing = 1f;
    public float ObjectSpacing { get => _objectSpacing; set => SetValue(ref _objectSpacing, value); }

    private float _minimumDensityValueThreshold;
    [DataMemberRange(minimum: 0, maximum: 1, smallStep: 0.01, largeStep: 0.1, decimalPlaces: 4)]
    public float MinimumDensityValueThreshold { get => _minimumDensityValueThreshold; set => SetValue(ref _minimumDensityValueThreshold, value); }

    private float _surfaceNormalMinimumAngleDegrees = 0;
    [Display(name: "Surface Normal Minimum (degrees)")]
    [DataMemberRange(minimum: 0, maximum: 180, smallStep: 0.1, largeStep: 5, decimalPlaces: 4)]
    public float SurfaceNormalMinimumAngleDegrees { get => _surfaceNormalMinimumAngleDegrees; set => SetValue(ref _surfaceNormalMinimumAngleDegrees, value); }

    private float _surfaceNormalMaximumAngleDegrees = 180;
    [Display(name: "Surface Normal Maximum (degrees)")]
    [DataMemberRange(minimum: 0, maximum: 180, smallStep: 0.1, largeStep: 5, decimalPlaces: 4)]
    public float SurfaceNormalMaximumAngleDegrees { get => _surfaceNormalMaximumAngleDegrees; set => SetValue(ref _surfaceNormalMaximumAngleDegrees, value); }

    private bool _alignWithSurfaceNormal;
    public bool AlignWithSurfaceNormal { get => _alignWithSurfaceNormal; set => SetValue(ref _alignWithSurfaceNormal, value); }

    private float _positionOffsetMinimumRadius = 0;
    [DataMemberRange(minimum: 0, decimalPlaces: 4)]
    public float PositionOffsetMinimumRadius { get => _positionOffsetMinimumRadius; set => SetValue(ref _positionOffsetMinimumRadius, value); }

    private float _positionOffsetMaximumRadius = 0;
    [DataMemberRange(minimum: 0, decimalPlaces: 4)]
    public float PositionOffsetMaximumRadius { get => _positionOffsetMaximumRadius; set => SetValue(ref _positionOffsetMaximumRadius, value); }

    private float _rotationYOffsetMinimumAngleDegrees = 0;
    [Display(name: "Rotation Y Offset Minimum (degrees)")]
    [DataMemberRange(minimum: -360, maximum: 360, smallStep: 0.1, largeStep: 5, decimalPlaces: 4)]
    public float RotationYOffsetMinimumAngleDegrees { get => _rotationYOffsetMinimumAngleDegrees; set => SetValue(ref _rotationYOffsetMinimumAngleDegrees, value); }

    private float _rotationYOffsetMaximumAngleDegrees = 360;
    [Display(name: "Rotation Y Offset Maximum (degrees)")]
    [DataMemberRange(minimum: -360, maximum: 360, smallStep: 0.1, largeStep: 5, decimalPlaces: 4)]
    public float RotationYOffsetMaximumAngleDegrees { get => _rotationYOffsetMaximumAngleDegrees; set => SetValue(ref _rotationYOffsetMaximumAngleDegrees, value); }

    private float _scaleMinimum = 1;
    [DataMemberRange(minimum: 0, decimalPlaces: 4)]
    public float ScaleMinimum { get => _scaleMinimum; set => SetValue(ref _scaleMinimum, value); }

    private float _scaleMaximum = 1;
    [DataMemberRange(minimum: 0, decimalPlaces: 4)]
    public float ScaleMaximum { get => _scaleMaximum; set => SetValue(ref _scaleMaximum, value); }

    [DataMemberIgnore]
    public bool HasChanged { get; set; }

    protected void SetValue<T>(ref T backingField, T newValue, bool doAssetRefCheck = false)
    {
        bool hasChanged = !EqualityComparer<T>.Default.Equals(backingField, newValue);
        if (doAssetRefCheck && backingField is not null && newValue is not null)
        {
            var origValueAttachedRef = AttachedReferenceManager.GetAttachedReference(backingField);
            var newValueAttachedRef = AttachedReferenceManager.GetAttachedReference(newValue);
            hasChanged = !string.Equals(origValueAttachedRef?.Url, newValueAttachedRef?.Url, StringComparison.OrdinalIgnoreCase);
        }
        backingField = newValue;
        HasChanged = HasChanged || (hasChanged && IsInitialized);
    }

    public void Initialize(IServiceRegistry services)
    {
        Services = services;

        OnInitialize();
        IsInitialized = true;
    }

    protected virtual void OnInitialize() { }

    public void Deinitialize()
    {
        OnDeinitialize();
    }

    protected virtual void OnDeinitialize() { }

    public void Update(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
        OnUpdate(gameTime, overrideCameraComponent);
    }

    protected virtual void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent) { }

    public void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
        OnUpdateForDraw(time, overrideCameraComponent);
    }
    protected virtual void OnUpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent) { }

    protected bool TryGetObjectPlacementMapEditor([NotNullWhen(true)] out ObjectPlacementMapEditorComponent? objectPlacementMapEditor)
    {
        objectPlacementMapEditor = null;
#if GAME_EDITOR
        if (_getObjectDensityMapNextRetryTime > DateTime.Now)
        {
            return false;
        }
#endif

        if (Entity.TryFindComponentOnAncestor<ObjectPlacementMapEditorComponent>(out var objPlcMapEditorComp))
        {
            if (EnsureLoadedObjectDensityMap(objPlcMapEditorComp?.ObjectPlacementMap))
            {
                objectPlacementMapEditor = objPlcMapEditorComp;
            }
        }

        return objectPlacementMapEditor is not null;
    }

    private bool EnsureLoadedObjectDensityMap(ObjectPlacementMap? objectPlacementMap)
    {
        if (objectPlacementMap is not null && !EditorExtensions.IsRuntimeAssetLoaded(objectPlacementMap))
        {
            Debug.WriteLine("ObjectSpawnerComponentBase: Editor Content Manager Object Placement Map not ready. Scheduling retry.");
#if GAME_EDITOR
            _getObjectDensityMapNextRetryTime = DateTime.Now + NextRetryTime;
#endif
            return false;
        }
        return objectPlacementMap is not null;
    }
}
