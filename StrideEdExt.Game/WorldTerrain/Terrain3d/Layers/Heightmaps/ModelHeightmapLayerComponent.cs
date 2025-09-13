using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Sprites;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.Rendering.RenderTextures.Requests;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Rendering;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;
using StrideEdExt.StrideEditorExt;
using System.Diagnostics;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;

public class ModelHeightmapLayerComponent : TerrainLayerComponentBase, ITerrainMapHeightmapLayer
{
    private RenderTextureJobSystem _renderTextureJobSystem = default!;

    private TransformTRS _prevTransformData;

    private ModelComponent? _modelComponent;

    private string? _pendingResultModelUrl;
    private Model? _pendingResultModel;
    private Task<RenderTextureResult>? _pendingResultTask = null;
    private Texture? _generatedTexture = null;

    private Array2d<Half?>? _layerHeightmapData;
    private Int2 _heightmapTexturePixelStartPosition;

    public override Type LayerDataType => typeof(ModelHeightmapLayerData);

    public TerrainHeightmapLayerBlendType LayerBlendType { get; set; } = TerrainHeightmapLayerBlendType.Maximum;

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

        _prevTransformData = Entity.GetTransformTRS();
    }

    protected override void OnDeinitialize()
    {
        if (_debugEntity is not null)
        {
            _debugEntity.Scene = null;
            _debugEntity = null;
        }

        DisposableExtensions.DisposeAndNull(ref _generatedTexture);
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
            _modelComponent ??= Entity.Get<ModelComponent>();
            if (_modelComponent is null)
            {
                Debug.WriteLine("ModelHeightmapLayerComponent: ModelComponent is missing on this entity.");
                return;
            }
            var model = _modelComponent.Model;
            if (model is null)
            {
                Debug.WriteLine("ModelHeightmapLayerComponent: ModelComponent.Model has not been assigned to this entity.");
                return;
            }
            if (!EditorExtensions.IsRuntimeAssetLoaded(model))
            {
                return;
            }
            _pendingResultModel = model;
            _pendingResultModelUrl = AttachedReferenceManager.GetUrl(model);

            var unitsPerPixel = terrainMap.MeshQuadSize;
            var textureOriginWorldPosition = terrainEntity?.Transform.Position.XZ() ?? Vector2.Zero;

            var request = new RenderHeightmapTextureRequest(
                model, curTransformData, textureOriginWorldPosition, unitsPerPixel);
            _pendingResultTask = _renderTextureJobSystem.EnqueueRequest(request);

            _prevTransformData = curTransformData;
        }

        // Check if texture has been generated yet
        if (_pendingResultTask?.IsCompleted == true)
        {
            if (_pendingResultTask.IsCompletedSuccessfully)
            {
                var pendingResult = _pendingResultTask.Result;
                ProcessRenderTextureResult(pendingResult);
            }
            else
            {
                Debug.WriteLine($"ModelHeightmapLayerComponent: Render Texture task failed:\r\n{_pendingResultTask.Exception}");
            }
            _pendingResultTask = null;
        }
    }

    private void ProcessRenderTextureResult(RenderTextureResult renderTextureResult)
    {
        switch (renderTextureResult.State)
        {
            case RenderTextureResultStateType.Success:
                {
                    _generatedTexture = renderTextureResult.Texture ?? throw new NullReferenceException("Generated Texture was not set.");
                    var game = Services.GetSafeServiceAs<IGame>();
                    var commandList = game.GraphicsContext.CommandList;
                    using var heightmapImage = _generatedTexture.GetDataAsImage(commandList);
                    var heightmapTexturePixelStartPosition = renderTextureResult.TexturePixelStartPosition;
                    var heightmapData = HeightmapTextureHelper.ConvertToMaskableArray2dDataHalf(heightmapImage);
                    Debug.WriteLineIf(condition: true, $"UpdateModelHeightmapRequest: {renderTextureResult.TexturePixelStartPosition}");
                    EditorComponent?.SendOrEnqueueEditorRequest(terrainMapAssetId =>
                    {
                        var request = new UpdateModelHeightmapRequest
                        {
                            TerrainMapAssetId = terrainMapAssetId,
                            LayerId = LayerId,
                            HeightmapTexturePixelStartPosition = heightmapTexturePixelStartPosition,
                            HeightmapData = heightmapData,
                        };
                        return request;
                    });
                    ToggleDebugEntityDisplay(_showDebug);
                }
                break;
            case RenderTextureResultStateType.Failed:
                Debug.WriteLine($"ModelHeightmapLayerComponent: Render Texture failed:\r\n{renderTextureResult?.ErrorException}");
                break;
        }
    }

    internal void UpdateData(Array2d<Half?> layerHeightmapData, Int2? heightmapTexturePixelStartPosition)
    {
        _layerHeightmapData = layerHeightmapData;
        _heightmapTexturePixelStartPosition = heightmapTexturePixelStartPosition ?? _heightmapTexturePixelStartPosition;
    }
}
