using SceneEditorExtensionExample.Rendering.RenderTextures;
using SceneEditorExtensionExample.Rendering.RenderTextures.Requests;
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
using Stride.Rendering.Sprites;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Half = System.Half;

#if GAME_EDITOR
using Stride.Assets.Models;
using Stride.Core.Assets.Editor.ViewModel;
using System;
using System.IO;
#endif

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;

public class ModelHeightmapLayerComponent : TerrainLayerComponentBase
{
    private RenderTextureJobSystem _renderTextureJobSystem = default!;
    private IGraphicsDeviceService _graphicsDeviceService = default!;

    private TransformTRS _prevTransformData;

    private string? _pendingResultModelUrl;
    private Model? _pendingResultModel;
    private RenderTextureResult? _pendingResult = null;
    private Texture? _generatedTexture = null;

    // Heightmap values are in world-space.
    private ModelHeightmapLayerMetadata? _layerMetadata;

    private LayerBlendType _layerBlendType;
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

    private Entity? _debugEntity;
    private bool _showDebug;
    public bool ShowDebug
    {
        get => _showDebug;
        set
        {
            if (_showDebug != value)
            {
                _showDebug = value;
                ToggleDebugEntityDisplay(_showDebug);
            }
        }
    }

    private void ToggleDebugEntityDisplay(bool show)
    {
        if (!show)
        {
            if (_debugEntity is not null)
            {
                _debugEntity.Scene = null;
                _debugEntity = null;
            }
            return;
        }

        if (_debugEntity is null)
        {
            _debugEntity = new("DebugModelHeightmap");
            _debugEntity.Scene = Entity.Scene;
            _debugEntity.Transform.Rotation = Quaternion.LookRotation(Vector3.UnitY, -Vector3.UnitZ);
        }
        _debugEntity.Transform.Position = Entity.Transform.Position;
        _debugEntity.Transform.Position.Y += 6;     // Offset it above the actual model
        var spriteComp = _debugEntity.GetOrCreate<SpriteComponent>();
        if (spriteComp is not null)
        {
            spriteComp.SpriteType = SpriteType.Sprite;
            spriteComp.Sampler = SpriteSampler.PointClamp;
            if (spriteComp.SpriteProvider is not SpriteFromTexture)
            {
                spriteComp.SpriteProvider = new SpriteFromTexture();
            }
            if (spriteComp.SpriteProvider is SpriteFromTexture texSprite)
            {
                texSprite.Texture = _generatedTexture;
                texSprite.PixelsPerUnit = 2;
            }
        }
    }

    protected override void OnInitialize()
    {
        _renderTextureJobSystem = Services.GetSafeServiceAs<RenderTextureJobSystem>();
        _graphicsDeviceService = Services.GetSafeServiceAs<IGraphicsDeviceService>();

        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnRegisterLayerMetadata(TerrainMapAsset terrainMapAsset)
    {
        EnsureIdIsSet();
        var layerId = LayerId!.Value;
        if (terrainMapAsset.TryGetLayerMetadata<ModelHeightmapLayerMetadata>(layerId, out _layerMetadata))
        {
#if GAME_EDITOR
            // Check if the source file had been modified
            if (_layerMetadata.ModelAssetUrl is not null)
            {
                var modelAssetVm = StrideEditorService?.FindAssetViewModelByUrl<AssetViewModel<ModelAsset>>(_layerMetadata.ModelAssetUrl);
                if (modelAssetVm?.Asset is ModelAsset modelAsset)
                {
                    string modelFilePath = modelAsset.MainSource;
                    if (File.Exists(modelFilePath))
                    {
                        DateTime lastModifiedDateTime = File.GetLastWriteTimeUtc(modelFilePath);
                        // Note that deserialized datetime has DateTimeKind.Unknown, so must force this back to UTC for comparison
                        if (_layerMetadata.LastModifiedSourceFile?.ToUniversalTime() != lastModifiedDateTime)
                        {
                            _prevTransformData = EntityHelper.UnsetTransformTRS;    // Must regenerate
                        }
                    }
                }
            }
            // Note that procedural models do not have a source file so LastModifiedSourceFile is always null
#endif
        }
        else
        {
            _layerMetadata = new ModelHeightmapLayerMetadata
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
            if (!TryGetTerrainMap(out var terrainMap, out var terrainEntity))
            {
                return;
            }
            var modelComp = Entity.Get<ModelComponent>();
            if (modelComp is null)
            {
                Debug.WriteLine("ModelHeightmapLayerComponent: ModelComponent is missing on this entity.");
                return;
            }
            var model = modelComp.Model;
            if (model is null)
            {
                Debug.WriteLine("ModelHeightmapLayerComponent: ModelComponent.Model has not been assigned to this entity.");
                return;
            }
#if GAME_EDITOR
            var modelAttachedRef = AttachedReferenceManager.GetAttachedReference(model);
            if (modelAttachedRef?.IsProxy ?? false)
            {
                return;     // Editor still loading
            }
#endif
            _pendingResultModel = model;
            _pendingResultModelUrl = AttachedReferenceManager.GetUrl(model);

            var unitsPerPixel = terrainMap.MeshQuadSize;
            var heightmapHeightRange = terrainMap.HeightRange;
            var textureOriginWorldPosition = terrainEntity?.Transform.Position.XZ() ?? Vector2.Zero;

            var graphicsDevice = _graphicsDeviceService.GraphicsDevice;
            var request = new RenderHeightmapTextureRequest(
                model, curTransformData, textureOriginWorldPosition, unitsPerPixel);
            _pendingResult = _renderTextureJobSystem.EnqueueRequest(request);

            _prevTransformData = curTransformData;
        }

        // Check if texture has been generated yet
        if (_pendingResult is not null)
        {
            switch (_pendingResult.State)
            {
                case RenderTextureResultStateType.Success:
                    _generatedTexture = _pendingResult.Texture;
                    SetHeightmapData(heightmapData: null);  // Invalidate until OnUpdateHeightmap
                    if (_layerMetadata is not null)
                    {
                        _layerMetadata.HeightmapTexturePixelStartPosition = _pendingResult.TexturePixelStartPosition;
                    }
                    _pendingResult = null;
                    ToggleDebugEntityDisplay(_showDebug);
                    RaiseLayerChangedEvent();
                    break;
                case RenderTextureResultStateType.Failed:
                    Debug.WriteLine($"ModelHeightmapLayerComponent: Render Texture failed:\r\n{_pendingResult?.ErrorException}");
                    _pendingResult = null;
                    break;
            }
        }
    }
    
    private bool TryGetHeightmapData([NotNullWhen(true)] out Array2d<Half?>? heightmapData)
    {
        heightmapData = null;
        if (_layerMetadata?.HeightmapData is not null)
        {
            heightmapData = _layerMetadata.HeightmapData;
        }
        else if (_generatedTexture is not null)
        {
            var game = Services.GetSafeServiceAs<IGame>();
            var commandList = game.GraphicsContext.CommandList;
            using var heightmapImage = _generatedTexture.GetDataAsImage(commandList);
            heightmapData = HeightmapTextureHelper.ConvertToMaskableArray2dDataHalf(heightmapImage);
            SetHeightmapData(heightmapData);
            //heightmapData.PrintToDebug();
#if GAME_EDITOR
            // Track source file last modified date
            if (_pendingResultModel is not null && _pendingResultModelUrl is not null
                && _layerMetadata is not null)
            {
                _layerMetadata.ModelAssetUrl = _pendingResultModelUrl;
                var modelAssetVm = StrideEditorService?.FindAssetViewModelByUrl<AssetViewModel<ModelAsset>>(_pendingResultModelUrl);
                if (modelAssetVm?.Asset is ModelAsset modelAsset
                     && _layerMetadata is not null)
                {
                    string modelFilePath = modelAsset.MainSource;
                    if (File.Exists(modelFilePath))
                    {
                        var lastModifiedDateTime = File.GetLastWriteTimeUtc(modelFilePath);
                        _layerMetadata.LastModifiedSourceFile = lastModifiedDateTime;
                    }
                }
                // Note that procedural models do not have a source file so LastModifiedSourceFile is always null
            }
#endif
        }
        return heightmapData is not null;
    }

    private void SetHeightmapData(Array2d<Half?>? heightmapData)
    {
        if (_layerMetadata is not null)
        {
            _layerMetadata.HeightmapData = heightmapData;
            _layerMetadata.IsSerializationRequired = true;
        }
    }

    protected override void OnDeinitialize()
    {
        if (_debugEntity is not null)
        {
            _debugEntity.Scene = null;
            _debugEntity = null;
        }

        _generatedTexture?.Dispose();
        _generatedTexture = null;
    }

    protected override void OnUpdateHeightmap(Array2d<float> terrainMapHeightmapData, Vector2 heightRange)
    {
        if (!TryGetHeightmapData(out var localHeightmapData))
        {
            return;
        }
        if (_layerMetadata?.HeightmapTexturePixelStartPosition is not Int2 startingIndex)
        {
            return;
        }
        UpdateHeightmapRegion(
            localHeightmapData, startingIndex,
            terrainMapHeightmapData, heightRange,
            LayerBlendType,
            isLocalRegionDataNormalized: false);
    }
}
