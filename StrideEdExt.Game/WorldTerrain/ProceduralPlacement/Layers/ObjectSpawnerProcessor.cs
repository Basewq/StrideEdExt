using Stride.Core;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;

#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
#endif

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers;

class ObjectSpawnerProcessor : EntityProcessor<ObjectSpawnerComponentBase, ObjectSpawnerProcessor.AssociatedData>
{
#if GAME_EDITOR
    private SceneEditorGame _sceneEditorGame = default!;
#endif

    public ObjectSpawnerProcessor()
    {
        Order = 100000;     // Make this processor's update call after any camera position changes
    }

    protected override void OnSystemAdd()
    {
#if GAME_EDITOR
        _sceneEditorGame = (Services.GetSafeServiceAs<IGame>() as SceneEditorGame)!;
#endif
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] ObjectSpawnerComponentBase component)
    {
        return new AssociatedData
        {
        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] ObjectSpawnerComponentBase component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] ObjectSpawnerComponentBase component, [NotNull] AssociatedData data)
    {
        component.Initialize(Services);

        if (entity.TryFindComponentOnAncestor<ObjectPlacementMapEditorComponent>(out var editorComp))
        {
            component.EditorComponent = editorComp;

            editorComp.SendOrEnqueueEditorRequest(objectPlacementMapAssetId =>
            {
                var request = new GetOrCreateObjectPlacementLayerDataRequest
                {
                    ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                    LayerId = component.Id,
                    LayerDataType = component.LayerDataType
                };
                return request;
            });
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ObjectSpawnerComponentBase component, [NotNull] AssociatedData data)
    {
        component.Deinitialize();
    }

    public override void Update(GameTime time)
    {
        foreach (var (comp, data) in ComponentDatas)
        {
            CameraComponent? overrideCameraComponent = null;
#if GAME_EDITOR
            // Chunk culling should be done on the editor's camera when in the editor
            var cameraService = _sceneEditorGame.EditorServices.Get<IEditorGameCameraService>();
            overrideCameraComponent = cameraService?.Component;
#endif
            comp.Update(time, overrideCameraComponent);
        }
    }

    public override void Draw(RenderContext context)
    {
        foreach (var (comp, data) in ComponentDatas)
        {
            CameraComponent? overrideCameraComponent = null;
////#if GAME_EDITOR
////            // Chunk culling should be done on the editor's camera when in the editor
////            var cameraService = _sceneEditorGame.EditorServices.Get<IEditorGameCameraService>();
////            overrideCameraComponent = cameraService?.Component;
////#endif
            comp.UpdateForDraw(context.Time, overrideCameraComponent);
        }
    }

    public class AssociatedData
    {
        public bool HasLayerChanged = false;
    }
}
