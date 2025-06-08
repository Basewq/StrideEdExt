using SceneEditorExtensionExample.SharedData.Terrain3d;
using SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;
using SceneEditorExtensionExample.WorldTerrain.TerrainMesh;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Core.Streaming;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d;

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
    private GraphicsContext _graphicsContext = default!;
    private GraphicsDevice _graphicsDevice = default!;
    private ContentManager _contentManager = default!;

    // The visible chunks
    private Dictionary<TerrainChunkModelId, ModelComponent> _chunkModelIdToActiveModelComponent = [];
    private Dictionary<TerrainChunkModelId, ModelComponent> _chunkModelIdToActiveModelComponentProcessing = [];

    private Entity? _staticPhysicsColliderEntity;
    //private Dictionary<TileChunkPhysicsId, StaticColliderComponent> _chunkPhysicsIdToPhysicsComponent = [];
    //private Dictionary<TileChunkPhysicsId, StaticColliderComponent> _chunkPhysicsIdToPhysicsComponentProcessing = [];

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

    public TerrainMap? TerrainMap { get; set; }

    /// <summary>
    /// The camera used to determine which chunks should be visible.
    /// </summary>
    public CameraComponent? CameraComponent { get; set; }
    public float MaxChunkRenderDistance { get; set; } = 100;

    private bool _isModelMaterialUpdateRequired = true;
    private Material? _terrainMaterial;
    public Material? TerrainMaterial
    {
        get => _terrainMaterial;
        set
        {
            _isModelMaterialUpdateRequired = _terrainMaterial != value;
            _terrainMaterial = value;
        }
    }

    internal void Initialize(IServiceRegistry serviceRegistry)
    {
        var game = serviceRegistry.GetSafeServiceAs<IGame>();
        _graphicsContext = game.GraphicsContext;
        _graphicsDevice = game.GraphicsDevice;
        _contentManager = game.Content;

        if (TerrainMap is not null)
        {
        }
        if (Entity.EntityManager.ExecutionMode == ExecutionMode.Runtime && TerrainMap is not null)
        {
            // Only load the asset as-is when running as an app, the editor loads the meshes via TerrainMapPainterProcessor
            TerrainMap.Initialize();

            //BuildInstancingModels(Terrain, _displayGrid, Terrain.TileSetNameToTileSetPrefabMap);
            //BuildPhysicsColliders(_displayGrid, Terrain, _tileRuleSet, Terrain.TileSetNameToTileSetPrefabMap);
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

        if (_staticPhysicsColliderEntity is not null)
        {
            _staticPhysicsColliderEntity.Scene = null;
            _staticPhysicsColliderEntity = null;
        }
        //_chunkPhysicsIdToPhysicsComponent.Clear();
        //_chunkPhysicsIdToPhysicsComponentProcessing.Clear();
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
    //private readonly List<TerrainChunkIndex2d> _reusedChunkIndices = [];
    private readonly List<TerrainChunkIndex2d> _reusedDebugDisplayChunkIndices = [];
    internal void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
        if (TerrainMap is null || !TerrainMap.IsInitialized)
        {
            return;
        }
#if GAME_EDITOR
        var painterComp = Entity.Get<TerrainMapPainterComponent>();
        if (painterComp is null)
        {
            return;
        }
#endif
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

            //if (!_chunkIndexToModelDataList.TryGetValue(visibleChunkIndex, out var modelDataList))
            //{
            //    continue;
            //}
            //foreach (var modelData in modelDataList)
            //{
            // Reuse existing modelComponent if possible

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
                        var heightmapData = TerrainMap.HeightmapData;
                        var terrainMeshData = TerrainMeshData.Generate(subChunk.HeightmapTextureRegion, TerrainMap.MapSize, heightmapData, TerrainMap.MeshQuadSize, TerrainMap.HeightRange);
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
                        subChunk.Mesh = mesh;
                        hasChangedMesh = true;
                    }
                    var chunkModelId = new TerrainChunkModelId(visibleChunkIndex, chunkSubCellIndex);
                    if (_chunkModelIdToActiveModelComponentProcessing.TryGetValue(chunkModelId, out var modelComponent))
                    {
                        //_reusedChunkIndices.Add(visibleChunkIndex);
                        _chunkModelIdToActiveModelComponentProcessing.Remove(chunkModelId);

                        if (hasChangedMesh)
                        {
                            var model = new Model
                            {
                                mesh
                            };
                            model.BoundingBox = mesh.BoundingBox;
                            model.BoundingSphere = mesh.BoundingSphere;
                            modelComponent.Model = model;
                        }
                        if (_isModelMaterialUpdateRequired && TerrainMaterial is not null)
                        {
                            modelComponent.Materials[0] = TerrainMaterial;
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
                    modelComponent.BoundingBox = mesh.BoundingBox;
                    modelComponent.BoundingSphere = mesh.BoundingSphere;
                }
            }
            if (_isModelMaterialUpdateRequired && TerrainMaterial is not null)
            {
                _isModelMaterialUpdateRequired = false;
            }
        }

        //foreach (var chunkModelId in _reusedChunkIndices)
        //{
        //    _chunkIndexIdToActiveModelComponentProcessing.Remove(chunkModelId);
        //}
        //_reusedChunkIndices.Clear();

        // Any remaining chunks are no longer visible and should be removed
        foreach (var (chunkModelId, modelComponent) in _chunkModelIdToActiveModelComponentProcessing)
        {
            Debug.WriteLineIf(modelComponent.Entity.Scene is not null, $"Terrain Map Chunk no longer visible: {chunkModelId}");
            modelComponent.Entity.Scene = null;
        }
        _chunkModelIdToActiveModelComponentProcessing.Clear();
    }

    //private readonly List<ColliderShape> _colliderShapeList = [];
    //public void BuildPhysicsColliders(TerrainDisplayGrid3d displayGrid, ITerrainMap terrain, TileRuleSet tileRuleSet, Dictionary<string, Prefab> tileSetNameToTileSetPrefabMap)
    //{
    //    // Swap the "actual" dictionary with the "processing" dictionary, so that the "actual" dictionary is
    //    // initially empty and the "processing" dictionary contains any chunks we can potentially reuse.
    //    Utilities.Swap(ref _chunkPhysicsIdToPhysicsComponent, ref _chunkPhysicsIdToPhysicsComponentProcessing);

    //    if (_staticPhysicsColliderEntity is null)
    //    {
    //        _staticPhysicsColliderEntity = new Entity("TerrainStaticPhysicsCollider");
    //        _staticPhysicsColliderEntity.SetParent(Entity);
    //    }

    //    // Any remaining chunks are no longer valid and should be removed
    //    foreach (var (_, physComp) in _chunkPhysicsIdToPhysicsComponentProcessing)
    //    {
    //        _staticPhysicsColliderEntity.Remove(physComp);

    //    }
    //    _chunkPhysicsIdToPhysicsComponentProcessing.Clear();

    //}

    private bool TryCreateModelComponent(TerrainChunkModelId chunkModelId, Mesh mesh, [NotNullWhen(true)] out ModelComponent? modelComponent)
    {
        var modelChunkEntity = new Entity($"Tile Map: {chunkModelId}");
        var model = new Model
        {
            mesh
        };
        model.BoundingBox = mesh.BoundingBox;
        model.BoundingSphere = mesh.BoundingSphere;
        modelComponent = new ModelComponent
        {
            Model = model,
            IsShadowCaster = true,
        };
        // Because we divide instancing into chunks we need to clone the material
        // so each model chunk have separate instancing data.
        if (TerrainMaterial is not null)
        {
            // Make a clone of the material
            var newMaterial = TerrainMaterial.CloneMaterial();
            modelComponent.Materials.Add(key: 0, newMaterial);
        }

        modelChunkEntity.Add(modelComponent);
        return true;
    }

    //private record struct TileChunkPhysicsId(TerrainChunkIndex2d chunkIndex, TileCellType TileCellType);
}
