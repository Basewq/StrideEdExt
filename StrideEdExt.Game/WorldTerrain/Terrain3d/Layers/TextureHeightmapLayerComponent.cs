using SceneEditorExtensionExample.Rendering.RenderTextures;
using SceneEditorExtensionExample.SharedData;
using SceneEditorExtensionExample.SharedData.Rendering;
using SceneEditorExtensionExample.StrideAssetExt.Assets;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;

#if GAME_EDITOR
using Stride.Assets.Textures;
using Stride.Core.Assets.Editor.ViewModel;
using System;
using System.IO;
#endif

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;

public class TextureHeightmapLayerComponent : TerrainLayerComponentBase
{
    private TransformTRS _prevTransformData;
    private TextureHeightmapLayerMetadata? _layerMetadata;
    private bool _ignoreTextureChange;
    private bool _isHeightmapDataUpdateRequired;

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

    private LayerBlendType _layerBlendType = LayerBlendType.Maximum;

    public LayerBlendType LayerBlendType
    {
        get => _layerBlendType;
        set
        {
            if (_layerBlendType != value)
            {
                _layerBlendType = value;
                if (IsInitialized)
                {
                    RaiseLayerChangedEvent();
                }
            }
        }
    }

    protected override void OnInitialize()
    {
        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnRegisterLayerMetadata(TerrainMapAsset terrainMapAsset)
    {
        EnsureIdIsSet();
        var layerId = LayerId!.Value;
        if (terrainMapAsset.TryGetLayerMetadata<TextureHeightmapLayerMetadata>(layerId, out _layerMetadata))
        {
#if GAME_EDITOR
            // Check if the source file had been modified
            if (_heightmapTexture is Texture heightmapTexture)
            {
                var texAssetVm = StrideEditorService?.FindAssetViewModel<AssetViewModel<TextureAsset>>(heightmapTexture);
                if (texAssetVm?.Asset is TextureAsset texAsset)
                {
                    string textureFilePath = texAsset.MainSource;
                    if (File.Exists(textureFilePath))
                    {
                        DateTime lastModifiedDateTime = File.GetLastWriteTimeUtc(textureFilePath);
                        // Note that deserialized datetime has DateTimeKind.Unknown, so must force this back to UTC for comparison
                        if (_layerMetadata.LastModifiedSourceFile?.ToUniversalTime() != lastModifiedDateTime)
                        {
                            _isHeightmapDataUpdateRequired = true;  // Must regenerate
                        }
                    }
                }
            }
#endif
        }
        else
        {
            _layerMetadata = new TextureHeightmapLayerMetadata
            {
                LayerId = layerId
            };
            terrainMapAsset.RegisterLayerMetadata(_layerMetadata);
        }
    }

    protected override void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
        var curTransformData = Entity.GetTransformTRS();
        if (!_prevTransformData.IsSame(curTransformData))
        {
            RaiseLayerChangedEvent();

            _prevTransformData = curTransformData;
        }
        if (_isHeightmapDataUpdateRequired)
        {
            if (HeightmapTexture is not null)
            {
                var heightmapTextureAttachedRef = AttachedReferenceManager.GetAttachedReference(HeightmapTexture);
                if (heightmapTextureAttachedRef?.IsProxy == false && _layerMetadata is not null)
                {
                    SetHeightmapData(null);     // No longer valid
                    RaiseLayerChangedEvent();
                    _isHeightmapDataUpdateRequired = false;
                }
                else
                {
                    // Editor is still loading the texture, check again on the next update
                }
            }
            else
            {
                if (_layerMetadata?.HeightmapData is not null)
                {
                    SetHeightmapData(null);     // No longer valid
                    RaiseLayerChangedEvent();
                }
                _isHeightmapDataUpdateRequired = false;
            }
        }
    }

    protected override void OnUpdateHeightmap(Array2d<float> terrainMapHeightmapData, Vector2 heightRange)
    {
        if (!TryGetTerrainMap(out var terrainMap, out var terrainEntity))
        {
            return;
        }
        if (!TryGetHeightmapData(out var localHeightmapData))
        {
            return;
        }

        var texWorldMeasurement = new TextureWorldMeasurement(terrainEntity.Transform.Position.XZ(), terrainMap.MeshQuadSize);
        var startingIndex = texWorldMeasurement.GetTextureCoordsXZ(Entity.Transform.Position);
        if (_layerMetadata is not null && _layerMetadata.HeightmapTexturePixelStartPosition != startingIndex)
        {
            _layerMetadata.HeightmapTexturePixelStartPosition = startingIndex;
            _layerMetadata.IsSerializationRequired = true;
        }
        const bool isHeightmapValueNormalized = true;
        UpdateHeightmapRegion(
            localHeightmapData, startingIndex,
            terrainMapHeightmapData, heightRange,
            LayerBlendType, isHeightmapValueNormalized);
    }

    private bool TryGetHeightmapData([NotNullWhen(true)] out Array2d<float>? heightmapData)
    {
        heightmapData = null;
        if (_layerMetadata?.HeightmapData is not null)
        {
            heightmapData = _layerMetadata.HeightmapData;
        }
        else if (HeightmapTexture is Texture heightmapTexture)
        {
            var game = Services.GetSafeServiceAs<IGame>();
            var commandList = game.GraphicsContext.CommandList;
            using var heightmapImage = heightmapTexture.GetDataAsImage(commandList);
            heightmapData = HeightmapTextureHelper.ConvertToArray2dDataFloat(heightmapImage);
            SetHeightmapData(heightmapData);
#if GAME_EDITOR
            // Track source file last modified date
            var texAssetVm = StrideEditorService?.FindAssetViewModel<AssetViewModel<TextureAsset>>(heightmapTexture);
            if (texAssetVm?.Asset is TextureAsset texAsset
                && _layerMetadata is not null)
            {
                string textureFilePath = texAsset.MainSource;
                if (File.Exists(textureFilePath))
                {
                    var lastModifiedDateTime = File.GetLastWriteTimeUtc(textureFilePath);
                    _layerMetadata.LastModifiedSourceFile = lastModifiedDateTime;
                }
            }
#endif
        }
        return heightmapData is not null;
    }

    private void SetHeightmapData(Array2d<float>? heightmapData)
    {
        if (_layerMetadata is not null)
        {
            _layerMetadata.HeightmapData = heightmapData;
            _layerMetadata.IsSerializationRequired = true;
        }
    }
}
