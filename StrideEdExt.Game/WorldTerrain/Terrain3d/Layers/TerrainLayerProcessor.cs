using Stride.Core;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;

#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
#endif

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers;

class TerrainLayerProcessor : EntityProcessor<TerrainLayerComponentBase, TerrainLayerProcessor.AssociatedData>
{
#if GAME_EDITOR
    private SceneEditorGame _sceneEditorGame = default!;
#endif

    public TerrainLayerProcessor()
    {
        Order = 100000;     // Make this processor's update call after any camera position changes
    }

    protected override void OnSystemAdd()
    {
#if GAME_EDITOR
        _sceneEditorGame = (Services.GetSafeServiceAs<IGame>() as SceneEditorGame)!;

        var selectionService = _sceneEditorGame.EditorServices.Get<IEditorGameEntitySelectionService>();
        if (selectionService is not null)
        {
            selectionService.SelectionUpdated += EntitySelectionService_OnSelectionUpdated;
        }
#endif
    }

#if GAME_EDITOR
    private void EntitySelectionService_OnSelectionUpdated(object? sender, EntitySelectionEventArgs e)
    {
        // Stop painting if the user changed entity selection
        var deselectedLayers = ComponentDatas
                                    .Select(x => x.Key)
                                    .Where(layerComp => !e.NewSelection.Any(selectedEntity => selectedEntity.Id == layerComp.Entity.Id))    // All layers that are not selected
                                    .ToList();
        if (deselectedLayers.Count > 0)
        {
            foreach (var layerComp in deselectedLayers)
            {
                layerComp.DeactivateLayerEditMode();
            }
        }

        // Only one can be active at a time
        if (e.NewSelection.Count == 1)
        {
            var selectedLayer = ComponentDatas
                                    .Select(x => x.Key)
                                    .Where(layerComp => e.NewSelection.Any(selectedEntity => selectedEntity.Id == layerComp.Entity.Id))     // All layers that are selected
                                    .FirstOrDefault();
            if (selectedLayer is not null)
            {
                selectedLayer.ActivateLayerEditMode();
            }
        }
    }
#endif

    protected override void OnSystemRemove()
    {
#if GAME_EDITOR
        var selectionService = _sceneEditorGame.EditorServices.Get<IEditorGameEntitySelectionService>();
        if (selectionService is not null)
        {
            selectionService.SelectionUpdated -= EntitySelectionService_OnSelectionUpdated;
        }
#endif
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] TerrainLayerComponentBase component)
    {
        return new AssociatedData
        {
        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] TerrainLayerComponentBase component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] TerrainLayerComponentBase component, [NotNull] AssociatedData data)
    {
        component.Initialize(Services);

        if (entity.TryFindComponentOnAncestor<TerrainMapEditorComponent>(out var editorComp))
        {
            component.EditorComponent = editorComp;

            editorComp.SendOrEnqueueEditorRequest(terrainMapAssetId =>
            {
                var request = new GetOrCreateLayerDataRequest
                {
                    TerrainMapAssetId = terrainMapAssetId,
                    LayerId = component.Id,
                    LayerDataType = component.LayerDataType
                };
                return request;
            });
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainLayerComponentBase component, [NotNull] AssociatedData data)
    {
        component.Deinitialize();
        if (component.EditorComponent is TerrainMapEditorComponent editorComp)
        {
            if (editorComp.ActiveTerrainLayerComponent == component)
            {
                component.DeactivateLayerEditMode();
            }
        }
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
#if GAME_EDITOR
            // Chunk culling should be done on the editor's camera when in the editor
            var cameraService = _sceneEditorGame.EditorServices.Get<IEditorGameCameraService>();
            overrideCameraComponent = cameraService?.Component;
#endif
            comp.UpdateForDraw(context.Time, overrideCameraComponent);
        }
    }

    public class AssociatedData
    {
        public bool HasLayerChanged = false;
    }
}
