using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.Spawners;

public class PrefabSpawnerComponent : ObjectSpawnerComponentBase
{
    private static readonly TimeSpan NextCheckTime = TimeSpan.FromMilliseconds(250);
    private DateTime _nextHasChangedCheckTime = DateTime.MaxValue;

    public override Type LayerDataType => typeof(PrefabSpawnerData);

    public TrackingCollection<PrefabInstanceSpawnerData> Prefabs { get; } = [];

    protected override void OnInitialize()
    {
        HasChanged = false;
        foreach (var prefab in Prefabs)
        {
            prefab.HasChanged = false;
        }
        Prefabs.CollectionChanged += OnPrefabsCollectionChanged;

        _nextHasChangedCheckTime = DateTime.Now + NextCheckTime;
    }

    private void OnPrefabsCollectionChanged(object? sender, TrackingCollectionChangedEventArgs e)
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

        if (!(HasChanged || Prefabs.Any(x => x.HasChanged))
            || !TryGetObjectPlacementMapEditor(out var objectPlacementMapEditor))
        {
            return;
        }

        objectPlacementMapEditor.SendOrEnqueueEditorRequest(objectDensityMapAssetId =>
        {
            var objectSpawnAssetDefinitionList = Prefabs.Select(x => x.ToObjectSpawnAssetDefinition()).ToList();
            var request = new UpdateObjectPlacementPrefabSpawnerDataRequest
            {
                ObjectPlacementMapAssetId = objectDensityMapAssetId,
                LayerId = LayerId,
                MinimumDensityValueThreshold = MinimumDensityValueThreshold,
                ObjectSpawnAssetDefinitionList = objectSpawnAssetDefinitionList,
            };
            return request;
        });
        HasChanged = false;
        foreach (var prefab in Prefabs)
        {
            prefab.HasChanged = false;
        }
    }
}

[DataContract]
public class PrefabInstanceSpawnerData
{
    private Prefab? _prefab;
    public Prefab? Prefab { get => _prefab; set => SetValue(ref _prefab, value, doAssetRefCheck: true); }

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
        if (Prefab is not null)
        {
            assetUrl = AttachedReferenceManager.GetUrl(Prefab);
        }
        return new ObjectSpawnAssetDefinition
        {
            AssetUrl = assetUrl,
            SpawnWeightValue = SpawnWeightValue,
            CollisionRadius = CollisionRadius
        };
    }
}
