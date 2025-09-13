using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;

public class TextureHeightmapLayerComponent : TerrainLayerComponentBase, ITerrainMapHeightmapLayer
{
    private TransformTRS _prevTransformData;

    private Array2d<float>? _layerHeightmapData;
    private Int2 _heightmapTexturePixelStartPosition;

    private bool _ignoreTextureChange;
    private bool _isHeightmapDataUpdateRequired;

    public override Type LayerDataType => typeof(TextureHeightmapLayerData);

    private Texture? _heightmapTexture;
    public Texture? HeightmapTexture
    {
        get => _heightmapTexture;
        set
        {
            if (IsInitialized)
            {
                var newTextureAttachedRef = AttachedReferenceManager.GetAttachedReference(value!);
                if (newTextureAttachedRef?.IsProxy == false)
                {
                    if (_ignoreTextureChange)
                    {
                        // Initial texture change was swapping the proxy object to the real object
                        _ignoreTextureChange = false;
                    }
                    else
                    {
                        _isHeightmapDataUpdateRequired = true;
                    }
                }
            }
            else
            {
                // Not initialized yet so this must be the asset being deserialized and assigned the original data
                _ignoreTextureChange = true;
            }
            _heightmapTexture = value;
        }
    }

    public TerrainHeightmapLayerBlendType LayerBlendType { get; set; } = TerrainHeightmapLayerBlendType.Maximum;

    protected override void OnInitialize()
    {
        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
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
        if (_isHeightmapDataUpdateRequired)
        {
            ////if (HeightmapTexture is not null)
            ////{
            ////    var heightmapTextureAttachedRef = AttachedReferenceManager.GetAttachedReference(HeightmapTexture);
            ////    if (heightmapTextureAttachedRef?.IsProxy == false && _layerData is not null)
            ////    {
            ////        SetHeightmapData(null);     // No longer valid
            ////        RaiseLayerChangedEvent(LayerChangedType.Heightmap);
            ////        _isHeightmapDataUpdateRequired = false;
            ////    }
            ////    else
            ////    {
            ////        // Editor is still loading the texture, check again on the next update
            ////    }
            ////}
            ////else
            ////{
            ////    if (_layerData?.HeightmapData is not null)
            ////    {
            ////        SetHeightmapData(null);     // No longer valid
            ////        RaiseLayerChangedEvent(LayerChangedType.Heightmap);
            ////    }
            ////    _isHeightmapDataUpdateRequired = false;
            ////}
        }
    }

    internal void UpdateData(Array2d<float> layerHeightmapData, Int2? heightmapTexturePixelStartPosition)
    {
        _layerHeightmapData = layerHeightmapData;
        _heightmapTexturePixelStartPosition = heightmapTexturePixelStartPosition ?? _heightmapTexturePixelStartPosition;
    }
}
