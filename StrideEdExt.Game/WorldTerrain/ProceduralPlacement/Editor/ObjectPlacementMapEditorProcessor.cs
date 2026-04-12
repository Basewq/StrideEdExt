using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing.EditorToRuntimeMessages;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Layers;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.DensityMaps;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.WorldTerrain.Terrain3d;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using System.Diagnostics.CodeAnalysis;
using NotNull = Stride.Core.Annotations.NotNullAttribute;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;

class ObjectPlacementMapEditorProcessor : EntityProcessor<ObjectPlacementMapEditorComponent, ObjectPlacementMapEditorProcessor.AssociatedData>
{
    private IRuntimeToEditorMessagingService? _runtimeToEditorMessagingService;
    private List<IDisposable> _editorMessageSubscriptions = [];

    private DateTime _processorStartTime = DateTime.MaxValue;

    private TerrainMapEditorProcessor _terrainMapEditorProcessor = default!;

    public ObjectPlacementMapEditorProcessor()
    {
        Order = 100100;     // Make this processor's update call after any camera position changes and after SceneEditorExtProcessor
    }

    protected override void OnSystemAdd()
    {
        _terrainMapEditorProcessor = EntityManager.GetProcessor<TerrainMapEditorProcessor>();
        if (_terrainMapEditorProcessor is null)
        {
            _terrainMapEditorProcessor = new TerrainMapEditorProcessor();
            EntityManager.Processors.Add(_terrainMapEditorProcessor);
        }

        _runtimeToEditorMessagingService = Services.GetService<IRuntimeToEditorMessagingService>();
        if (_runtimeToEditorMessagingService is not null)
        {
            RegisterMessageHandler<TestEditorToRuntimeMessage>(msg =>
            {
                System.Diagnostics.Debug.WriteLine($"Message was: {msg.Message}");
            });

            // Density Map messages
            RegisterMessageHandler<SetPainterObjectDensityMapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<TerrainPainterObjectDensityMapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.ObjectDensityMapData, msg.ObjectDensityMapTexturePixelStartPosition);
                }
            });
            RegisterMessageHandler<SetTextureObjectDensityMapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<TextureObjectDensityMapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.ObjectDensityMapData, msg.ObjectDensityMapTexturePixelStartPosition);
                }
            });

            // Spawner messages
            RegisterMessageHandler<SetObjectPlacementObjectDataMessage>(msg =>
            {
                var (editorComp, data) = ComponentDatas.FirstOrDefault(x => x.Key.ObjectPlacementMap?.ObjectPlacementMapAssetId == msg.ObjectPlacementMapAssetId);
                if (editorComp is not null)
                {
                    if (!TryProcessSetObjectPlacements(msg, editorComp, data))
                    {
                        data.PendingSetObjectPlacements = msg;  // Must process later
                    }
                }
            });

            // Heightmap messages
            RegisterMessageHandler<SetTerrainMapHeightmapDataMessage>(msg =>
            {
                foreach (var (editorComp, data) in ComponentDatas)
                {
                    if (!msg.IsInitialData
                        && editorComp.IsInitialized
                        && editorComp.ObjectPlacementMap?.TerrainMapAssetId == msg.TerrainMapAssetId
                        && data.LoadedObjectPlacementMap is not null)
                    {
                        var request = new RegenerateObjectPlacementSpawnerObjectsDataRequest
                        {
                            ObjectPlacementMapAssetId = data.LoadedObjectPlacementMap.ObjectPlacementMapAssetId
                        };
                        _runtimeToEditorMessagingService.Send(request);
                    }
                }
            });

            void RegisterMessageHandler<TMessage>(Action<TMessage> messageHandler)
                where TMessage : IEditorToRuntimeMessage
            {
                var sub = _runtimeToEditorMessagingService.Subscribe<TMessage>(this, messageHandler);
                _editorMessageSubscriptions.Add(sub);
            }
        }

        _processorStartTime = DateTime.Now.AddSeconds(3);
    }

    private bool TryGetComponentByLayerId<TComponent>(Guid layerId, [NotNullWhen(true)] out TComponent? layerComponent)
        where TComponent : IObjectPlacementLayer
    {
        foreach (var (editorComp, compData) in ComponentDatas)
        {
            if (editorComp.Entity.TryFindComponentOnDescendant(x => x.LayerId == layerId, out layerComponent))
            {
                return true;
            }
        }
        layerComponent = default;
        return false;
    }

    protected override void OnSystemRemove()
    {
        foreach (var sub in _editorMessageSubscriptions)
        {
            sub.Dispose();
        }
        _editorMessageSubscriptions.Clear();
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] ObjectPlacementMapEditorComponent component)
    {
        return new AssociatedData
        {
        };
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] ObjectPlacementMapEditorComponent component, [NotNull] AssociatedData data)
    {
        component.EditorProcessor = this;
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ObjectPlacementMapEditorComponent component, [NotNull] AssociatedData data)
    {
        if (data.ObjectPlacementPreviewEntity is not null)
        {
            data.ObjectPlacementPreviewEntity.Scene = null;
        }
    }

    public override void Update(GameTime time)
    {
        if (DateTime.Now < _processorStartTime)
        {
            // HACK: Delay further execution because code like editorComp.GetTerrainInternalAsset() calls code on UI thread
            // which can crash the editor when called while the scene is still being loaded in the editor
            return;
        }

        foreach (var kv in ComponentDatas)
        {
            var editorComp = kv.Key;
            var data = kv.Value;

            if (!editorComp.IsInitialized)
            {
                continue;
            }
            if (editorComp.ObjectPlacementMap is null || !EditorExtensions.IsRuntimeAssetLoaded(editorComp))
            {
                continue;
            }

            if (data.LoadedObjectPlacementMap != editorComp.ObjectPlacementMap)
            {
                // Must wait until array has loaded with correct size (eg. due to resizing the object placement map size)
                //if (editorComp.ObjectPlacementMap.DensityMapData?.Length2d == editorComp.ObjectPlacementMap.DensityMapTextureSize)
                {
                    if (!editorComp.ObjectPlacementMap.IsInitialized)
                    {
                        editorComp.ObjectPlacementMap.Initialize();
                    }
                    data.LoadedObjectPlacementMap = editorComp.ObjectPlacementMap;
                    if (data.ObjectPlacementComponent is not null)
                    {
                        data.ObjectPlacementComponent.ObjectPlacementMap = editorComp.ObjectPlacementMap;
                    }
                }
            }
            TerrainComponent? terrainComponent = null;
            if (data.LoadedObjectPlacementMap is not null && data.ObjectPlacementPreviewEntity is null)
            {
                // Warning: this bypasses the editor and does not appear in the scene/entity tree.
                // An issue is if the user adds any new entities to the scene (at the top level)
                // then the editor will crash because the quantum node tree detects differences
                // in the scene's entity hierarchy, so avoid manually adding more entities and painting
                // at the same time, just save/reload after you add any additional entities.
                TryGetTerrainComponent(editorComp, out terrainComponent);
                if (data.ObjectPlacementComponent is not null && terrainComponent is not null)
                {
                    terrainComponent.UnregisterChunkStreamListener(data.ObjectPlacementComponent);
                }
                data.ObjectPlacementComponent = new()
                {
                    IsEditorEnabled = true,
                    ObjectPlacementMap = data.LoadedObjectPlacementMap,
                };
                data.ObjectPlacementPreviewEntity = new()
                {
                    Name = "ObjectPlacementPreviewEntity"
                };
                data.ObjectPlacementPreviewEntity.Add(data.ObjectPlacementComponent);
                var editorScene = editorComp.Entity.Scene;
                data.ObjectPlacementPreviewEntity.Scene = editorScene;
            }
            if (data.ObjectPlacementComponent is not null
                && !data.IsRegisteredTerrainChunkListener
                && (terrainComponent is not null || TryGetTerrainComponent(editorComp, out terrainComponent)))
            {
                terrainComponent?.RegisterChunkStreamListener(data.ObjectPlacementComponent);
                data.IsRegisteredTerrainChunkListener = true;
            }
            if (data.PendingSetObjectPlacements is not null)
            {
                if (TryProcessSetObjectPlacements(data.PendingSetObjectPlacements, editorComp, data))
                {
                    data.PendingSetObjectPlacements = null;
                }
            }

            break;     // Only deal with one editorComp...
        }
    }

    private bool TryProcessSetObjectPlacements(
        SetObjectPlacementObjectDataMessage setObjectPlacementsMsg,
        ObjectPlacementMapEditorComponent editorComp,
        AssociatedData data)
    {
        if (data.ObjectPlacementComponent is ProceduralObjectPlacementComponent objPlcComp
            && TryGetTerrainComponent(editorComp, out var terrainComp)
            && terrainComp.TerrainMap is TerrainMap terrainMap)
        {
            var chunkIndexToPos = terrainMap.ChunkWorldSizeVec2;
            var posToChunkIndex = chunkIndexToPos != Vector2.Zero
                                    ? 1f / chunkIndexToPos
                                    : Vector2.Zero;
            var chunkIndexToChunkDataMap = new Dictionary<TerrainChunkIndex2d, ObjectPlacementsChunkData>();
            foreach (var (layerId, modelObjPlcDataList) in setObjectPlacementsMsg.LayerIdToModelPlacementDataList)
            {
                //if (!TryGetComponentByLayerId<ObjectSpawnerComponentBase>(layerId, out var layerComp))
                //{
                //    System.Diagnostics.Debug.WriteLine($"{nameof(SetObjectPlacementObjectDataMessage)}: Spawner Layer Component not was found: {layerId}");
                //    continue;
                //}
                //if (layerComp is not ModelInstancingSpawnerComponent modelInstancingSpawnerComp)
                //{
                //    System.Diagnostics.Debug.WriteLine($"{nameof(SetObjectPlacementObjectDataMessage)}: Spawner Layer Component type mismatch: Expected {nameof(ModelInstancingSpawnerComponent)} but type was {layerComp.GetType().Name}");
                //    continue;
                //}

                foreach (var modelObjPlacementsData in modelObjPlcDataList)
                {
                    // Separate placements into chunks
                    var chunkIndexToPlacements = modelObjPlacementsData.Placements
                                                    .GroupBy(x => TerrainChunkIndex2d.ToChunkIndex(x.Position, posToChunkIndex));
                    foreach (var plcGroup in chunkIndexToPlacements)
                    {
                        var chunkIndex = plcGroup.Key;
                        var placementsData = plcGroup.ToList();
                        if (!chunkIndexToChunkDataMap.TryGetValue(chunkIndex, out var chunkData))
                        {
                            chunkData = new ObjectPlacementsChunkData();
                            chunkIndexToChunkDataMap[chunkIndex] = chunkData;
                        }
                        var chunkedObjPlacementsData = modelObjPlacementsData with { Placements = placementsData };
                        chunkData.ModelPlacements.Add(chunkedObjPlacementsData);
                    }
                }
            }
            foreach (var (layerId, prefabObjPlcDataList) in setObjectPlacementsMsg.LayerIdToPrefabPlacementDataList)
            {
                //if (!TryGetComponentByLayerId<ObjectSpawnerComponentBase>(layerId, out var layerComp))
                //{
                //    System.Diagnostics.Debug.WriteLine($"{nameof(SetObjectPlacementObjectDataMessage)}: Spawner Layer Component not was found: {layerId}");
                //    continue;
                //}
                //if (layerComp is not PrefabSpawnerComponent prefabSpawnerComp)
                //{
                //    System.Diagnostics.Debug.WriteLine($"{nameof(SetObjectPlacementObjectDataMessage)}: Spawner Layer Component type mismatch: Expected {nameof(PrefabSpawnerComponent)} but type was {layerComp.GetType().Name}");
                //    continue;
                //}

                foreach (var prefabObjPlacementsData in prefabObjPlcDataList)
                {
                    // Separate placements into chunks
                    var chunkIndexToPlacements = prefabObjPlacementsData.Placements
                                                    .GroupBy(x => TerrainChunkIndex2d.ToChunkIndex(x.Position, posToChunkIndex));
                    foreach (var plcGroup in chunkIndexToPlacements)
                    {
                        var chunkIndex = plcGroup.Key;
                        var placementsData = plcGroup.ToList();
                        if (!chunkIndexToChunkDataMap.TryGetValue(chunkIndex, out var chunkData))
                        {
                            chunkData = new ObjectPlacementsChunkData();
                            chunkIndexToChunkDataMap[chunkIndex] = chunkData;
                        }
                        var chunkedObjPlacementsData = prefabObjPlacementsData with { Placements = placementsData };
                        chunkData.PrefabPlacements.Add(chunkedObjPlacementsData);
                    }
                }
            }

            var modelAssetUrlList = setObjectPlacementsMsg.ModelAssetUrlList.Select(url => new UrlReference<Model>(url)).ToList();
            var prefabAssetUrlList = setObjectPlacementsMsg.PrefabAssetUrlList.Select(url => new UrlReference<Prefab>(url)).ToList();
            objPlcComp.SetOverrideObjectPlacements(modelAssetUrlList, prefabAssetUrlList, chunkIndexToChunkDataMap, terrainMap);
            objPlcComp.RebuildAllVisibleChunks(terrainMap);

            return true;
        }

        return false;
    }

    internal bool TryGetObjectPlacementComponent(ObjectPlacementMapEditorComponent objectPlacementMapEditorComponent, [NotNullWhen(true)] out ProceduralObjectPlacementComponent? objectPlacementComponent)
    {
        if (ComponentDatas.TryGetValue(objectPlacementMapEditorComponent, out var data))
        {
            objectPlacementComponent = data.ObjectPlacementComponent;
            return objectPlacementComponent is not null;
        }

        objectPlacementComponent = null;
        return false;
    }

    internal bool TryGetTerrainComponent(ObjectPlacementMapEditorComponent objectPlacementMapEditorComponent, [NotNullWhen(true)] out TerrainComponent? terrainComponent)
    {
        var terrainMapAssetId = objectPlacementMapEditorComponent.ObjectPlacementMap?.TerrainMapAssetId;
        if (terrainMapAssetId is null)
        {
            terrainComponent = null;
            return false;
        }

        if (_terrainMapEditorProcessor.TryGetTerrainComponentByTerrainMapAssetId(terrainMapAssetId.Value, out terrainComponent))
        {
            return terrainComponent is not null;
        }

        terrainComponent = null;
        return false;
    }

    internal class AssociatedData
    {
        public ObjectPlacementMap? LoadedObjectPlacementMap;

        public Entity? ObjectPlacementPreviewEntity;
        public ProceduralObjectPlacementComponent? ObjectPlacementComponent;

        public bool IsRegisteredTerrainChunkListener = false;

        public SetObjectPlacementObjectDataMessage? PendingSetObjectPlacements;
    }
}
