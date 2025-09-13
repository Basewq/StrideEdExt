using Stride.Core;
using Stride.Core.Assets;
using Stride.Engine;
using Stride.Graphics;
using StrideEdExt.Painting;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.Terrain3d.Layers;
using System.Diagnostics.CodeAnalysis;

#if GAME_EDITOR
using Stride.Engine.Design;
using Stride.Games;
#endif

namespace StrideEdExt.WorldTerrain.Terrain3d.Editor;

[DataContract]
public enum TerrainDisplayMode
{
    Normal,
    DebugMode,
}

public record TerrainMapMaterialLayerData(string MaterialName, byte MaterialIndex);

public delegate TerrainMapRequestBase CreateEditorRequestDelegate(AssetId terrainMapAssetId);

#if GAME_EDITOR
[DefaultEntityComponentProcessor(typeof(TerrainMapEditorProcessor), ExecutionMode = ExecutionMode.Editor)]
#endif
public class TerrainMapEditorComponent : SceneEditorExtBase, ITerrainMapEditor
{
    private readonly List<CreateEditorRequestDelegate> _pendingEditorRequests = [];

    internal TerrainMapEditorProcessor? EditorProcessor;

    [DataMember(order: 10)]
    public TerrainMap? TerrainMap { get; set; }

    [DataMember(order: 20)]
    public Prefab? PaintPlacementPreviewPrefab;

#if GAME_EDITOR
    protected internal override void Initialize()
    {
        SendOrEnqueueEditorRequest(terrainMapAssetId =>
        {
            var request = new TerrainMapEditorReadyRequest
            {
                TerrainMapAssetId = terrainMapAssetId,
                SceneId = Entity.Scene.Id,
                EditorEntityId = Entity.Id
            };
            return request;
        });
    }

    protected internal override void Deinitialize()
    {
        SendOrEnqueueEditorRequest(terrainMapAssetId =>
        {
            var request = new TerrainMapEditorDestroyedRequest
            {
                TerrainMapAssetId = terrainMapAssetId,
                SceneId = Entity.Scene.Id,
                EditorEntityId = Entity.Id
            };
            return request;
        });
    }

    protected internal override void Update(GameTime gameTime)
    {
        if (TerrainMap is null || !EditorExtensions.IsRuntimeAssetLoaded(TerrainMap))
        {
            return;
        }
        // Send enqueued editor requests
        if (IsInitialized && RuntimeToEditorMessagingService is not null
            && _pendingEditorRequests.Count > 0)
        {
            foreach (var requestCreatorFunc in _pendingEditorRequests)
            {
                var request = requestCreatorFunc(TerrainMap.TerrainMapAssetId);
                RuntimeToEditorMessagingService.Send(request);
            }
            _pendingEditorRequests.Clear();
        }
    }
#endif

    [DataMemberIgnore]
    public TerrainLayerComponentBase? ActiveTerrainLayerComponent { get; private set; }
    internal void SetActiveLayer(TerrainLayerComponentBase terrainLayerComponent)
    {
        ActiveTerrainLayerComponent = terrainLayerComponent;
    }

    internal void UnsetActiveLayer(TerrainLayerComponentBase terrainLayerComponent)
    {
        if (ActiveTerrainLayerComponent == terrainLayerComponent)
        {
            ActiveTerrainLayerComponent = null;
        }
    }

    [DataMemberIgnore]
    public List<TerrainMapMaterialLayerData> MaterialLayers { get; } = [];
    internal void UpdateMaterialLayerList(IEnumerable<TerrainMapMaterialLayerData> materialLayers)
    {
        MaterialLayers.Clear();
        MaterialLayers.AddRange(materialLayers);
    }

    internal bool TryGetMaterialIndex(string? materialName, out byte materialIndex)
    {
        if (!string.IsNullOrEmpty(materialName))
        {
            for (int i = 0; i < MaterialLayers.Count; i++)
            {
                if (string.Equals(materialName, MaterialLayers[i].MaterialName, StringComparison.OrdinalIgnoreCase))
                {
                    materialIndex = (byte)i;
                    return true;
                }
            }
        }
        materialIndex = 0;
        return false;
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
            && TerrainMap is not null
            && RuntimeToEditorMessagingService is not null
            && EditorExtensions.IsRuntimeAssetLoaded(TerrainMap))
        {
            // Send immediately
            var request = requestCreatorFunc(TerrainMap.TerrainMapAssetId);
            RuntimeToEditorMessagingService.Send(request);
        }
        else
        {
            _pendingEditorRequests.Add(requestCreatorFunc);
        }
    }

    public bool TryGetTerrainComponent([NotNullWhen(true)] out TerrainComponent? terrainComponent)
    {
        terrainComponent = null;
        if (EditorProcessor is not null
            && EditorProcessor.TryGetTerrainComponent(this, out terrainComponent))
        {
            return true;
        }
        return false;
    }

    private readonly Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshData> _paintableTerrainMeshMapProcessing = [];
    internal void PrepareVisiblePaintableHeightmapMeshTargetAndRenderTargetMap(
        Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> targetEntityMeshAndRenderTargetMapOutput)
    {
        if (TryGetTerrainComponent(out var terrainComponent))
        {
            terrainComponent.GetOrCreateVisiblePaintableTerrainMeshMapForHeightmapEdit(_paintableTerrainMeshMapProcessing);
            foreach (var (targetEntityMesh, meshData) in _paintableTerrainMeshMapProcessing)
            {
                targetEntityMeshAndRenderTargetMapOutput[targetEntityMesh] = meshData.StrokeMapRenderTarget;
            }
            _paintableTerrainMeshMapProcessing.Clear();
        }
    }

    internal void PrepareVisiblePaintableMaterialMeshTargetAndRenderTargetMap(
        Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> targetEntityMeshAndRenderTargetMapOutput,
        byte overrideMaterialIndex)
    {
        if (TryGetTerrainComponent(out var terrainComponent))
        {
            terrainComponent.GetOrCreateVisiblePaintableTerrainMeshMapForMaterialEdit(_paintableTerrainMeshMapProcessing, overrideMaterialIndex);
            foreach (var (targetEntityMesh, meshData) in _paintableTerrainMeshMapProcessing)
            {
                targetEntityMeshAndRenderTargetMapOutput[targetEntityMesh] = meshData.StrokeMapRenderTarget;
            }
            _paintableTerrainMeshMapProcessing.Clear();
        }
    }

    internal void CompletePaintableMeshTargetAndRenderTargetMap(
        Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshResultData> targetEntityMeshAndRenderTargetMapOutput)
    {
        if (TryGetTerrainComponent(out var terrainComponent))
        {
            terrainComponent.EndPaintableMeshTargets(targetEntityMeshAndRenderTargetMapOutput);
        }
    }

    internal void CommitPaintableHeightmapMeshTargetAndRenderTargetChanges(Guid layerId, List<HeightmapAdjustmentRegionRequest> heightmapAdjustmentRegions)
    {
        SendOrEnqueueEditorRequest(terrainMapAssetId =>
        {
            var request = new AdjustPainterHeightmapRequest
            {
                TerrainMapAssetId = terrainMapAssetId,
                LayerId = layerId,
                HeightmapAdjustmentRegions = heightmapAdjustmentRegions,
            };
            return request;
        });
    }

    internal void CommitPaintableMaterialMeshTargetAndRenderTargetChanges(Guid layerId, List<MaterialWeightMapAdjustmentRegionRequest> weightMapAdjustmentRegions)
    {
        SendOrEnqueueEditorRequest(terrainMapAssetId =>
        {
            var request = new AdjustPainterMaterialWeightMapRequest
            {
                TerrainMapAssetId = terrainMapAssetId,
                LayerId = layerId,
                WeightMapAdjustmentRegions = weightMapAdjustmentRegions,
            };
            return request;
        });
    }
}
