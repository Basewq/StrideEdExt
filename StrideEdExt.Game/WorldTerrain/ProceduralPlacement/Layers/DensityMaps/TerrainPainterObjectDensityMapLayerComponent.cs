using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using StrideEdExt.Painting;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.DensityMaps;
using StrideEdExt.WorldTerrain.ProceduralPlacement.Editor;
using StrideEdExt.WorldTerrain.Terrain3d;
using System.Diagnostics;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.DensityMaps;

public class TerrainPainterObjectDensityMapLayerComponent : ObjectDensityMapLayerComponentBase
{
    private IPainterService _painterService = default!;
    private DensityMapPainterTool _densityMapPainterTool;
    private PaintSessionKey? _paintSessionKey;

    private TransformTRS _prevTransformData;
    private bool _isDensityMapDataUpdateRequired;

    private Array2d<Half>? _layerDensityMapData;
    //private Int2 _layerDensityMapTexturePixelStartPosition;

    public override Type LayerDataType => typeof(PainterObjectDensityMapLayerData);

    private ObjectDensityMapBlendType _blendType = ObjectDensityMapBlendType.Multiply;
    public override ObjectDensityMapBlendType BlendType
    {
        get => _blendType;
        set
        {
            bool hasChanged = _blendType != value;
            _blendType = value;
            if (IsInitialized && hasChanged)
            {
                _isDensityMapDataUpdateRequired = true;
            }
        }
    }

    private bool _isInverted = false;
    public override bool IsInverted
    {
        get => _isInverted;
        set
        {
            bool hasChanged = _isInverted != value;
            _isInverted = value;
            if (IsInitialized && hasChanged)
            {
                _isDensityMapDataUpdateRequired = true;
            }
        }
    }

    private TerrainPainterDensityMapBrushSettings? _prevBrushSettings;
    [Display(Expand = ExpandRule.Once)]
    public TerrainPainterDensityMapBrushSettings BrushSettings { get; set; } = new();

    public TerrainPainterObjectDensityMapLayerComponent()
    {
        _densityMapPainterTool = new(this);
    }

    protected override void OnInitialize()
    {
        _painterService = Services.GetSafeServiceAs<IPainterService>();
        _densityMapPainterTool.Initialize(Services);

        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnDeinitialize()
    {
        DisposableExtensions.DisposeAndNull(ref _paintSessionKey);

        _densityMapPainterTool.Deinitialize();
    }

    protected override void OnActivateLayerEditMode()
    {
        if (TryGetTerrainMapEditor(out var editorComp))
        {
            var editorEntityId = editorComp.Entity.Id;
            var paintSessionId = _painterService.BeginSession(editorEntityId);
            _paintSessionKey = new PaintSessionKey(_painterService, paintSessionId);
            UpdateBrushSettingsChanges();
            _painterService.SetActiveTool(paintSessionId, _densityMapPainterTool);
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
        if (!_prevTransformData.IsSame(curTransformData) || _isDensityMapDataUpdateRequired)
        {
            if (!TryGetTerrainMap(out var terrainMap, out var terrainEntity)
                || !TryGetObjectPlacementMapEditor(out var objectPlacementMapEditor))
            {
                return;
            }

            var texWorldMeasurement = new TextureWorldMeasurement(terrainEntity.Transform.Position.XZ(), terrainMap.MeshQuadSize);
            var startingIndex = texWorldMeasurement.GetTextureCoordsXZ(Entity.Transform.Position);
            objectPlacementMapEditor.SendOrEnqueueEditorRequest(objectDensityMapAssetId =>
            {
                var request = new UpdateObjectDensityMapTextureStartPositionRequest
                {
                    ObjectPlacementMapAssetId = objectDensityMapAssetId,
                    LayerId = LayerId,
                    ObjectDensityMapTexturePixelStartPosition = startingIndex
                };
                return request;
            });

            _prevTransformData = curTransformData;
            _isDensityMapDataUpdateRequired = false;
        }
    }

    private void UpdateBrushSettingsChanges()
    {
        if (_paintSessionKey is not null && BrushSettings is not null
            && (_prevBrushSettings != BrushSettings || BrushSettings.HasChanged))
        {
            BrushSettings.CopyTo(_densityMapPainterTool.BrushSettings);
            _prevBrushSettings = BrushSettings;
            BrushSettings.HasChanged = false;
        }
    }

    internal void UpdateData(Array2d<Half> layerDensityMapData, Int2? layerDensityMapTexturePixelStartPosition)
    {
        _layerDensityMapData = layerDensityMapData;
        //_layerDensityMapTexturePixelStartPosition = layerDensityMapTexturePixelStartPosition ?? _layerDensityMapTexturePixelStartPosition;
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

    private class DensityMapPainterTool : PainterToolBase
    {
        private readonly TerrainPainterObjectDensityMapLayerComponent _parent;

        public DensityMapPainterTool(TerrainPainterObjectDensityMapLayerComponent parent)
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

            terrainComponent.SetObjectDensityMapPaintModeType(_parent.BrushSettings.PaintModeType);
            if (_parent._layerDensityMapData is not null)
            {
                terrainComponent.SetEditLayerObjectDensityMap(_parent._layerDensityMapData);
            }
            terrainMapEditor.PrepareVisiblePaintableObjectPlacementMeshTargetAndRenderTargetMap(targetEntityMeshAndRenderTargetMapOutput);
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
                || terrainMapEditor.TerrainMap is not TerrainMap terrainMap
                || !_parent.TryGetObjectPlacementMapEditor(out var objectPlacementMapEditor))
            {
                return;
            }
            var paintableTerrainMeshResultMap = new Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshResultData>();
            terrainMapEditor.CompletePaintableMeshTargetAndRenderTargetMap(paintableTerrainMeshResultMap);

            var IndexOffsetEast = new Int2(1, 0);
            var IndexOffsetSouth = new Int2(0, 1);

            var paintModeType = _parent.BrushSettings.PaintModeType;
            var densityMapAdjustmentRegions = new List<ObjectDensityMapAdjustmentRegionRequest>();
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
                var adjustmentDensityMapData = meshData.StrokeMapData;
                if (adjustmentDensityMapData.Length2d != writableAdjustmentRegionSize)
                {
                    adjustmentDensityMapData.Resize(writableAdjustmentRegionSize);
                }

                bool hasNonZeroValue = adjustmentDensityMapData.Contains((in float x) => x != 0);
                if (!hasNonZeroValue)
                {
                    continue;
                }
                if (paintModeType == ObjectDensityMapPaintModeType.Erase)
                {
                    for (int y = 0; y < adjustmentDensityMapData.LengthY; y++)
                    {
                        for (int x = 0; x < adjustmentDensityMapData.LengthX; x++)
                        {
                            adjustmentDensityMapData[x, y] = -adjustmentDensityMapData[x, y];
                        }
                    }
                }
                var adjustRegionReq = new ObjectDensityMapAdjustmentRegionRequest
                {
                    AdjustmentObjectDensityMapData = adjustmentDensityMapData,
                    StartPosition = startPosition
                };
                densityMapAdjustmentRegions.Add(adjustRegionReq);
            }

            if (densityMapAdjustmentRegions.Count > 0)
            {
                var layerId = _parent.LayerId;
                CommitPaintableObjectPlacementMeshTargetAndRenderTargetChanges(objectPlacementMapEditor, layerId, densityMapAdjustmentRegions);
            }
        }

        private void CommitPaintableObjectPlacementMeshTargetAndRenderTargetChanges(
            ObjectPlacementMapEditorComponent objectPlacementMapEditor,
            Guid layerId, List<ObjectDensityMapAdjustmentRegionRequest> densityMapAdjustmentRegions)
        {
            objectPlacementMapEditor.SendOrEnqueueEditorRequest(objectPlacementMapAssetId =>
            {
                var request = new AdjustPainterObjectDensityMapRequest
                {
                    ObjectPlacementMapAssetId = objectPlacementMapAssetId,
                    LayerId = layerId,
                    ObjectDensityMapAdjustmentRegions = densityMapAdjustmentRegions,
                };
                return request;
            });
        }

        public override bool IsValidTargetEntityMesh(PaintTargetEntityMesh targetEntityMesh)
        {
            if (_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
               && terrainMapEditor.TryGetTerrainComponent(out var terrainComponent))
            {
                bool isValidTarget = terrainComponent.IsValidTargetEntityMesh(targetEntityMesh);
                return isValidTarget;
            }

            return false;
        }
    }
}
