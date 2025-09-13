using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Painting;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.MaterialMaps;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using System.Diagnostics;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.MaterialWeightMaps;

public class PainterMaterialWeightMapLayerComponent : TerrainLayerComponentBase, ITerrainMapMaterialWeightMapLayer
{
    private IPainterService _painterService = default!;
    private MaterialPainterTool _materialPainterTool;
    private PaintSessionKey? _paintSessionKey;

    private TransformTRS _prevTransformData;

    private Array2d<Half>? _layerMaterialWeightMapData;
    private Int2 _layerMaterialWeightMapTexturePixelStartPosition;

    public override Type LayerDataType => typeof(PainterMaterialWeightMapLayerData);

    public string? MaterialName { get; set; }

    private PainterMaterialMapBrushSettings? _prevBrushSettings;
    [Display(Expand = ExpandRule.Once)]
    public PainterMaterialMapBrushSettings BrushSettings { get; set; } = new();

    public PainterMaterialWeightMapLayerComponent()
    {
        _materialPainterTool = new(this);
    }

    protected override void OnInitialize()
    {
        _painterService = Services.GetSafeServiceAs<IPainterService>();
        _materialPainterTool.Initialize(Services);

        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnDeinitialize()
    {
        DisposableExtensions.DisposeAndNull(ref _paintSessionKey);

        _materialPainterTool.Deinitialize();
    }

    protected override void OnActivateLayerEditMode()
    {
        if (Entity.TryFindComponentOnAncestor<TerrainMapEditorComponent>(out var editorComp))
        {
            var editorEntityId = editorComp.Entity.Id;
            var paintSessionId = _painterService.BeginSession(editorEntityId);
            _paintSessionKey = new PaintSessionKey(_painterService, paintSessionId);
            UpdateBrushSettingsChanges();
            _painterService.SetActiveTool(paintSessionId, _materialPainterTool);
            _painterService.SetActiveSessionId(paintSessionId);
        }
        else
        {
            Debug.WriteLine($"Editor Component not found on Layer {GetType().Name}: {Id}");
        }
    }

    protected override void OnDeactivateLayerEditMode()
    {
        DisposableExtensions.DisposeAndNull(ref _paintSessionKey);
    }

    protected override void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
        UpdateBrushSettingsChanges();

        var curTransformData = Entity.GetTransformTRS();
        if (!_prevTransformData.IsSame(curTransformData))
        {
            if (!TryGetTerrainMap(out var terrainMap, out var terrainEntity))
            {
                return;
            }

            var texWorldMeasurement = new TextureWorldMeasurement(terrainEntity.Transform.Position.XZ(), terrainMap.MeshQuadSize);
            var startingIndex = texWorldMeasurement.GetTextureCoordsXZ(Entity.Transform.Position);
            EditorComponent?.SendOrEnqueueEditorRequest(terrainMapAssetId =>
            {
                var request = new UpdateMaterialWeightMapTextureStartPositionRequest
                {
                    TerrainMapAssetId = terrainMapAssetId,
                    LayerId = LayerId,
                    MaterialWeightMapTexturePixelStartPosition = startingIndex
                };
                return request;
            });

            _prevTransformData = curTransformData;
        }
    }

    private void UpdateBrushSettingsChanges()
    {
        if (_paintSessionKey is not null && BrushSettings is not null
            && (_prevBrushSettings != BrushSettings || BrushSettings.HasChanged))
        {
            BrushSettings.CopyTo(_materialPainterTool.BrushSettings);
            _prevBrushSettings = BrushSettings;
            BrushSettings.HasChanged = false;
        }
    }

    internal void UpdateData(Array2d<Half> layerMaterialWeightMapData, Int2? layerMaterialWeightMapTexturePixelStartPosition)
    {
        _layerMaterialWeightMapData = layerMaterialWeightMapData;
        _layerMaterialWeightMapTexturePixelStartPosition = layerMaterialWeightMapTexturePixelStartPosition ?? _layerMaterialWeightMapTexturePixelStartPosition;
    }

    private class MaterialPainterTool : PainterToolBase
    {
        private readonly PainterMaterialWeightMapLayerComponent _parent;

        public MaterialPainterTool(PainterMaterialWeightMapLayerComponent parent)
        {
            _parent = parent;
        }

        public override PaintBrushSettings BrushSettings { get; } = new PaintBrushSettings();

        protected override void OnDeactivate()
        {
            if (_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
               && terrainMapEditor.TryGetTerrainComponent(out var terrainComponent))
            {
                terrainComponent.EndPaintableMeshTargets([]);
            }
        }

        public override void GetAndPrepareTargetEntityMeshAndPaintRenderTargetMap(Dictionary<PaintTargetEntityMesh, PaintRenderTargetTexture> targetEntityMeshAndRenderTargetMapOutput)
        {
            if (!_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
                || !terrainMapEditor.TryGetTerrainComponent(out var terrainComponent))
            {
                return;
            }
            var materialName = _parent.MaterialName;
            if (terrainMapEditor.TryGetMaterialIndex(materialName, out byte materialIndex))
            {
                terrainComponent.SetMaterialMapPaintModeType(_parent.BrushSettings.PaintModeType);
                if (_parent._layerMaterialWeightMapData is not null)
                {
                    terrainComponent.SetEditLayerMaterialWeightMap(_parent._layerMaterialWeightMapData);
                }
                terrainMapEditor.PrepareVisiblePaintableMaterialMeshTargetAndRenderTargetMap(targetEntityMeshAndRenderTargetMapOutput, materialIndex);
            }
        }

        protected override void OnPaintStarted(BrushPoint strokeMapBrushPoint)
        {
            if (!_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
                || !terrainMapEditor.TryGetTerrainComponent(out var terrainComponent))
            {
                return;
            }
            terrainComponent.SetIsPaintingActive(true);
            terrainComponent.SetInitialBrushWorldPosition(strokeMapBrushPoint.WorldPosition);
        }

        protected override void OnPaintCompleted(List<BrushPoint> strokeMapBrushPoints)
        {
            if (!_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
                || terrainMapEditor.TerrainMap is not TerrainMap terrainMap)
            {
                return;
            }
            var paintableTerrainMeshResultMap = new Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshResultData>();
            terrainMapEditor.CompletePaintableMeshTargetAndRenderTargetMap(paintableTerrainMeshResultMap);

            var IndexOffsetEast = new Int2(1, 0);
            var IndexOffsetSouth = new Int2(0, 1);

            var paintModeType = _parent.BrushSettings.PaintModeType;
            var weightMapAdjustmentRegions = new List<MaterialWeightMapAdjustmentRegionRequest>();
            foreach (var (targetEntityMesh, meshData) in paintableTerrainMeshResultMap)
            {
                var heightmapTextureRegion = meshData.HeightmapTextureRegion;
                var startPosition = new Int2(heightmapTextureRegion.X, heightmapTextureRegion.Y);

                if (!terrainMap.TryGetChunk(meshData.ChunkIndex, out var chunk))
                {
                    // Chunk disappeared?
                    continue;
                }

                // Check if this mesh is adjacent to another mesh at the east/south ends.
                // If they are, then the edge is an overlapping point, so we should discard them
                // to ensure they are only adjusted once.
                var writableAdjustmentRegionSize = meshData.StrokeMapData.Length2d;
                var chunkIndex = meshData.ChunkIndex;
                var chunkSubCellIndex = meshData.ChunkSubCellIndex;
                if (HasMesh(terrainMap, chunkIndex, chunkSubCellIndex + IndexOffsetEast)
                    || HasMesh(terrainMap, chunkIndex + IndexOffsetEast, Int2.Zero))
                {
                    writableAdjustmentRegionSize.Width--;
                }
                if (HasMesh(terrainMap, chunkIndex, chunkSubCellIndex + IndexOffsetSouth)
                    || HasMesh(terrainMap, chunkIndex + IndexOffsetSouth, Int2.Zero))
                {
                    writableAdjustmentRegionSize.Height--;
                }
                var adjustmentWeightMapData = meshData.StrokeMapData;
                if (adjustmentWeightMapData.Length2d != writableAdjustmentRegionSize)
                {
                    adjustmentWeightMapData.Resize(writableAdjustmentRegionSize);
                }

                bool hasNonZeroValue = adjustmentWeightMapData.Contains((in float x) => x != 0);
                if (!hasNonZeroValue)
                {
                    continue;
                }
                if (paintModeType == MaterialMapPaintModeType.Erase)
                {
                    for (int y = 0; y < adjustmentWeightMapData.LengthY; y++)
                    {
                        for (int x = 0; x < adjustmentWeightMapData.LengthX; x++)
                        {
                            adjustmentWeightMapData[x, y] = -adjustmentWeightMapData[x, y];
                        }
                    }
                }
                var adjustRegionReq = new MaterialWeightMapAdjustmentRegionRequest
                {
                    AdjustmentWeightMapData = adjustmentWeightMapData,
                    StartPosition = startPosition
                };
                weightMapAdjustmentRegions.Add(adjustRegionReq);
            }

            if (weightMapAdjustmentRegions.Count > 0)
            {
                var layerId = _parent.LayerId;
                terrainMapEditor.CommitPaintableMaterialMeshTargetAndRenderTargetChanges(layerId, weightMapAdjustmentRegions);
            }
        }
    }
}
