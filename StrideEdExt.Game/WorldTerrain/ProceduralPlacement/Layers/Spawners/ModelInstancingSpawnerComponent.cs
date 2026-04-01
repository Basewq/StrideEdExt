using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.Spawners;

public class ModelInstancingSpawnerComponent : ObjectSpawnerComponentBase
{
    private static readonly TimeSpan NextCheckTime = TimeSpan.FromMilliseconds(250);
    private DateTime _nextHasChangedCheckTime = DateTime.MaxValue;

    public override Type LayerDataType => typeof(ModelInstancingSpawnerData);

    private ObjectPlacementModelType _modelType = ObjectPlacementModelType.Static;
    public ObjectPlacementModelType ModelType { get => _modelType; set => SetValue(ref _modelType, value); }

    public TrackingCollection<ModelInstanceSpawnerData> Models { get; } = [];

    protected override void OnInitialize()
    {
        HasChanged = false;
        foreach (var model in Models)
        {
            model.HasChanged = false;
        }
        Models.CollectionChanged += OnModelsCollectionChanged;

        _nextHasChangedCheckTime = DateTime.Now + NextCheckTime;
    }

    private void OnModelsCollectionChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        HasChanged = true;
    }

    protected override void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
        // Throttle the HasChanged check
        var nowTime = DateTime.Now;
        if (_nextHasChangedCheckTime > nowTime)
        {
            return;
        }
        _nextHasChangedCheckTime = nowTime + NextCheckTime;

        if (!(HasChanged || Models.Any(x => x.HasChanged))
            || !TryGetObjectPlacementMapEditor(out var objectPlacementMapEditor))
        {
            return;
        }

        objectPlacementMapEditor.SendOrEnqueueEditorRequest(objectDensityMapAssetId =>
        {
            var objectSpawnAssetDefinitionList = Models.Select(x => x.ToObjectSpawnAssetDefinition()).ToList();
            var request = new UpdateObjectPlacementModelInstacingSpawnerDataRequest
            {
                ObjectPlacementMapAssetId = objectDensityMapAssetId,
                LayerId = LayerId,
                MinimumDensityValueThreshold = MinimumDensityValueThreshold,
                ModelType = ModelType,
                ObjectSpawnAssetDefinitionList = objectSpawnAssetDefinitionList,
            };
            return request;
        });
        HasChanged = false;
        foreach (var model in Models)
        {
            model.HasChanged = false;
        }
    }
}

[DataContract]
public class ModelInstanceSpawnerData
{
    private Model? _model;
    public Model? Model { get => _model; set => SetValue(ref _model, value, doAssetRefCheck: true); }

    private float _spawnWeightValue = 1;
    public float SpawnWeightValue { get => _spawnWeightValue; set => SetValue(ref _spawnWeightValue, value); }

    private float _collisionRadius = 0.5f;
    public float CollisionRadius { get => _collisionRadius; set => SetValue(ref _collisionRadius, value); }

    [DataMemberIgnore]
    public bool HasChanged { get; set; }

    private void SetValue<T>(ref T backingField, T newValue, bool doAssetRefCheck = false)
    {
        bool hasChanged = !EqualityComparer<T>.Default.Equals(backingField, newValue);
        if (doAssetRefCheck && backingField is not null && newValue is not null)
        {
            var origValueAttachedRef = AttachedReferenceManager.GetAttachedReference(backingField);
            var newValueAttachedRef = AttachedReferenceManager.GetAttachedReference(newValue);
            hasChanged = !string.Equals(origValueAttachedRef?.Url, newValueAttachedRef?.Url, StringComparison.OrdinalIgnoreCase);
        }
        backingField = newValue;
        HasChanged = HasChanged || hasChanged;
    }

    public ObjectSpawnAssetDefinition ToObjectSpawnAssetDefinition()
    {
        string? assetUrl = null;
        if (Model is not null)
        {
            assetUrl = AttachedReferenceManager.GetUrl(Model);
        }
        return new ObjectSpawnAssetDefinition
        {
            AssetUrl = assetUrl,
            SpawnWeightValue = SpawnWeightValue,
            CollisionRadius = CollisionRadius
        };
    }
}
