using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.DensityMaps;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement.Layers.DensityMaps;

public class TextureObjectDensityMapLayerComponent : ObjectDensityMapLayerComponentBase
{
    private TransformTRS _prevTransformData;
    private Array2d<Half>? _layerDensityMapData;
    private Int2 _layerDensityMapTexturePixelStartPosition;

    private bool _ignoreTextureChange;
    private bool _isDensityMapDataUpdateRequired;

    public override Type LayerDataType => typeof(TextureObjectDensityMapLayerData);

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

    private Texture? _densityMapTexture;
    public Texture? DensityMapTexture
    {
        get => _densityMapTexture;
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
                        _isDensityMapDataUpdateRequired = true;
                    }
                }
            }
            else
            {
                // Not initialized yet so this must be the asset being deserialized and assigned the original data
                _ignoreTextureChange = true;
            }
            _densityMapTexture = value;
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
            if (TryGetTerrainMap(out var terrainMap, out var terrainEntity))
            {
                // Texture is 1 px = 1 game unit - can change size in the world by Entity.Scale.XZ()
                var texWorldMeasurement = new TextureWorldMeasurement(terrainEntity.Transform.Position.XZ(), Vector2.One);
                _layerDensityMapTexturePixelStartPosition = texWorldMeasurement.GetTextureCoordsXZ(Entity.Transform.Position);

                _prevTransformData = curTransformData;

                _isDensityMapDataUpdateRequired = true;
            }
        }
        if (_isDensityMapDataUpdateRequired)
        {
            if (DensityMapTexture is not null)
            {
                var densityMapTextureAttachedRef = AttachedReferenceManager.GetAttachedReference(DensityMapTexture);
                if (densityMapTextureAttachedRef?.IsProxy == false)
                {
                    if (EditorComponent is not null)
                    {
                        var game = Services.GetSafeServiceAs<IGame>();
                        var commandList = game.GraphicsContext.CommandList;
                        using var densityMapImage = DensityMapTexture.GetDataAsImage(commandList);
                        var densityMapData = HeightmapTextureHelper.ConvertToArray2dDataHalf(densityMapImage);

                        EditorComponent.SendOrEnqueueEditorRequest(terrainMapAssetId =>
                        {
                            var request = new UpdateTextureObjectDensityMapRequest
                            {
                                ObjectPlacementMapAssetId = terrainMapAssetId,
                                LayerId = LayerId,
                                ObjectDensityMapTexturePixelStartPosition = _layerDensityMapTexturePixelStartPosition,
                                ObjectDensityMapTextureScale = Entity.Transform.Scale.XZ(),
                                ObjectDensityMapData = densityMapData,
                            };
                            return request;
                        });
                    }
                    _isDensityMapDataUpdateRequired = false;
                }
                else
                {
                    // Editor is still loading the texture, check again on the next update
                }
            }
            else
            {
                EditorComponent?.SendOrEnqueueEditorRequest(terrainMapAssetId =>
                {
                    var request = new UpdateTextureObjectDensityMapRequest
                    {
                        ObjectPlacementMapAssetId = terrainMapAssetId,
                        LayerId = LayerId,
                        ObjectDensityMapTexturePixelStartPosition = _layerDensityMapTexturePixelStartPosition,
                        ObjectDensityMapTextureScale = Entity.Transform.Scale.XZ(),
                        ObjectDensityMapData = null,
                    };
                    return request;
                });
                _isDensityMapDataUpdateRequired = false;
            }
        }
    }

    internal void UpdateData(Array2d<Half> layerDensityMapData, Int2? layerDensityMapTexturePixelStartPosition)
    {
        _layerDensityMapData = layerDensityMapData;
        _layerDensityMapTexturePixelStartPosition = layerDensityMapTexturePixelStartPosition ?? _layerDensityMapTexturePixelStartPosition;
    }
}
