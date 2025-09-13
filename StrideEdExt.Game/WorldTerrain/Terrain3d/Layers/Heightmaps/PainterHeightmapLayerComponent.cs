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
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using System.Diagnostics;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;

public class PainterHeightmapLayerComponent : TerrainLayerComponentBase, ITerrainMapHeightmapLayer
{
    private IGraphicsDeviceService _graphicsDeviceService = default!;
    private IPainterService _painterService = default!;
    private HeightmapPainterTool _heightmapPainterTool;
    private PaintSessionKey? _paintSessionKey;

    private TransformTRS _prevTransformData;

    private Array2d<float>? _layerHeightmapData;
    private Int2 _heightmapTexturePixelStartPosition;

    public override Type LayerDataType => typeof(PainterHeightmapLayerData);

    public TerrainHeightmapLayerBlendType LayerBlendType { get; set; } = TerrainHeightmapLayerBlendType.Maximum;

    private PainterHeightmapBrushSettings? _prevBrushSettings;
    [Display(Expand = ExpandRule.Once)]
    public PainterHeightmapBrushSettings BrushSettings { get; set; } = new();

    public PainterHeightmapLayerComponent()
    {
        _heightmapPainterTool = new(this);
    }

    protected override void OnInitialize()
    {
        _graphicsDeviceService = Services.GetSafeServiceAs<IGraphicsDeviceService>();
        _painterService = Services.GetSafeServiceAs<IPainterService>();
        _heightmapPainterTool.Initialize(Services);

        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnDeinitialize()
    {
        DisposableExtensions.DisposeAndNull(ref _paintSessionKey);

        _heightmapPainterTool.Deinitialize();
    }

    protected override void OnActivateLayerEditMode()
    {
        if (Entity.TryFindComponentOnAncestor<TerrainMapEditorComponent>(out var editorComp))
        {
            var editorEntityId = editorComp.Entity.Id;
            var paintSessionId = _painterService.BeginSession(editorEntityId);
            _paintSessionKey = new PaintSessionKey(_painterService, paintSessionId);
            UpdateBrushSettingsChanges();
            _painterService.SetActiveTool(paintSessionId, _heightmapPainterTool);
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
                var request = new UpdateHeightmapTextureStartPositionRequest
                {
                    TerrainMapAssetId = terrainMapAssetId,
                    LayerId = LayerId,
                    HeightmapTexturePixelStartPosition = startingIndex
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
            BrushSettings.CopyTo(_heightmapPainterTool.BrushSettings);
            _prevBrushSettings = BrushSettings;
            BrushSettings.HasChanged = false;
        }
    }

    internal void UpdateData(Array2d<float> layerHeightmapData, Int2? heightmapTexturePixelStartPosition)
    {
        _layerHeightmapData = layerHeightmapData;
        _heightmapTexturePixelStartPosition = heightmapTexturePixelStartPosition ?? _heightmapTexturePixelStartPosition;
    }

    private class HeightmapPainterTool : PainterToolBase
    {
        private readonly PainterHeightmapLayerComponent _parent;

        public HeightmapPainterTool(PainterHeightmapLayerComponent parent)
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
            if (_parent.TryGetTerrainMapEditor(out var terrainMapEditor)
                && terrainMapEditor.TryGetTerrainComponent(out var terrainComponent))
            {
                terrainComponent.SetEditPreviewType(TerrainMapEditPreviewType.Heightmap);
                terrainComponent.SetMaxAdjustmentHeightValue(_parent.BrushSettings.TerrainHeight);
                terrainComponent.SetHeightmapPaintModeType(_parent.BrushSettings.PaintModeType);
                terrainMapEditor.PrepareVisiblePaintableHeightmapMeshTargetAndRenderTargetMap(targetEntityMeshAndRenderTargetMapOutput);
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
                || !terrainMapEditor.TryGetTerrainComponent(out var terrainComponent)
                || _parent._layerHeightmapData is not Array2d<float> layerHeightmapData
                || terrainMapEditor.TerrainMap is not TerrainMap terrainMap)
            {
                return;
            }
            var paintableTerrainMeshResultMap = new Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshResultData>();
            terrainMapEditor.CompletePaintableMeshTargetAndRenderTargetMap(paintableTerrainMeshResultMap);

            var IndexOffsetEast = new Int2(1, 0);
            var IndexOffsetSouth = new Int2(0, 1);

            var paintModeType = _parent.BrushSettings.PaintModeType;
            var heightmapAdjustmentRegions = new List<HeightmapAdjustmentRegionRequest>();
            foreach (var (targetEntityMesh, meshData) in paintableTerrainMeshResultMap)
            {
                if (!terrainMap.TryGetChunk(meshData.ChunkIndex, out var chunk))
                {
                    // Chunk disappeared?
                    continue;
                }

                var heightmapTextureRegion = meshData.HeightmapTextureRegion;
                var startPosition = new Int2(heightmapTextureRegion.X, heightmapTextureRegion.Y);
                var strokeMapData = meshData.StrokeMapData;
                bool hasNonZeroValue = strokeMapData.Contains((in float x) => x != 0);
                if (!hasNonZeroValue)
                {
                    continue;
                }
                Array2d<float> adjustmentHeightmapData;
                switch (paintModeType)
                {
                    case HeightmapPaintModeType.Raise:
                        adjustmentHeightmapData = CalculateRaiseHeightAdjustment(layerHeightmapData, strokeMapData, startPosition, terrainMap);
                        break;
                    case HeightmapPaintModeType.Lower:
                        adjustmentHeightmapData = CalculateLowerHeightAdjustment(layerHeightmapData, strokeMapData, startPosition, terrainMap);
                        break;
                    case HeightmapPaintModeType.Smooth:
                        adjustmentHeightmapData = CalculateSmoothHeightAdjustment(layerHeightmapData, strokeMapData, startPosition, terrainMap, terrainComponent);
                        break;
                    case HeightmapPaintModeType.Flatten:
                        adjustmentHeightmapData = CalculateFlattenHeightAdjustment(layerHeightmapData, strokeMapData, startPosition, strokeMapBrushPoints, terrainMap, terrainComponent);
                        break;
                    default:
                        Debug.WriteLine($"Warning: Unhandled paint mode type '{paintModeType}'");
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
                if (adjustmentHeightmapData.Length2d != writableAdjustmentRegionSize)
                {
                    adjustmentHeightmapData.Resize(writableAdjustmentRegionSize);
                    hasNonZeroValue = adjustmentHeightmapData.Contains((in float x) => x != 0);
                    if (!hasNonZeroValue)
                    {
                        continue;
                    }
                }

                var adjustRegionReq = new HeightmapAdjustmentRegionRequest
                {
                    AdjustmentHeightmapData = adjustmentHeightmapData,
                    StartPosition = startPosition
                };
                heightmapAdjustmentRegions.Add(adjustRegionReq);
            }

            if (heightmapAdjustmentRegions.Count > 0)
            {
                var layerId = _parent.LayerId;
                terrainMapEditor.CommitPaintableHeightmapMeshTargetAndRenderTargetChanges(layerId, heightmapAdjustmentRegions);
            }
        }

        private Array2d<float> CalculateRaiseHeightAdjustment(
            Array2d<float> layerHeightmapData,
            Array2d<float> strokeMapData, Int2 strokeMapStartPosition,
            TerrainMap terrainMap)
        {
            var brushSettings = _parent.BrushSettings;
            var heightRangeVec2 = terrainMap.HeightRange;
            float heightRange = heightRangeVec2.Y - heightRangeVec2.X;
            var adjustmentHeightmapData = new Array2d<float>(strokeMapData.Length2d);
            for (int y = 0; y < strokeMapData.LengthY; y++)
            {
                for (int x = 0; x < strokeMapData.LengthX; x++)
                {
                    float brushStrokeAmount = strokeMapData[x, y];
                    float adjustmentValue = brushStrokeAmount * brushSettings.TerrainHeight;
                    adjustmentValue /= heightRange;
                    adjustmentHeightmapData[x, y] = adjustmentValue;
                }
            }
            return adjustmentHeightmapData;
        }

        private Array2d<float> CalculateLowerHeightAdjustment(
            Array2d<float> layerHeightmapData,
            Array2d<float> strokeMapData, Int2 strokeMapStartPosition,
            TerrainMap terrainMap)
        {
            var brushSettings = _parent.BrushSettings;
            var heightRangeVec2 = terrainMap.HeightRange;
            float heightRange = heightRangeVec2.Y - heightRangeVec2.X;
            var adjustmentHeightmapData = new Array2d<float>(strokeMapData.Length2d);
            for (int y = 0; y < strokeMapData.LengthY; y++)
            {
                for (int x = 0; x < strokeMapData.LengthX; x++)
                {
                    float brushStrokeAmount = -strokeMapData[x, y];
                    float adjustmentValue = brushStrokeAmount * brushSettings.TerrainHeight;
                    adjustmentValue /= heightRange;
                    adjustmentHeightmapData[x, y] = adjustmentValue;
                }
            }
            return adjustmentHeightmapData;
        }

        private static float IntercardinalInfluence = 1f / MathF.Sqrt(2);   // Inverse of diagonal distance, ie. 1 / sqrt(1^2 + 1^2)
        private Array2d<float> CalculateSmoothHeightAdjustment(
            Array2d<float> layerHeightmapData,
            Array2d<float> strokeMapData, Int2 strokeMapStartPosition,
            TerrainMap terrainMap, TerrainComponent terrainComponent)
        {
            var terrainHeightmapData = terrainComponent.OverrideHeightmapData ?? terrainMap?.HeightmapData;
            if (terrainHeightmapData is null)
            {
                terrainHeightmapData = new(layerHeightmapData.Length2d);    // Should error instead?
            }
            var brushSettings = _parent.BrushSettings;
            Span<Int2> cardinalIndexOffsets = [
                new(+0, -1),    // N
                new(+1, +0),    // E
                new(+0, +1),    // S
                new(-1, +0),    // W
            ];
            Span<Int2> intercardinalIndexOffsets = [
                new(-1, -1),    // NW
                new(+1, -1),    // NE
                new(+1, +1),    // SE
                new(-1, +1),    // SW
            ];
            var adjustmentHeightmapData = new Array2d<float>(strokeMapData.Length2d);
            for (int y = 0; y < strokeMapData.LengthY; y++)
            {
                for (int x = 0; x < strokeMapData.LengthX; x++)
                {
                    float brushStrokeAmount = strokeMapData[x, y];
                    brushStrokeAmount = MathUtil.Clamp(brushStrokeAmount, 0, 1);
                    if (brushStrokeAmount <= 0)
                    {
                        continue;
                    }

                    var heightmapBaseIndex = new Int2(x, y) + strokeMapStartPosition;
                    float currentHeightmapValue = terrainHeightmapData[heightmapBaseIndex];

                    float summedDiffValue = 0;
                    int sampledValueCount = 0;

                    foreach (var idxOffset in cardinalIndexOffsets)
                    {
                        float sampledHeightmapValue = SampleHeightmapValue(terrainHeightmapData, heightmapBaseIndex, idxOffset, ref sampledValueCount);
                        float diffValue = sampledHeightmapValue - currentHeightmapValue;
                        summedDiffValue += diffValue;
                    }
                    foreach (var idxOffset in intercardinalIndexOffsets)
                    {
                        float sampledHeightmapValue = SampleHeightmapValue(terrainHeightmapData, heightmapBaseIndex, idxOffset, ref sampledValueCount);
                        float diffValue = sampledHeightmapValue - currentHeightmapValue;
                        summedDiffValue += diffValue * IntercardinalInfluence;
                    }

                    float avgDiffValue = summedDiffValue / sampledValueCount;
                    float adjustmentValue = avgDiffValue * brushStrokeAmount;
                    adjustmentHeightmapData[x, y] = adjustmentValue;
                }
            }
            return adjustmentHeightmapData;

            static float SampleHeightmapValue(Array2d<float> terrainHeightmapData, Int2 baseIndex, Int2 indexOffset, ref int sampledValueCount)
            {
                var idx = baseIndex + indexOffset;
                // To be consistent with the shader preview, indices are clamped instead of ignored
                idx.X = MathUtil.Clamp(idx.X, min: 0, max: terrainHeightmapData.LengthX - 1);
                idx.Y = MathUtil.Clamp(idx.Y, min: 0, max: terrainHeightmapData.LengthY - 1);
                if (terrainHeightmapData.TryGetValue(idx, out var sampledHeightValue))
                {
                    sampledValueCount++;
                    return sampledHeightValue;
                }
                return 0;
            }
        }

        private Array2d<float> CalculateFlattenHeightAdjustment(
            Array2d<float> layerHeightmapData,
            Array2d<float> strokeMapData, Int2 strokeMapStartPosition,
            List<BrushPoint> strokeMapBrushPoints,
            TerrainMap terrainMap, TerrainComponent terrainComponent)
        {
            var terrainHeightmapData = terrainComponent.OverrideHeightmapData ?? terrainMap.HeightmapData;
            if (terrainHeightmapData is null)
            {
                terrainHeightmapData = new(layerHeightmapData.Length2d);    // Should error instead?
            }
            var brushSettings = _parent.BrushSettings;
            var heightRangeVec2 = terrainMap.HeightRange;
            float heightRange = heightRangeVec2.Y - heightRangeVec2.X;
            var adjustmentHeightmapData = new Array2d<float>(strokeMapData.Length2d);
            if (strokeMapBrushPoints.Count > 0)
            {
                var firstBrushPoint = strokeMapBrushPoints[0];
                float desiredHeightmapValue = MathUtil.InverseLerp(min: heightRangeVec2.X, max: heightRangeVec2.Y, firstBrushPoint.WorldPosition.Y);    // Get normalized height
                for (int y = 0; y < strokeMapData.LengthY; y++)
                {
                    for (int x = 0; x < strokeMapData.LengthX; x++)
                    {
                        var heightmapIndex = new Int2(x, y) + strokeMapStartPosition;
                        float currentHeightmapValue = terrainHeightmapData[heightmapIndex];
                        float brushStrokeAmount = strokeMapData[x, y];
                        brushStrokeAmount = MathUtil.Clamp(brushStrokeAmount, 0, 1);
                        float adjustmentValue = (desiredHeightmapValue - currentHeightmapValue) * brushStrokeAmount;
                        adjustmentHeightmapData[x, y] = adjustmentValue;
                    }
                }
            }
            return adjustmentHeightmapData;
        }
    }
}
