using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers;

[ComponentCategory("Terrain")]
[DataContract(Inherited = true)]
[DefaultEntityComponentProcessor(typeof(TerrainLayerProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
public abstract class TerrainLayerComponentBase : EntityComponent
{
#if GAME_EDITOR
    private static readonly TimeSpan NextRetryTime = TimeSpan.FromSeconds(3);
    private DateTime _getTerrainMapNextRetryTime = DateTime.MinValue;
#endif

    protected IServiceRegistry Services { get; private set; } = default!;
    protected internal TerrainMapEditorComponent? EditorComponent { get; set; }

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    [DataMemberIgnore]
    public Guid LayerId => Id;      // Use the entity component's ID as the layer ID

    [DataMemberIgnore]
    public abstract Type LayerDataType { get; }

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
        if (terrainMap is null && EditorComponent is not null)
        {
            terrainEntity = EditorComponent.Entity;
            terrainMap = EditorComponent.TerrainMap;
        }

        if (terrainMap is not null && !EnsureLoadedTerrainMap(terrainMap))
        {
            return false;
        }
#endif

        return terrainMap is not null && terrainEntity is not null;
    }

    protected bool TryGetTerrainMapEditor([NotNullWhen(true)] out TerrainMapEditorComponent? terrainMapEditor)
    {
        terrainMapEditor = null;
#if GAME_EDITOR
        if (_getTerrainMapNextRetryTime > DateTime.Now)
        {
            return false;
        }
#endif

        if (Entity.TryFindComponentOnAncestor<TerrainMapEditorComponent>(out var painterComp))
        {
            if (EnsureLoadedTerrainMap(painterComp?.TerrainMap))
            {
                terrainMapEditor = painterComp;
            }
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

    protected static bool HasMesh(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex, TerrainChunkSubCellIndex2d chunkSubCellIndex)
    {
        if (terrainMap.TryGetChunk(chunkIndex, out var chunk)
            && chunk.TryGetSubChunk(chunkSubCellIndex, out var subChunk))
        {
            return subChunk.Mesh is not null;
        }
        return false;
    }
}
