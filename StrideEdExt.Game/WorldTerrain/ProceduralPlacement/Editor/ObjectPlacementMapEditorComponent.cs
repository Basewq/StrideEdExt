using Stride.Core;
using Stride.Core.Assets;
using Stride.Engine;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Layers;

#if GAME_EDITOR
using Stride.Engine.Design;
using Stride.Games;
#endif

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;

public delegate ObjectPlacementMapRequestBase CreateEditorRequestDelegate(AssetId objectPlacementMapAssetId);

/// <remarks>
/// Make sure to place the entity with this component as a child entity of the entity with <see cref="Terrain3d.Editor.TerrainMapEditorComponent"/>
/// </remarks>
#if GAME_EDITOR
[DefaultEntityComponentProcessor(typeof(ObjectPlacementMapEditorProcessor), ExecutionMode = ExecutionMode.Editor)]
#endif
public class ObjectPlacementMapEditorComponent : SceneEditorExtBase, IObjectPlacementMapEditor
{
    private readonly List<CreateEditorRequestDelegate> _pendingEditorRequests = [];

    internal ObjectPlacementMapEditorProcessor? EditorProcessor;

    [DataMember(order: 10)]
    public ObjectPlacementMap? ObjectPlacementMap { get; set; }

#if GAME_EDITOR
    protected internal override void Initialize()
    {
        SendOrEnqueueEditorRequest(objectPlacementMapAssetId =>
        {
            var request = new ObjectPlacementMapEditorReadyRequest
            {
                ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                SceneId = Entity.Scene.Id,
                EditorEntityId = Entity.Id
            };
            return request;
        });
    }

    protected internal override void Deinitialize(Guid entityId, Guid? entitySceneId)
    {
        SendOrEnqueueEditorRequest(objectPlacementMapAssetId =>
        {
            var request = new ObjectPlacementMapEditorDestroyedRequest
            {
                ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                EditorEntityId = entityId,
                SceneId = entitySceneId,
            };
            return request;
        });
    }

    protected internal override void Update(GameTime gameTime)
    {
        if (ObjectPlacementMap is null || !EditorExtensions.IsRuntimeAssetLoaded(ObjectPlacementMap))
        {
            return;
        }
        // Send enqueued editor requests
        if (IsInitialized && RuntimeToEditorMessagingService is not null
            && _pendingEditorRequests.Count > 0)
        {
            foreach (var requestCreatorFunc in _pendingEditorRequests)
            {
                var request = requestCreatorFunc(ObjectPlacementMap.ObjectPlacementMapAssetId);
                RuntimeToEditorMessagingService.Send(request);
            }
            _pendingEditorRequests.Clear();
        }
    }
#endif

    [DataMemberIgnore]
    public ObjectDensityMapLayerComponentBase? ActiveDensityMapLayerComponent { get; private set; }
    internal void SetActiveLayer(ObjectDensityMapLayerComponentBase densityMapLayerComponent)
    {
        ActiveDensityMapLayerComponent = densityMapLayerComponent;
    }

    internal void UnsetActiveLayer(ObjectDensityMapLayerComponentBase densityMapLayerComponent)
    {
        if (ActiveDensityMapLayerComponent == densityMapLayerComponent)
        {
            ActiveDensityMapLayerComponent = null;
        }
    }

    public void SendOrEnqueueEditorRequest(CreateEditorRequestDelegate requestCreatorFunc)
    {
#if !GAME_EDITOR
        if (RuntimeToEditorMessagingService is null)
        {
            // Ignored
            return;
        }
#endif

        if (IsInitialized
            && ObjectPlacementMap is not null
            && RuntimeToEditorMessagingService is not null
            && EditorExtensions.IsRuntimeAssetLoaded(ObjectPlacementMap))
        {
            // Send immediately
            var request = requestCreatorFunc(ObjectPlacementMap.ObjectPlacementMapAssetId);
            RuntimeToEditorMessagingService.Send(request);
        }
        else
        {
            _pendingEditorRequests.Add(requestCreatorFunc);
        }
    }
}
