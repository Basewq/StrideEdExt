using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using Stride.Physics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using StrideEdExt.Painting;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.Materials;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.WorldTerrain.Terrain3d.Editor;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.MaterialWeightMaps;
using StrideEdExt.WorldTerrain.TerrainMesh;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Buffer = Stride.Graphics.Buffer;
using Half = System.Half;

namespace StrideEdExt.WorldTerrain.Terrain3d;

/// <summary>
/// Terrain Manager for a given <see cref="TerrainMap"/> asset, which manages
/// the rendering instancing dividing it into chunks.
/// </summary>
[ComponentCategory("Terrain")]
[DataContract]
[DefaultEntityComponentProcessor(typeof(TerrainProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
#if GAME_EDITOR
//[DefaultEntityComponentProcessor(typeof(TerrainMapDataBuilderProcessor), ExecutionMode = ExecutionMode.Editor)]
#endif
public class TerrainComponent : EntityComponent
{
    public static readonly CollisionFilterGroups TerrainColliderGroup = CollisionFilterGroups.StaticFilter;
    public const string TerrainColliderTag = "Terrain";

    private GraphicsContext _graphicsContext = default!;
    private GraphicsDevice _graphicsDevice = default!;
    private ContentManager _contentManager = default!;

    private readonly List<Model> _pendingDisposeModels = [];
    private readonly List<IDisposable> _pendingDisposables = [];

    // The visible chunks
    private Dictionary<TerrainChunkModelId, ModelComponent> _chunkModelIdToActiveModelComponent = [];
    private Dictionary<TerrainChunkModelId, ModelComponent> _chunkModelIdToActiveModelComponentProcessing = [];
    private Dictionary<TerrainChunkModelId, PaintRenderTargetTexture> _chunkModelIdToRenderTarget = [];

    private Entity? _staticPhysicsColliderEntity;

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                foreach (var (_, modelComp) in _chunkModelIdToActiveModelComponent)
                {
                    modelComp.Entity.Scene = null;
                }
            }
        }
    }

    [DataMemberIgnore]
    internal bool IsEditorEnabled { get; set; }
    /// <summary>
    /// Height values in [0...1] range.
    /// </summary>
    [DataMemberIgnore]
    internal Array2d<float>? OverrideHeightmapData { get; private set; }
#if GAME_EDITOR
    private Texture? _overrideHeightmapTexture;
    private Texture? _nextOverrideHeightmapTexture;
#endif

    public TerrainMap? TerrainMap { get; set; }

    /// <summary>
    /// The camera used to determine which chunks should be visible.
    /// </summary>
    public CameraComponent? CameraComponent { get; set; }
    public float MaxChunkRenderDistance { get; set; } = 100;

    internal void Initialize(IServiceRegistry serviceRegistry)
    {
        var game = serviceRegistry.GetSafeServiceAs<IGame>();
        _graphicsContext = game.GraphicsContext;
        _graphicsDevice = game.GraphicsDevice;
        _contentManager = game.Content;

        if (Entity.EntityManager.ExecutionMode == ExecutionMode.Runtime && TerrainMap is not null)
        {
            // Only load the asset as-is when running as an app, the editor loads the meshes via TerrainMapEditorProcessor
            TerrainMap.Initialize();
            BuildPhysicsColliders();
        }
    }

    internal void Deinitialize()
    {
        _graphicsContext = null!;
        _graphicsDevice = null!;
        _contentManager = null!;

        foreach (var (_, modelComp) in _chunkModelIdToActiveModelComponent)
        {
            modelComp.Entity.Scene = null;
        }
        _chunkModelIdToActiveModelComponent.Clear();
        _chunkModelIdToActiveModelComponentProcessing.Clear();
        foreach (var (_, renderTarget) in _chunkModelIdToRenderTarget)
        {
            renderTarget.Texture.Dispose();
        }
        _chunkModelIdToRenderTarget.Clear();

        if (_staticPhysicsColliderEntity is not null)
        {
            _staticPhysicsColliderEntity.Scene = null;
            _staticPhysicsColliderEntity = null;
        }
#if GAME_EDITOR
        DisposableExtensions.DisposeAndNull(ref _nextOverrideHeightmapTexture);
        DisposableExtensions.DisposeAndNull(ref _overrideHeightmapTexture);
        if (_cachedEditPreviewDiffuseMapSettings is not null)
        {
            DisposableExtensions.DisposeAndNull(ref _cachedEditPreviewDiffuseMapSettings.OverrideMaterialIndexMapTexture);
            DisposableExtensions.DisposeAndNull(ref _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture);
            DisposableExtensions.DisposeAndNull(ref _cachedEditPreviewDiffuseMapSettings.EditLayerMaterialWeightMapTexture);
        }
#endif
        DisposeDisposables();
    }

    private void DisposeDisposables()
    {
        if (_pendingDisposeModels.Count > 0)
        {
            foreach (var model in _pendingDisposeModels)
            {
                ModelHelper.DisposeModel(model);
            }
            _pendingDisposeModels.Clear();
        }
        if (_pendingDisposables.Count > 0)
        {
            foreach (var disp in _pendingDisposables)
            {
                disp.Dispose();
            }
            _pendingDisposables.Clear();
        }
    }

    internal void Update(GameTime time)
    {
        DisposeDisposables();
    }

    private static readonly Vector3[] FrustumPointsClipSpace = [
        // Far plane
        new Vector3(-1, +1, 1),
        new Vector3(+1, +1, 1),
        new Vector3(-1, -1, 1),
        new Vector3(+1, -1, 1),
        // Near plane
        new Vector3(-1, +1, 0),
        new Vector3(+1, +1, 0),
        new Vector3(-1, -1, 0),
        new Vector3(+1, -1, 0),
    ];
    private readonly List<TerrainChunkIndex2d> _visibleChunkIndexList = [];
    private readonly Vector3[] _frustumPointsWorldSpace = new Vector3[8];
    internal void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
#if GAME_EDITOR
        if (!IsEditorEnabled)
        {
            return;
        }
#endif
        if (TerrainMap is null || !TerrainMap.IsInitialized)
        {
            return;
        }
        // Find the visible chunks
        _visibleChunkIndexList.Clear();
        var camComp = overrideCameraComponent ?? CameraComponent;
        if (camComp is null)
        {
            return;
        }
        // TODO Find a way to get all visible chunk indices based off frustum

        // Assume we're always using Perspective camera
        float fovRadians = MathUtil.DegreesToRadians(camComp.VerticalFieldOfView);
        float aspectRatio = camComp.AspectRatio;
        float zNear = camComp.NearClipPlane;
        float zFar = MaxChunkRenderDistance;   // We use our own instead of camComp.FarClipPlane (which should be smaller)

        Matrix.PerspectiveFovRH(fovRadians, aspectRatio, zNear, zFar, out var projMatrix);

        Matrix.Multiply(in camComp.ViewMatrix, in projMatrix, out var viewProjMatrix);
        var frustum = new BoundingFrustum(in viewProjMatrix);

        // Determine the AABB bounds of the frustum based off the corners of the frustum
        Matrix.Invert(in viewProjMatrix, out var viewProjInverseMatrix);
        for (int i = 0; i < FrustumPointsClipSpace.Length; i++)
        {
            var vec4 = Vector3.Transform(FrustumPointsClipSpace[i], viewProjInverseMatrix);
            _frustumPointsWorldSpace[i] = vec4.XYZ() / vec4.W;
        }
        BoundingBox.FromPoints(_frustumPointsWorldSpace, out var frustumBoundingBox);

        // Determine the chunks visible to the frustum
        var chunkIndexToPos = TerrainMap.ChunkWorldSizeVec2;
        var posToChunkIndex = 1f / chunkIndexToPos;
        var minIndex = MathExt.ToInt2Floor(frustumBoundingBox.Minimum.XZ() * posToChunkIndex);
        var maxIndex = MathExt.ToInt2Floor(frustumBoundingBox.Maximum.XZ() * posToChunkIndex);

        float minTerrainHeight = TerrainMap.HeightRange.X;
        float maxTerrainHeight = TerrainMap.HeightRange.Y;
        for (int x = minIndex.X; x <= maxIndex.X; x++)
        {
            for (int y = minIndex.Y; y <= maxIndex.Y; y++)
            {
                var chunkIndex = new TerrainChunkIndex2d(x, y);
                var minChunkBoundsPos = TerrainMap.ToChunkMinimumWorldPosition(chunkIndex, minTerrainHeight);
                var maxChunkBoundsPos = TerrainMap.ToChunkMinimumWorldPosition(chunkIndex + Int2.One, maxTerrainHeight);
                var chunkBoundingBox = new BoundingBoxExt(minChunkBoundsPos, maxChunkBoundsPos);
                if (frustum.Contains(in chunkBoundingBox))
                {
                    if (TerrainMap.TryGetChunk(chunkIndex, out _))
                    {
                        _visibleChunkIndexList.Add(chunkIndex);
                    }
                }
            }
        }

        // From the visible chunk index list, we go through _chunkIndexToModelDataList and find which chunks we need to render.
        // For performance reasons, we use try to reuse existing 'active' chunks where possible.

        // Swap the "actual" instancing dictionary with the "processing" dictionary, so that the "actual" instancing dictionary is
        // initially empty and the "processing" dictionary contains any chunks we can potentially reuse.
        Utilities.Swap(ref _chunkModelIdToActiveModelComponent, ref _chunkModelIdToActiveModelComponentProcessing);

        foreach (var visibleChunkIndex in _visibleChunkIndexList)
        {
            if (!TerrainMap.TryGetChunk(visibleChunkIndex, out var chunk))
            {
                continue;
            }

            int meshPerChunkSingleAxisLength = TerrainMap.MeshPerChunk.GetSingleAxisLength();
            var chunkMeshWorldSizeVec2 = TerrainMap.ChunkMeshWorldSizeVec2;
            for (int chunkSubCellY = 0; chunkSubCellY < meshPerChunkSingleAxisLength; chunkSubCellY++)
            {
                for (int chunkSubCellX = 0; chunkSubCellX < meshPerChunkSingleAxisLength; chunkSubCellX++)
                {
                    var chunkSubCellIndex = new TerrainChunkSubCellIndex2d(chunkSubCellX, chunkSubCellY);
                    var subChunk = chunk.GetSubChunk(chunkSubCellIndex);
                    bool hasChangedMesh = false;
                    var mesh = subChunk.Mesh;
                    if (mesh is null)
                    {
                        var heightmapData = OverrideHeightmapData ?? TerrainMap.HeightmapData;
                        if (heightmapData is null)
                        {
                            throw new InvalidOperationException("TerrainMap.HeightmapData is missing.");
                        }

                        // Sub-chunk can potentially be zero length if the chunk is with the range of the terrain map size but the sub-chunk isn't.
                        if (subChunk.HeightmapTextureRegion.Width > 0 && subChunk.HeightmapTextureRegion.Height > 0)
                        {
                            var terrainMeshData = TerrainMeshData.Generate(subChunk.HeightmapTextureRegion, TerrainMap.HeightmapTextureSize, heightmapData, TerrainMap.MeshQuadSize, TerrainMap.HeightRange);
                            var vertexBuffer = Buffer.Vertex.New(_graphicsDevice, terrainMeshData.Vertices, GraphicsResourceUsage.Default);
                            var indexBuffer = Buffer.Index.New(_graphicsDevice, terrainMeshData.VertexIndices, GraphicsResourceUsage.Default);
                            var minPos = new Vector3(0, TerrainMap.HeightRange.X, 0);
                            var maxPos = new Vector3(chunkMeshWorldSizeVec2.X, TerrainMap.HeightRange.Y, chunkMeshWorldSizeVec2.Y);
                            var boundingBox = new BoundingBox(minPos, maxPos);
                            mesh = new Mesh
                            {
                                Draw = new MeshDraw
                                {
                                    PrimitiveType = PrimitiveType.TriangleList,
                                    DrawCount = terrainMeshData.VertexIndices.Length,
                                    IndexBuffer = new IndexBufferBinding(indexBuffer, is32Bit: false, terrainMeshData.VertexIndices.Length),
                                    VertexBuffers = [new VertexBufferBinding(vertexBuffer, TerrainVertex.Layout, vertexBuffer.ElementCount)],
                                },
                                MaterialIndex = 0,
                                BoundingBox = boundingBox,
                                BoundingSphere = BoundingSphere.FromBox(boundingBox)
                            };
                        }
                        subChunk.Mesh = mesh;
                        hasChangedMesh = true;
                    }
                    var chunkModelId = new TerrainChunkModelId(visibleChunkIndex, chunkSubCellIndex);
                    if (_chunkModelIdToActiveModelComponentProcessing.Remove(chunkModelId, out var modelComponent))
                    {
                        if (hasChangedMesh)
                        {
                            if (modelComponent.Model is not null)
                            {
                                // Disposing immediately causes flickering, so only dispose on the next update
                                _pendingDisposeModels.Add(modelComponent.Model);
                            }

                            var model = new Model();
                            if (mesh is not null)
                            {
                                model.Add(mesh);
                                model.BoundingBox = mesh.BoundingBox;
                                model.BoundingSphere = mesh.BoundingSphere;
                            }
                            else
                            {
                                model.BoundingBox = BoundingBox.Empty;
                                model.BoundingSphere = BoundingSphere.Empty;
                            }
                            modelComponent.Model = model;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Terrain Map Chunk now visible: {chunkModelId}");
                        bool wasCreated = TryCreateModelComponent(chunkModelId, mesh, out modelComponent);
                    }
                    if (modelComponent is null)
                    {
                        continue;
                    }
                    _chunkModelIdToActiveModelComponent[chunkModelId] = modelComponent;
                    if (modelComponent.Entity.Scene is null)
                    {
                        modelComponent.Entity.SetParent(Entity);
                    }
                    float terrainEntityHeight = Entity.Transform.Position.Y;
                    var chunkMeshPos = TerrainMap.ToChunkSubCellMinimumWorldPosition(visibleChunkIndex, chunkSubCellIndex, terrainEntityHeight);
                    modelComponent.Entity.Transform.Position = chunkMeshPos;
                }
            }
        }
        UpdateMaterialTexturesChanges();

        // Any remaining chunks are no longer visible and should be removed
        foreach (var (chunkModelId, modelComponent) in _chunkModelIdToActiveModelComponentProcessing)
        {
            Debug.WriteLineIf(modelComponent.Entity.Scene is not null, $"Terrain Map Chunk no longer visible: {chunkModelId}");
            modelComponent.Entity.Scene = null;
        }
        _chunkModelIdToActiveModelComponentProcessing.Clear();
    }

    public void UpdateHeightmap(Size2 heightmapTextureSize, Array2d<float> heightmapData)
    {
        if (TerrainMap is null)
        {
            return;
        }
        // HACK: We can't overwrite TerrainMap.HeightmapData because Stride Editor
        // resets this to the asset's version during undo/redo, so we need the ability
        // to hold the 'in-progress' edited state.
        OverrideHeightmapData = heightmapData;
        bool hasChangedSize = TerrainMap.HeightmapTextureSize != heightmapTextureSize;
        TerrainMap.HeightmapTextureSize = heightmapTextureSize;
        if (hasChangedSize)
        {
            TerrainMap.RebuildChunks();
        }
        else
        {
            TerrainMap.InvalidateMeshes();
        }

#if GAME_EDITOR
        if (IsEditorEnabled && _cachedEditPreviewHeightmapFeature is not null)
        {
            float[] heightmapData1d = heightmapData.ToArray();
            var texture = Texture.New2D(
                _graphicsDevice,
                width: heightmapData.LengthX, height: heightmapData.LengthY,
                format: PixelFormat.R32_Float,
                heightmapData1d);

            _nextOverrideHeightmapTexture = texture;
        }
#endif
    }

    public void UpdateMaterialIndexMap(Array2d<byte> materialIndexMapData, Array2d<Half> materialWeightMapData)
    {
        if (TerrainMap is null)
        {
            return;
        }
        _nextMaterialIndexMapTexture = TerrainMaterial.CreateMaterialIndexMapTexture(materialIndexMapData, _graphicsDevice);
        _nextTerrainMaterialWeightMapTexture = TerrainMaterial.CreateMaterialWeightMapTexture(materialWeightMapData, _graphicsDevice);
    }

    internal void GetVisiblePaintableTerrainMeshMap(
        Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshData> paintableTerrainMeshMapOutput)
    {
        GetOrCreateVisiblePaintableTerrainMeshMap(
            paintableTerrainMeshMapOutput, PaintableTerrainEditType.GetOnly);
    }

    internal void GetOrCreateVisiblePaintableTerrainMeshMapForHeightmapEdit(
        Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshData> paintableTerrainMeshMapOutput)
    {
        GetOrCreateVisiblePaintableTerrainMeshMap(
            paintableTerrainMeshMapOutput, PaintableTerrainEditType.Heightmap);
    }

    internal void GetOrCreateVisiblePaintableTerrainMeshMapForMaterialEdit(
        Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshData> paintableTerrainMeshMapOutput,
        byte overrideMaterialIndex)
    {
        GetOrCreateVisiblePaintableTerrainMeshMap(
            paintableTerrainMeshMapOutput, PaintableTerrainEditType.MaterialIndexMap, overrideMaterialIndex);
    }

    private void GetOrCreateVisiblePaintableTerrainMeshMap(
        Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshData> paintableTerrainMeshMapOutput,
        PaintableTerrainEditType editType, byte overrideMaterialIndex = 0)
    {
        if (TerrainMap is null)
        {
            return;
        }

        var commandList = _graphicsContext.CommandList;
        foreach (var (chunkModelId, modelComp) in _chunkModelIdToActiveModelComponent)
        {
            if (TerrainMap.TryGetChunk(chunkModelId.ChunkIndex, out var chunk))
            {
                var chunkSubCellIndex = chunkModelId.ChunkSubCellIndex;
                if (!chunk.TryGetSubChunk(chunkSubCellIndex, out var subChunk))
                {
                    continue;
                }
                var entityId = modelComp.Entity.Id;
                var mesh = modelComp.Model.Meshes[0];
                var targetEntityMesh = new PaintTargetEntityMesh
                {
                    EntityId = entityId,
                    Mesh = mesh
                };
                var textureSize = subChunk.HeightmapTextureRegion.Size;
                if (!_chunkModelIdToRenderTarget.TryGetValue(chunkModelId, out var renderTarget)
                    || renderTarget.Texture.Width != textureSize.Width || renderTarget.Texture.Height != textureSize.Height)
                {
                    if (editType == PaintableTerrainEditType.GetOnly)
                    {
                        continue;
                    }
                    if (renderTarget is not null)
                    {
                        _pendingDisposables.Add(renderTarget.Texture);
                    }
                    var texture = PainterToolHelper.CreateNewRenderTarget(_graphicsDevice, textureSize);
                    renderTarget = new PaintRenderTargetTexture
                    {
                        Texture = texture,
                        IsNewTexture = true,
                    };
                    _chunkModelIdToRenderTarget[chunkModelId] = renderTarget;
                }
                // Assign to material (always re-assign because chunks may be recreated)
                if (modelComp.Materials.TryGetValue(key: 0, out var material))
                {
                    var activeMatParams = material.Passes[0].Parameters;

                    activeMatParams.Set(StrokeMapPaintingInputSharedKeys.StrokeMapTexture, renderTarget.Texture);
                    activeMatParams.Set(StrokeMapPaintingInputSharedKeys.IsPaintingActive, _isPaintingActive);
                    var strokeMapTextureSizeVec2 = new Vector2(textureSize.Width, textureSize.Height);
                    activeMatParams.Set(StrokeMapPaintingInputSharedKeys.StrokeMapTextureSize, strokeMapTextureSizeVec2);
                    switch (editType)
                    {
                        case PaintableTerrainEditType.Heightmap:
                            activeMatParams.Set(MaterialTerrainEditPreviewInputSharedKeys.EditPreviewType, (uint)TerrainMapEditPreviewType.Heightmap);

                            activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmap, _cachedEditPreviewHeightmapFeature?.TerrainHeightmap);
                            var heightmapSize = TerrainMap.HeightmapTextureSize.ToVector2();
                            if (_cachedEditPreviewHeightmapFeature?.TerrainHeightmap is Texture heightmapTexture)
                            {
                                heightmapSize = new(heightmapTexture.Width, heightmapTexture.Height);
                            }
                            activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmapSize, heightmapSize);
                            activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightRange, TerrainMap.HeightRange);
                            activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.MaxAdjustmentHeightValue, _cachedEditPreviewHeightmapFeature?.MaxAdjustmentHeightValue ?? default);
                            activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.HeightmapPaintModeType, (uint)(_cachedEditPreviewHeightmapFeature?.PaintModeType ?? default));
                            break;
                        case PaintableTerrainEditType.MaterialIndexMap:
                            activeMatParams.Set(MaterialTerrainEditPreviewInputSharedKeys.EditPreviewType, (uint)TerrainMapEditPreviewType.Material);

                            activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings?.TerrainMaterialWeightMapTexture);
                            var materialWeightMapSize = TerrainMap.HeightmapTextureSize.ToVector2();
                            if (_cachedEditPreviewDiffuseMapSettings?.TerrainMaterialWeightMapTexture is Texture terrainMaterialWeightMapTexture)
                            {
                                materialWeightMapSize = new(terrainMaterialWeightMapTexture.Width, terrainMaterialWeightMapTexture.Height);
                            }
                            activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMapSize, materialWeightMapSize);
                            activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.OverrideMaterialIndex, (uint)overrideMaterialIndex);
                            activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.MaterialMapPaintModeType, (uint)(_cachedEditPreviewDiffuseMapSettings?.PaintModeType ?? default));
                            activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.EditLayerMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings?.EditLayerMaterialWeightMapTexture);
                            break;
                    }
                }
                var meshData = new PaintableTerrainMeshData
                {
                    StrokeMapRenderTarget = renderTarget,
                    ChunkIndex = chunkModelId.ChunkIndex,
                    ChunkSubCellIndex = chunkSubCellIndex,
                    HeightmapTextureRegion = subChunk.HeightmapTextureRegion,
                };
                paintableTerrainMeshMapOutput[targetEntityMesh] = meshData;
            }
            else
            {
                Debug.WriteLine($"Warning: ChunkIndex was not found: {chunkModelId.ChunkIndex}");
            }
        }
    }

    internal void EndPaintableMeshTargets(Dictionary<PaintTargetEntityMesh, PaintableTerrainMeshResultData> paintableTerrainMeshResultMapOutput)
    {
        if (TerrainMap is null)
        {
            return;
        }

        SetIsPaintingActive(false);
        var commandList = _graphicsContext.CommandList;
        foreach (var (chunkModelId, renderTarget) in _chunkModelIdToRenderTarget)
        {
            if (_chunkModelIdToActiveModelComponent.TryGetValue(chunkModelId, out var modelComp)
                && TerrainMap.TryGetChunk(chunkModelId.ChunkIndex, out var chunk))
            {
                var chunkSubCellIndex = chunkModelId.ChunkSubCellIndex;
                if (!chunk.TryGetSubChunk(chunkSubCellIndex, out var subChunk))
                {
                    continue;
                }
                var entityId = modelComp.Entity.Id;
                var mesh = modelComp.Model.Meshes[0];
                var targetEntityMesh = new PaintTargetEntityMesh
                {
                    EntityId = entityId,
                    Mesh = mesh
                };
                var strokeMapData = PainterToolHelper.RenderTargetToArray2dData(commandList, renderTarget.Texture);
                var meshData = new PaintableTerrainMeshResultData
                {
                    StrokeMapData = strokeMapData,
                    ChunkIndex = chunkModelId.ChunkIndex,
                    ChunkSubCellIndex = chunkModelId.ChunkSubCellIndex,
                    HeightmapTextureRegion = subChunk.HeightmapTextureRegion,
                };
                paintableTerrainMeshResultMapOutput[targetEntityMesh] = meshData;

                if (modelComp.Materials.TryGetValue(key: 0, out var material))
                {
                    var activeMatParams = material.Passes[0].Parameters;

                    // Common material properties:
                    activeMatParams.Set(StrokeMapPaintingInputSharedKeys.StrokeMapTexture, null);
                    activeMatParams.Set(StrokeMapPaintingInputSharedKeys.IsPaintingActive, false);
                    activeMatParams.Set(MaterialTerrainEditPreviewInputSharedKeys.EditPreviewType, (uint)TerrainMapEditPreviewType.NotSet);
                    //PaintableTerrainEditType.Heightmap:

                    //PaintableTerrainEditType.MaterialIndexMap:
                    activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMap, null);
                    activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.OverrideMaterialIndex, 0u);
                    activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.EditLayerMaterialWeightMap, null);
                }
            }

            _pendingDisposables.Add(renderTarget.Texture);
        }
        _chunkModelIdToRenderTarget.Clear();
    }

    public void BuildPhysicsColliders()
    {
        if (TerrainMap is null)
        {
            throw new InvalidOperationException("TerrainMap is null.");
        }
        var heightmapData = OverrideHeightmapData ?? TerrainMap.HeightmapData;
        if (heightmapData is null)
        {
            throw new InvalidOperationException("TerrainMap.HeightmapData is missing.");
        }

        if (_staticPhysicsColliderEntity is null)
        {
            _staticPhysicsColliderEntity = new Entity("TerrainStaticPhysicsCollider");
        }

        var staticColliderComponent = _staticPhysicsColliderEntity.Get<StaticColliderComponent>();
        bool isNewCollider = staticColliderComponent is null;
        if (staticColliderComponent is null)
        {
            staticColliderComponent = new StaticColliderComponent
            {
                CollisionGroup = TerrainColliderGroup,
                Tag = TerrainColliderTag,
            };
        }

        // Free memory before building our collider(s)
        staticColliderComponent.ColliderShapes.Clear();
        staticColliderComponent.ColliderShape?.Dispose();

        var compoundColliderShape = new CompoundColliderShape();
        // This creates mesh colliders split on the terrain chunk size
        var chunks = TerrainMap.GetAllChunks();
        foreach (var chunk in chunks)
        {
            var heightmapTextureRegion = chunk.HeightmapTextureRegion;
            if (TerrainMeshData.GenerateMeshDataForPhysics(
                heightmapTextureRegion, heightmapData, TerrainMap.MeshQuadSize, TerrainMap.HeightRange,
                out var positions, out var vertexIndices))
            {
                var meshColliderShape = new StaticMeshColliderShape(positions, vertexIndices);
                compoundColliderShape.AddChildShape(meshColliderShape);

                // HACK: we also need to set 'ColliderShapeDesc' because of the following cases:
                // 1. The editor loads colliders via 'ColliderShapeDesc' instead of ColliderShapes
                // 2. If NavigationBoundingBoxComponent exists & dynamic navigation mesh is enabled, this will be used. (note that static nav mesh crashes the app, only dynamic mesh works...)
                var meshColliderShapeDesc = new EditorMeshColliderShapeDesc
                {
                    VertexPositions = positions,
                    VertexIndices = vertexIndices
                };
                staticColliderComponent.ColliderShapes.Add(meshColliderShapeDesc);
            }
            else
            {
                Debug.WriteLine("Failed to generate terrain collider.");
            }
        }
        staticColliderComponent.ColliderShape = compoundColliderShape;

        if (isNewCollider)
        {
            // For new colliders, we must set the ColliderShape BEFORE attaching the component, otherwise the collider does not get registered
            _staticPhysicsColliderEntity.Add(staticColliderComponent);
        }

        if (_staticPhysicsColliderEntity.Scene is null)
        {
            //_staticPhysicsColliderEntity.SetParent(Entity);
            Entity.Scene.Entities.Add(_staticPhysicsColliderEntity);
        }
    }

    private bool TryCreateModelComponent(TerrainChunkModelId chunkModelId, Mesh? mesh, [NotNullWhen(true)] out ModelComponent? modelComponent)
    {
        var modelChunkEntity = new Entity($"Tile Map: {chunkModelId}");
        var model = new Model();
        if (mesh is not null)
        {
            model.Add(mesh);
            model.BoundingBox = mesh.BoundingBox;
            model.BoundingSphere = mesh.BoundingSphere;
        }
        else
        {
            model.BoundingBox = BoundingBox.Empty;
            model.BoundingSphere = BoundingSphere.Empty;
        }
        modelComponent = new ModelComponent
        {
            Model = model,
            IsShadowCaster = true,
        };
        // Because we divide instancing into chunks we need to clone the material
        // so each model chunk have separate instancing data.
        var terrainMaterial = GetOrCreateMaterial();
        // Make a clone of the material
        var newMaterial = terrainMaterial.CloneMaterial();
        modelComponent.Materials.Add(key: 0, newMaterial);

        modelChunkEntity.Add(modelComponent);
        return true;
    }

    private Material? _cachedFallbackMaterial;
    private Material? _cachedTerrainMaterial;
    private MaterialTerrainDiffuseMapFeature? _cachedDiffuseMapFeature;
    private MaterialTerrainEditPreviewDiffuseMapSettings? _cachedEditPreviewDiffuseMapSettings;
    private MaterialTerrainEditPreviewHeightmapFeature? _cachedEditPreviewHeightmapFeature;
    private bool _terrainEditPreviewHeightmapFeatureHasPendingChanges = false;
    private Texture? _nextMaterialIndexMapTexture;
    private Texture? _nextTerrainMaterialWeightMapTexture;
    private Texture? _nextEditLayerMaterialWeightMapTexture;
    private TerrainMapEditPreviewType? _nextEditPreviewType;
    private Vector3 _initialBrushWorldPosition;
    private Vector3? _nextInitialBrushWorldPosition;
    private bool _isPaintingActive = false;
    public Material GetOrCreateMaterial()
    {
        if (_cachedTerrainMaterial is not null)
        {
            return _cachedTerrainMaterial;
        }
        var terrainMaterial = TerrainMap?.TerrainMaterial;
        if (terrainMaterial is null)
        {
            _cachedFallbackMaterial ??= Material.New(_graphicsDevice, new MaterialDescriptor
            {
                Attributes =
                {
                    Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(Color.Magenta)),
                    DiffuseModel = new MaterialDiffuseLambertModelFeature()
                }
            });
            return _cachedFallbackMaterial;
        }

        _cachedDiffuseMapFeature = new MaterialTerrainDiffuseMapFeature
        {
            MaterialIndexMap = TerrainMap?.MaterialIndexMapTexture,
            MaterialIndexMapSize = TerrainMap?.MaterialIndexMapSize ?? Vector2.Zero,
            DiffuseMapTextureArray = terrainMaterial.DiffuseMapTextureArray,
            NormalMapTextureArray = terrainMaterial.NormalMapTextureArray,
            HeightBlendMapTextureArray = terrainMaterial.HeightBlendMapTextureArray,
#if GAME_EDITOR
            IsEditable = true,
#endif
        };
#if GAME_EDITOR
        _cachedEditPreviewDiffuseMapSettings = new();
        _cachedEditPreviewHeightmapFeature = new MaterialTerrainEditPreviewHeightmapFeature
        {
            // Properties actually set direct in the material via GetOrCreateVisiblePaintableTerrainMeshMap
        };
#endif
        var material = Material.New(_graphicsDevice, new MaterialDescriptor
        {
            Attributes =
            {
                //Surface = _cachedNormalMapFeature,
                Displacement = _cachedEditPreviewHeightmapFeature,
                Diffuse = _cachedDiffuseMapFeature,
                DiffuseModel = new MaterialDiffuseLambertModelFeature()
            }
        });

        _cachedTerrainMaterial = material;
        return material;
    }

    internal void SetEditPreviewType(TerrainMapEditPreviewType editPreviewType)
    {
        _nextEditPreviewType = editPreviewType;
    }

    internal void SetIsPaintingActive(bool isPaintingActive)
    {
        _isPaintingActive = isPaintingActive;
    }

    internal void SetInitialBrushWorldPosition(Vector3 brushWorldPosition)
    {
        _nextInitialBrushWorldPosition = brushWorldPosition;
    }

    internal void SetMaxAdjustmentHeightValue(float maxAdjustmentHeightValue)
    {
        if (_cachedEditPreviewHeightmapFeature is not null)
        {
            _terrainEditPreviewHeightmapFeatureHasPendingChanges = _terrainEditPreviewHeightmapFeatureHasPendingChanges
                || _cachedEditPreviewHeightmapFeature.MaxAdjustmentHeightValue != maxAdjustmentHeightValue;
            _cachedEditPreviewHeightmapFeature.MaxAdjustmentHeightValue = maxAdjustmentHeightValue;
        }
    }

    internal void SetHeightmapPaintModeType(HeightmapPaintModeType heightmapPaintModeType)
    {
        if (_cachedEditPreviewHeightmapFeature is not null)
        {
            _terrainEditPreviewHeightmapFeatureHasPendingChanges = _terrainEditPreviewHeightmapFeatureHasPendingChanges
                || _cachedEditPreviewHeightmapFeature.PaintModeType != heightmapPaintModeType;
            _cachedEditPreviewHeightmapFeature.PaintModeType = heightmapPaintModeType;
        }
    }

    internal void SetMaterialMapPaintModeType(MaterialMapPaintModeType materialMapPaintModeType)
    {
        if (_cachedEditPreviewDiffuseMapSettings is not null)
        {
            _cachedEditPreviewDiffuseMapSettings.PaintModeType = materialMapPaintModeType;
        }
    }

    internal void SetEditLayerMaterialWeightMap(Array2d<Half> layerMaterialWeightMapData)
    {
        _nextEditLayerMaterialWeightMapTexture = TerrainMaterial.CreateMaterialWeightMapTexture(layerMaterialWeightMapData, _graphicsDevice);
    }

    [Conditional("GAME_EDITOR")]
    private void UpdateMaterialTexturesChanges()
    {
        if (TerrainMap is null || _cachedTerrainMaterial is null)
        {
            return;
        }

        // Note that while running in the editor, we need to check for changes due to:
        // 1. Editor loads proxy objects first so we need to change when the texture is properly loaded.
        // 2. Texture change when the layer has modified the Material Index Map Texture.
        bool hasChanged = false;
        var terrainMat = TerrainMap.TerrainMaterial;
        if (terrainMat is null)
        {
            return;
        }
        if (_cachedDiffuseMapFeature is not null)
        {
            var cachedMatParams = _cachedTerrainMaterial.Passes[0].Parameters;
            if (_cachedEditPreviewDiffuseMapSettings is not null)
            {
                // Check if Material Index/Weight Map Texture needs to be changed
                if (_nextMaterialIndexMapTexture is not null)
                {
                    if (_cachedEditPreviewDiffuseMapSettings.OverrideMaterialIndexMapTexture is not null)
                    {
                        _pendingDisposables.Add(_cachedEditPreviewDiffuseMapSettings.OverrideMaterialIndexMapTexture);
                    }
                    _cachedEditPreviewDiffuseMapSettings.OverrideMaterialIndexMapTexture = _nextMaterialIndexMapTexture;
                    _nextMaterialIndexMapTexture = null;
                }
                if (_nextTerrainMaterialWeightMapTexture is not null)
                {
                    if (_cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture is not null)
                    {
                        _pendingDisposables.Add(_cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture);
                    }
                    _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture = _nextTerrainMaterialWeightMapTexture;
                    _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapSize = TerrainMap.HeightmapTextureSize.ToVector2();
                    if (_cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture is Texture materialWeightMapTexture)
                    {
                        _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapSize = new(materialWeightMapTexture.Width, materialWeightMapTexture.Height);
                    }
                    _nextTerrainMaterialWeightMapTexture = null;

                    cachedMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapTexture);
                    cachedMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMapSize, _cachedEditPreviewDiffuseMapSettings.TerrainMaterialWeightMapSize);
                    hasChanged = true;
                }
                if (_nextEditLayerMaterialWeightMapTexture is not null)
                {
                    //
                    if (_cachedEditPreviewDiffuseMapSettings.EditLayerMaterialWeightMapTexture is not null)
                    {
                        _pendingDisposables.Add(_cachedEditPreviewDiffuseMapSettings.EditLayerMaterialWeightMapTexture);
                    }
                    _cachedEditPreviewDiffuseMapSettings.EditLayerMaterialWeightMapTexture = _nextEditLayerMaterialWeightMapTexture;
                    _nextEditLayerMaterialWeightMapTexture = null;

                    cachedMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.EditLayerMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings.EditLayerMaterialWeightMapTexture);
                    hasChanged = true;
                }
            }
            var materialIndexMapTexture = _cachedEditPreviewDiffuseMapSettings?.OverrideMaterialIndexMapTexture ?? TerrainMap.MaterialIndexMapTexture;
            if (_cachedDiffuseMapFeature.MaterialIndexMap != materialIndexMapTexture)
            {
                _cachedDiffuseMapFeature.MaterialIndexMap = materialIndexMapTexture;
                cachedMatParams.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMap, _cachedDiffuseMapFeature.MaterialIndexMap);
                hasChanged = true;
            }
            if (_cachedDiffuseMapFeature.MaterialIndexMapSize != TerrainMap.MaterialIndexMapSize)
            {
                _cachedDiffuseMapFeature.MaterialIndexMapSize = TerrainMap.MaterialIndexMapSize;
                cachedMatParams.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMapSize, _cachedDiffuseMapFeature.MaterialIndexMapSize);
                hasChanged = true;
            }
            if (_cachedDiffuseMapFeature.DiffuseMapTextureArray != terrainMat.DiffuseMapTextureArray)
            {
                _cachedDiffuseMapFeature.DiffuseMapTextureArray = terrainMat.DiffuseMapTextureArray;
                cachedMatParams.Set(MaterialTerrainDiffuseMapKeys.DiffuseMap, _cachedDiffuseMapFeature.DiffuseMapTextureArray);
                hasChanged = true;
            }
            if (_cachedDiffuseMapFeature.NormalMapTextureArray != terrainMat.NormalMapTextureArray)
            {
                _cachedDiffuseMapFeature.NormalMapTextureArray = terrainMat.NormalMapTextureArray;
                cachedMatParams.Set(MaterialTerrainDiffuseMapKeys.NormalMap, _cachedDiffuseMapFeature.NormalMapTextureArray);
                hasChanged = true;
            }
            if (_cachedDiffuseMapFeature.HeightBlendMapTextureArray != terrainMat.HeightBlendMapTextureArray)
            {
                _cachedDiffuseMapFeature.HeightBlendMapTextureArray = terrainMat.HeightBlendMapTextureArray;
                cachedMatParams.Set(MaterialTerrainDiffuseMapKeys.HeightBlendMap, _cachedDiffuseMapFeature.HeightBlendMapTextureArray);
                hasChanged = true;
            }
            if (hasChanged)
            {
                // Materials are cloned so we also need to set the active models
                foreach (var (_, modelComp) in _chunkModelIdToActiveModelComponent)
                {
                    if (modelComp.Materials.TryGetValue(key: 0, out var material))
                    {
                        var activeMatParams = material.Passes[0].Parameters;
                        activeMatParams.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMap, _cachedDiffuseMapFeature.MaterialIndexMap);
                        activeMatParams.Set(MaterialTerrainDiffuseMapKeys.MaterialIndexMapSize, _cachedDiffuseMapFeature.MaterialIndexMapSize);
                        activeMatParams.Set(MaterialTerrainDiffuseMapKeys.DiffuseMap, _cachedDiffuseMapFeature.DiffuseMapTextureArray);
                        activeMatParams.Set(MaterialTerrainDiffuseMapKeys.NormalMap, _cachedDiffuseMapFeature.NormalMapTextureArray);
                        activeMatParams.Set(MaterialTerrainDiffuseMapKeys.HeightBlendMap, _cachedDiffuseMapFeature.HeightBlendMapTextureArray);

                        activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings?.TerrainMaterialWeightMapTexture);
                        activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.TerrainMaterialWeightMapSize, _cachedEditPreviewDiffuseMapSettings?.TerrainMaterialWeightMapSize ?? TerrainMap.HeightmapTextureSize.ToVector2());
                        activeMatParams.Set(MaterialTerrainEditPreviewDiffuseMapKeys.EditLayerMaterialWeightMap, _cachedEditPreviewDiffuseMapSettings?.EditLayerMaterialWeightMapTexture);
                    }
                }
            }
        }
#if GAME_EDITOR
        hasChanged = false;
        if (_cachedEditPreviewHeightmapFeature is not null)
        {
            var matParams = _cachedTerrainMaterial.Passes[0].Parameters;
            // Check if Heightmap Texture needs to be changed
            if (_nextOverrideHeightmapTexture is not null)
            {
                if (_overrideHeightmapTexture is not null)
                {
                    _pendingDisposables.Add(_overrideHeightmapTexture);
                }
                _overrideHeightmapTexture = _nextOverrideHeightmapTexture;
                _nextOverrideHeightmapTexture = null;
            }
            if (_cachedEditPreviewHeightmapFeature.TerrainHeightmap != _overrideHeightmapTexture)
            {
                _cachedEditPreviewHeightmapFeature.TerrainHeightmap = _overrideHeightmapTexture;
                matParams.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmap, _cachedEditPreviewHeightmapFeature.TerrainHeightmap);
                hasChanged = true;
            }
            var initialBrushWorldPosition = _initialBrushWorldPosition;
            if (_nextInitialBrushWorldPosition is Vector3 nextInitialBrushWorldPosition)
            {
                _initialBrushWorldPosition = nextInitialBrushWorldPosition;
                _nextInitialBrushWorldPosition = null;
                initialBrushWorldPosition = nextInitialBrushWorldPosition;
                hasChanged = true;
            }
            if (hasChanged || _terrainEditPreviewHeightmapFeatureHasPendingChanges)
            {
                // Materials are cloned so we also need to set the active models
                foreach (var (_, modelComp) in _chunkModelIdToActiveModelComponent)
                {
                    if (modelComp.Materials.TryGetValue(key: 0, out var material))
                    {
                        var activeMatParams = material.Passes[0].Parameters;
                        activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.TerrainHeightmap, _cachedEditPreviewHeightmapFeature.TerrainHeightmap);
                        activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.MaxAdjustmentHeightValue, _cachedEditPreviewHeightmapFeature.MaxAdjustmentHeightValue);
                        activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.HeightmapPaintModeType, (uint)_cachedEditPreviewHeightmapFeature.PaintModeType);
                        activeMatParams.Set(MaterialTerrainEditPreviewHeightmapKeys.InitialBrushWorldPosition, initialBrushWorldPosition);
                    }
                }
                _terrainEditPreviewHeightmapFeatureHasPendingChanges = false;
            }
        }
        if (_nextEditPreviewType is TerrainMapEditPreviewType editPreviewType)
        {
            foreach (var (_, modelComp) in _chunkModelIdToActiveModelComponent)
            {
                if (modelComp.Materials.TryGetValue(key: 0, out var material))
                {
                    var activeMatParams = material.Passes[0].Parameters;
                    activeMatParams.Set(MaterialTerrainEditPreviewInputSharedKeys.EditPreviewType, (uint)editPreviewType);
                }
            }
            _nextEditPreviewType = null;
        }
#endif
    }

    record MaterialTerrainEditPreviewDiffuseMapSettings
    {
        public MaterialMapPaintModeType PaintModeType;

        public Texture? OverrideMaterialIndexMapTexture;

        public Texture? TerrainMaterialWeightMapTexture;
        public Vector2 TerrainMaterialWeightMapSize;

        public Texture? EditLayerMaterialWeightMapTexture;
    }
}

readonly record struct PaintableTerrainMeshData
{
    public required PaintRenderTargetTexture StrokeMapRenderTarget { get; init; }
    /// <summary>
    /// The chunk index of the mesh.
    /// </summary>
    public required TerrainChunkIndex2d ChunkIndex { get; init; }
    /// <summary>
    /// The sub-chunk index of the mesh.
    /// </summary>
    public required TerrainChunkSubCellIndex2d ChunkSubCellIndex { get; init; }
    /// <summary>
    /// The heightmap area used by the sub-chunk/mesh.
    /// </summary>
    public required Rectangle HeightmapTextureRegion { get; init; }
}

readonly record struct PaintableTerrainMeshResultData
{
    public required Array2d<float> StrokeMapData { get; init; }
    /// <summary>
    /// The chunk index of the mesh.
    /// </summary>
    public required TerrainChunkIndex2d ChunkIndex { get; init; }
    /// <summary>
    /// The sub-chunk index of the mesh.
    /// </summary>
    public required TerrainChunkSubCellIndex2d ChunkSubCellIndex { get; init; }
    /// <summary>
    /// The heightmap area used by the sub-chunk/mesh.
    /// </summary>
    public required Rectangle HeightmapTextureRegion { get; init; }
}

enum PaintableTerrainEditType
{
    GetOnly = 0,
    Heightmap,
    MaterialIndexMap
}
