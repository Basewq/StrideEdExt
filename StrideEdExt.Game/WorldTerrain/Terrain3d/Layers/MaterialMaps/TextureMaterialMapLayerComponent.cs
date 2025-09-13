using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.MaterialMaps;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.MaterialMaps;

public class TextureMaterialWeightMapLayerComponent : TerrainLayerComponentBase, ITerrainMapMaterialWeightMapLayer
{
    private TransformTRS _prevTransformData;
    private Array2d<Half>? _layerMaterialWeightMapData { get; set; }
    private Int2 _layerMaterialWeightMapTexturePixelStartPosition;

    private bool _ignoreTextureChange;
    private bool _isMaterialWeightMapDataUpdateRequired;

    public override Type LayerDataType => typeof(TextureMaterialWeightMapLayerData);

    public string? MaterialName { get; set; }

    private Texture? _materialWeightMapTexture;
    public Texture? MaterialWeightMapTexture
    {
        get => _materialWeightMapTexture;
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
                        _isMaterialWeightMapDataUpdateRequired = true;
                    }
                }
            }
            else
            {
                // Not initialized yet so this must be the asset being deserialized and assigned the original data
                _ignoreTextureChange = true;
            }
            _materialWeightMapTexture = value;
        }
    }

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
        if (_isMaterialWeightMapDataUpdateRequired)
        {
            //if (MaterialWeightMapTexture is not null)
            //{
            //    var heightmapTextureAttachedRef = AttachedReferenceManager.GetAttachedReference(MaterialWeightMapTexture);
            //    if (heightmapTextureAttachedRef?.IsProxy == false && _layerData is not null)
            //    {
            //        SetMaterialWeightMapData(null);     // No longer valid
            //        RaiseLayerChangedEvent(LayerChangedType.MaterialWeightMap);
            //        _isMaterialWeightMapDataUpdateRequired = false;
            //    }
            //    else
            //    {
            //        // Editor is still loading the texture, check again on the next update
            //    }
            //}
            //else
            //{
            //    if (_layerData?.MaterialWeightMapData is not null)
            //    {
            //        SetMaterialWeightMapData(null);     // No longer valid
            //        RaiseLayerChangedEvent(LayerChangedType.MaterialWeightMap);
            //    }
            //    _isMaterialWeightMapDataUpdateRequired = false;
            //}
        }
    }

    internal void UpdateData(Array2d<Half> layerMaterialWeightMapData, Int2? layerMaterialWeightMapTexturePixelStartPosition)
    {
        _layerMaterialWeightMapData = layerMaterialWeightMapData;
        _layerMaterialWeightMapTexturePixelStartPosition = layerMaterialWeightMapTexturePixelStartPosition ?? _layerMaterialWeightMapTexturePixelStartPosition;
    }
}
