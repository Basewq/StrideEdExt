using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;
using StrideEdExt.WorldTerrain.Terrain3d;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers;

[ComponentCategory("Procedural Density Maps")]
[DataContract(Inherited = true)]
[DefaultEntityComponentProcessor(typeof(ObjectDensityMapLayerProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
public abstract class ObjectDensityMapLayerComponentBase : EntityComponent, IObjectDensityMapLayer
{
#if GAME_EDITOR
    private static readonly TimeSpan NextRetryTime = TimeSpan.FromSeconds(3);
    private DateTime _getTerrainMapNextRetryTime = DateTime.MinValue;
#endif

    protected IServiceRegistry Services { get; private set; } = default!;
    protected internal ObjectPlacementMapEditorComponent? EditorComponent { get; set; }

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    [DataMemberIgnore]
    public Guid LayerId => Id;      // Use the entity component's ID as the layer ID

    [DataMemberIgnore]
    public abstract Type LayerDataType { get; }

    public abstract ObjectDensityMapBlendType BlendType { get; set; }
    public abstract bool IsInverted { get; set; }

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

    public void ActivateLayerEditMode()
    {
        EditorComponent?.SetActiveLayer(this);
        OnActivateLayerEditMode();
    }

    protected virtual void OnActivateLayerEditMode() { }

    public void DeactivateLayerEditMode()
    {
        OnDeactivateLayerEditMode();
        EditorComponent?.UnsetActiveLayer(this);
    }

    protected virtual void OnDeactivateLayerEditMode() { }

    protected virtual void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent) { }

    public void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
        OnUpdateForDraw(time, overrideCameraComponent);
    }
    protected virtual void OnUpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent) { }

    protected bool TryGetObjectPlacementMapEditor([NotNullWhen(true)] out ObjectPlacementMapEditorComponent? objectPlacementMapEditor)
    {
        objectPlacementMapEditor = EditorComponent;
        return objectPlacementMapEditor is not null;
    }

    protected bool TryGetTerrainMap([NotNullWhen(true)] out TerrainMap? terrainMap, [NotNullWhen(true)] out Entity? terrainEntity)
    {
        terrainMap = null;
        terrainEntity = null;

#if GAME_EDITOR
        if (_getTerrainMapNextRetryTime > DateTime.Now)
        {
            return false;
        }
#endif
        if (Entity.TryFindComponentOnAncestor<TerrainComponent>(out var terrainComp))
        {
            terrainEntity = terrainComp.Entity;
            terrainMap = terrainComp.TerrainMap;
        }
#if GAME_EDITOR
        if (terrainMap is null && TryGetTerrainMapEditor(out var terrainMapEditorComp))
        {
            terrainEntity = terrainMapEditorComp.Entity;
            terrainMap = terrainMapEditorComp.TerrainMap;
        }

        if (terrainMap is not null && !EnsureLoadedTerrainMap(terrainMap))
        {
            return false;
        }
#endif

        return terrainMap is not null && terrainEntity is not null;
    }

    private TerrainMapEditorComponent? _terrainMapEditorComponent;
    protected bool TryGetTerrainMapEditor([NotNullWhen(true)] out TerrainMapEditorComponent? terrainMapEditor)
    {
        terrainMapEditor = _terrainMapEditorComponent;
        if (terrainMapEditor is not null)
        {
            return true;
        }
#if GAME_EDITOR
        if (_getTerrainMapNextRetryTime > DateTime.Now)
        {
            return false;
        }
#endif

        if (!Entity.TryFindComponentOnAncestor<TerrainMapEditorComponent>(out var terrainMapEditorComp))
        {
            foreach (var rootEnt in Entity.Scene.Entities)
            {
                if (rootEnt.TryFindComponentOnSelfOrDescendant<TerrainMapEditorComponent>(out terrainMapEditorComp))
                {
                    break;
                }
            }
        }
        if (terrainMapEditorComp is not null && EnsureLoadedTerrainMap(terrainMapEditorComp.TerrainMap))
        {
            terrainMapEditor = terrainMapEditorComp;
            _terrainMapEditorComponent = terrainMapEditorComp;
        }

        return terrainMapEditor is not null;
    }

    private bool EnsureLoadedTerrainMap(TerrainMap? terrainMap)
    {
        if (terrainMap is not null && !EditorExtensions.IsRuntimeAssetLoaded(terrainMap))
        {
            Debug.WriteLine("TerrainHeightmapLayerComponentBase: Editor Content Manager Terrain Map not ready. Scheduling retry.");
#if GAME_EDITOR
            _getTerrainMapNextRetryTime = DateTime.Now + NextRetryTime;
#endif
            return false;
        }
        return terrainMap is not null;
    }
}
