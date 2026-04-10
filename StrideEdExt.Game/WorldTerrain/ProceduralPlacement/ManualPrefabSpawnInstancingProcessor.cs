using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.Spawners;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NotNull = Stride.Core.Annotations.NotNullAttribute;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement;

class ManualPrefabSpawnInstancingProcessor : EntityProcessor<ManualPrefabSpawnInstancingComponent, ManualPrefabSpawnInstancingProcessor.AssociatedData>
{
    private static readonly TimeSpan NextCheckTime = TimeSpan.FromMilliseconds(350);
    private DateTime _nextHasChangedCheckTime = DateTime.MaxValue;

    private static readonly TimeSpan NextRetryTime = TimeSpan.FromSeconds(3);
    private DateTime _getObjectSpawnerLayerNextRetryTime = DateTime.MinValue;
    private DateTime _getObjectPlacementMapNextRetryTime = DateTime.MinValue;

    private IRuntimeToEditorMessagingService _runtimeToEditorMessagingService = default!;

    private readonly Dictionary<Entity, ObjectPlacementMapEditorComponent> _spawnerToEditorComponentMap = [];
    private readonly Dictionary<ObjectPlacementMapEditorComponent, List<PendingDeletePrefabTransform>> _pendingEditorToDeletePrefabTransformList = [];

    public ManualPrefabSpawnInstancingProcessor()
    {
    }

    protected override void OnSystemAdd()
    {
        _nextHasChangedCheckTime = DateTime.Now + NextCheckTime;

        _runtimeToEditorMessagingService = Services.GetSafeServiceAs<IRuntimeToEditorMessagingService>();

        EntityManager.EntityRemoved += OnEntityRemoved;
    }

    private void OnEntityRemoved(object? sender, Entity e)
    {
        if (e.TryGetComponent<ObjectPlacementMapEditorComponent>(out var objectPlacementMapEditor))
        {
            _pendingEditorToDeletePrefabTransformList.Remove(objectPlacementMapEditor);
            foreach (var (comp, data) in ComponentDatas)
            {
                if (data.EditorComponent == objectPlacementMapEditor)
                {
                    data.EditorComponent = null;
                }
            }

            List<Entity>? removeSpawnerKey = null;
            foreach (var (spawnerEnt, editorComp) in _spawnerToEditorComponentMap)
            {
                if (editorComp == objectPlacementMapEditor)
                {
                    removeSpawnerKey ??= [];
                    removeSpawnerKey.Add(spawnerEnt);
                }
            }
            if (removeSpawnerKey is not null)
            {
                foreach (var ent in removeSpawnerKey)
                {
                    _spawnerToEditorComponentMap.Remove(ent);
                }
            }
        }
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] ManualPrefabSpawnInstancingComponent component)
    {
        return new AssociatedData
        {
            PreviousTransformTRS = EntityExtensions.UnsetTransformTRS,
            PreviousIsEnabled = component.IsEnabled,
        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] ManualPrefabSpawnInstancingComponent component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] ManualPrefabSpawnInstancingComponent component, [NotNull] AssociatedData data)
    {
        foreach (var (editorComp, pendingDeletePrefabList) in _pendingEditorToDeletePrefabTransformList)
        {
            pendingDeletePrefabList.RemoveAll(x => x.SpawnInstancingId == component.SpawnInstancingId);  // If reordering entities in the editor, the entity is removed then re-added
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ManualPrefabSpawnInstancingComponent component, [NotNull] AssociatedData data)
    {
        if (data.EditorComponent is not null && data.EditorComponent.ObjectPlacementMap is not null
            && data.SpawnerLayerComponent is not null
            && _pendingEditorToDeletePrefabTransformList.TryGetValue(data.EditorComponent, out var pendingDeletePrefabList))
        {
            var deletePrefabTransform = new PendingDeletePrefabTransform
            {
                ObjectPlacementMapAssetId = data.EditorComponent.ObjectPlacementMap.ObjectPlacementMapAssetId,
                LayerId = data.SpawnerLayerComponent.LayerId,
                SpawnInstancingId = component.SpawnInstancingId
            };
            pendingDeletePrefabList.Add(deletePrefabTransform);
        }

        _spawnerToEditorComponentMap.Remove(entity);
    }

    public override void Update(GameTime time)
    {
        // Throttle the HasChanged check
        var nowTime = DateTime.Now;
        if (_nextHasChangedCheckTime > nowTime)
        {
            return;
        }
        _nextHasChangedCheckTime = nowTime + NextCheckTime;

        List<UpdateObjectPlacementManualPrefabSpawnerDataRequest>? updateRequestList = null;
        foreach (var (comp, data) in ComponentDatas)
        {
            var transformTRS = comp.Entity.GetTransformTRS();
            bool hasChanged = false
                || comp.IsEnabled != data.PreviousIsEnabled
                || !transformTRS.IsSame(data.PreviousTransformTRS)
                || comp.CollisionRadius != data.PreviousCollisionRadius
            ;
            string? prefabUrl = null;
            if (!hasChanged)
            {
                var prefabAttachedRef = AttachedReferenceManager.GetAttachedReference(comp.Prefab!);
                prefabUrl = prefabAttachedRef?.Url;
                if (!string.Equals(prefabUrl, data.PreviousPrefabUrl, StringComparison.OrdinalIgnoreCase))
                {
                    hasChanged = true;
                }
            }
            if (!hasChanged)
            {
                continue;
            }

            updateRequestList ??= [];
            if (!TryGetObjectPlacementMapEditor(comp.Entity, data, out var objectPlacementMapEditor)
                || !TryGetSpawnerLayerComponent(comp.Entity, data, out var spawnerLayerComponent))
            {
                continue;
            }
            var objectPlacementMapAssetId = objectPlacementMapEditor.ObjectPlacementMap!.ObjectPlacementMapAssetId;
            var layerId = spawnerLayerComponent.LayerId;
            var request = updateRequestList.FirstOrDefault(x => x.ObjectPlacementMapAssetId == objectPlacementMapAssetId && x.LayerId == layerId);
            if (request is null)
            {
                request = new UpdateObjectPlacementManualPrefabSpawnerDataRequest
                {
                    ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                    LayerId = layerId,
                    UpsertPrefabTransformList = [],
                    DeletePrefabTransformList = [],
                };
                updateRequestList.Add(request);
            }

            var upsertPrefabTransform = new UpsertObjectPlacementManualPrefabTransform
            {
                SpawnInstancingId = comp.SpawnInstancingId,
                IsEnabled = comp.IsEnabled,
                PrefabUrl = prefabUrl,
                CollisionRadius = comp.CollisionRadius,
                Position = transformTRS.Position,
                Orientation = transformTRS.Rotation,
                Scale = transformTRS.Scale
            };
            request.UpsertPrefabTransformList!.Add(upsertPrefabTransform);

            data.PreviousIsEnabled = comp.IsEnabled;
            data.PreviousTransformTRS = transformTRS;
            data.PreviousPrefabUrl = prefabUrl;
            data.PreviousCollisionRadius = comp.CollisionRadius;
        }

        if (_pendingEditorToDeletePrefabTransformList.Count > 0)
        {
            updateRequestList ??= [];
            foreach (var (editor, deletePrefabTransformList) in _pendingEditorToDeletePrefabTransformList)
            {
                foreach (var pendingDelete in deletePrefabTransformList)
                {
                    var objectPlacementMapAssetId = pendingDelete.ObjectPlacementMapAssetId;
                    var layerId = pendingDelete.LayerId;
                    var request = updateRequestList.FirstOrDefault(x => x.ObjectPlacementMapAssetId == objectPlacementMapAssetId && x.LayerId == layerId);
                    if (request is null)
                    {
                        request = new UpdateObjectPlacementManualPrefabSpawnerDataRequest
                        {
                            ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                            LayerId = layerId,
                            UpsertPrefabTransformList = [],
                            DeletePrefabTransformList = [],
                        };
                        updateRequestList.Add(request);
                    }

                    var deletePrefabTransform = new DeleteObjectPlacementManualPrefabTransform
                    {
                        SpawnInstancingId = pendingDelete.SpawnInstancingId
                    };
                    request.DeletePrefabTransformList!.Add(deletePrefabTransform);
                }
            }
            _pendingEditorToDeletePrefabTransformList.Clear();
        }
        if (updateRequestList is not null)
        {
            foreach (var updateRequest in updateRequestList)
            {
                _runtimeToEditorMessagingService.Send(updateRequest);
            }
        }
    }

    private bool TryGetSpawnerLayerComponent(Entity spawnerEntity, AssociatedData data, [NotNullWhen(true)] out ManualPrefabSpawnerComponent? spawnerLayerComponent)
    {
        spawnerLayerComponent = data.SpawnerLayerComponent;
        if (spawnerLayerComponent is not null)
        {
            return true;
        }
        if (_getObjectSpawnerLayerNextRetryTime > DateTime.Now)
        {
            return false;
        }

        if (spawnerEntity.TryFindComponentOnAncestor<ManualPrefabSpawnerComponent>(out var spwnLayerComp))
        {
            data.SpawnerLayerComponent = spwnLayerComp;
            spawnerLayerComponent = spwnLayerComp;
        }
        else
        {
            _getObjectSpawnerLayerNextRetryTime = DateTime.Now + NextRetryTime;
        }

        return spawnerLayerComponent is not null;
    }

    private bool TryGetObjectPlacementMapEditor(Entity spawnerEntity, AssociatedData data, [NotNullWhen(true)] out ObjectPlacementMapEditorComponent? objectPlacementMapEditor)
    {
        objectPlacementMapEditor = data.EditorComponent;
        if (objectPlacementMapEditor is not null)
        {
            return true;
        }
        if (_getObjectPlacementMapNextRetryTime > DateTime.Now)
        {
            return false;
        }

        if (spawnerEntity.TryFindComponentOnAncestor<ObjectPlacementMapEditorComponent>(out var objPlcMapEditorComp))
        {
            if (EnsureLoadedObjectDensityMap(objPlcMapEditorComp?.ObjectPlacementMap))
            {
                data.EditorComponent = objectPlacementMapEditor;
                objectPlacementMapEditor = objPlcMapEditorComp;
            }
        }

        return objectPlacementMapEditor is not null;
    }

    private bool EnsureLoadedObjectDensityMap(ObjectPlacementMap? objectPlacementMap)
    {
        if (objectPlacementMap is not null && !EditorExtensions.IsRuntimeAssetLoaded(objectPlacementMap))
        {
            Debug.WriteLine("ManualPrefabSpawnInstancingProcessor: Editor Content Manager Object Placement Map not ready. Scheduling retry.");
            _getObjectPlacementMapNextRetryTime = DateTime.Now + NextRetryTime;

            return false;
        }
        return objectPlacementMap is not null;
    }

    private record PendingDeletePrefabTransform
    {
        public required AssetId ObjectPlacementMapAssetId { get; init; }
        public required Guid LayerId { get; init; }
        public required Guid SpawnInstancingId { get; init; }
    }

    public class AssociatedData
    {
        public ObjectPlacementMapEditorComponent? EditorComponent;
        public ManualPrefabSpawnerComponent? SpawnerLayerComponent;

        public TransformTRS PreviousTransformTRS;
        public bool PreviousIsEnabled;
        public string? PreviousPrefabUrl;
        public float PreviousCollisionRadius;
    }
}
