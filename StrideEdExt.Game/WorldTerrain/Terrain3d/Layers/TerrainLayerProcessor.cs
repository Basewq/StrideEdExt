using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using Stride.Core;
using SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;
using SceneEditorExtensionExample.StrideEditorExt;
using SceneEditorExtensionExample.StrideAssetExt.Assets;

#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
#endif

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;

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
#endif

        EntityManager.EntityAdded += OnEntityAdded;
        EntityManager.EntityRemoved += OnEntityRemoved;
    }

    private void OnEntityAdded(object? sender, Entity e)
    {
    }

    private void OnEntityRemoved(object? sender, Entity e)
    {
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
        component.LayerChanged += OnLayerChanged;
    }

    private void OnLayerChanged(object? sender, System.EventArgs e)
    {
        if (sender is not TerrainLayerComponentBase layerComp)
        {
            return;
        }
        if (ComponentDatas.TryGetValue(layerComp, out var data))
        {
            if (data.PainterComponent is null && layerComp.Entity.TryFindComponentOnAncestor<TerrainMapPainterComponent>(out var painterComp))
            {
                data.PainterComponent = painterComp;
            }
            if (data.PainterComponent is not null)
            {
                data.PainterComponent.RebuildMap();
            }
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainLayerComponentBase component, [NotNull] AssociatedData data)
    {
        component.LayerChanged -= OnLayerChanged;
        component.Deinitialize();
#if GAME_EDITOR
        if (data.PainterComponent is not null)
        {
            var terrainMapAsset = data.PainterComponent.GetTerrainInternalAsset();
            if (terrainMapAsset is not null)
            {
                component.UnregisterLayerMetadata(terrainMapAsset);
                if (terrainMapAsset.HasLayerMetadataListChanged)
                {
                    var strideEditorService = Services.GetSafeServiceAs<IStrideEditorService>();
                    strideEditorService.Invoke(() =>
                    {
                        if (strideEditorService.IsActive)   // Need to check IsActive again because the editor might be shutting down
                        {
                            strideEditorService.RefreshAssetCollection(terrainMapAsset, TerrainMapAsset.LayerMetadataListName);
                        }
                    });
                    terrainMapAsset.HasLayerMetadataListChanged = false;
                }
            }
        }
#endif
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
        public TerrainMapPainterComponent? PainterComponent;
    }
}
