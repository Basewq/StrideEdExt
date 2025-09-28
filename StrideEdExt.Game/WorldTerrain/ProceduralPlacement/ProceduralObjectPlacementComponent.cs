using Stride.Core;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Rendering;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.WorldTerrain.Foliage;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace StrideEdExt.WorldTerrain.ProceduralPlacement;

/// <summary>
/// Procedural Placement Manager for a given <see cref="ObjectPlacementMap"/> asset, which manages
/// the rendering instancing dividing it into chunks.
/// </summary>
[ComponentCategory("Procedural")]
[DataContract]
[DefaultEntityComponentProcessor(typeof(ProceduralObjectPlacementProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
public class ProceduralObjectPlacementComponent : TerrainMapChunkStreamListenerBase
{
    private GraphicsDevice _graphicsDevice = default!;
    private ContentManager _contentManager = default!;

    private readonly List<Model> _pendingDisposeModels = [];
    private readonly List<IDisposable> _pendingDisposables = [];

    // The visible chunks
    private readonly Dictionary<TerrainChunkIndex2d, ChunkInstanceData> _chunkIndexToInstanceDataMap = [];
    private readonly Dictionary<TerrainChunkIndex2d, ChunkInstanceData> _chunkIndexToInstanceDataMapProcessing = [];

    private bool _isEnabledChanged = false;
    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabledChanged = _isEnabledChanged || (_isEnabled != value);
            _isEnabled = value;
        }
    }

    [DataMemberIgnore]
    internal bool IsEditorEnabled { get; set; }
    private OverrideObjectPlacementMapData _overrideObjectPlacementMapData = default;

    public ObjectPlacementMap? ObjectPlacementMap { get; set; }

    ///// <summary>
    ///// The camera used to determine which chunks should be visible.
    ///// </summary>
    ////public CameraComponent? CameraComponent { get; set; }
    ////public float MaxChunkRenderDistance { get; set; } = 100;

    internal void Initialize(IServiceRegistry serviceRegistry)
    {
        var game = serviceRegistry.GetSafeServiceAs<IGame>();
        _graphicsDevice = game.GraphicsDevice;
        _contentManager = game.Content;

        //if (Entity.EntityManager.ExecutionMode == ExecutionMode.Runtime && ObjectPlacementMap is not null)
        //{
        //    // Only load the asset as-is when running as an app, the editor loads the meshes via TerrainMapEditorProcessor
        //    ObjectPlacementMap.Initialize();
        //}
    }

    internal void Deinitialize()
    {
        _graphicsDevice = null!;
        _contentManager = null!;

        foreach (var (_, chunkInstanceData) in _chunkIndexToInstanceDataMap)
        {
            chunkInstanceData.Clear();
        }
        _chunkIndexToInstanceDataMap.Clear();
        foreach (var (_, chunkInstanceData) in _chunkIndexToInstanceDataMapProcessing)
        {
            chunkInstanceData.Clear();
        }
        _chunkIndexToInstanceDataMapProcessing.Clear();

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

        if (_isEnabledChanged)
        {
            if (IsEnabled)
            {
                foreach (var (_, chunkInstanceData) in _chunkIndexToInstanceDataMap)
                {
                    chunkInstanceData.SetEntityParent(Entity);
                }
            }
            else
            {
                foreach (var (_, chunkInstanceData) in _chunkIndexToInstanceDataMap)
                {
                    chunkInstanceData.RemoveEntitiesFromScene();
                }
            }
        }
    }

    private readonly List<TerrainChunkIndex2d> _visibleChunkIndexList = [];
    public override void OnChunkVisible(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex, TerrainChunk chunk)
    {
        Debug.WriteLine($"{nameof(ProceduralObjectPlacementComponent)}.{nameof(OnChunkVisible)}: {chunkIndex}");

        _visibleChunkIndexList.Add(chunkIndex);
        GenerateChunkObjects(terrainMap, chunkIndex, chunk);
    }

    private void GenerateChunkObjects(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex, TerrainChunk chunk)
    {
        if (ObjectPlacementMap is null)
        {
            return;
        }

        ObjectPlacementsChunkData? chunkData = null;
        if (_overrideObjectPlacementMapData.ChunkIndexToChunkDataMap is not null)
        {
            // Use the override data
            if (!_overrideObjectPlacementMapData.ChunkIndexToChunkDataMap.TryGetValue(chunkIndex, out chunkData))
            {
                return;
            }
        }
        else
        {
            // Load data from content
            chunkData = new ObjectPlacementsChunkData();
            if (ObjectPlacementMap.TryGetChunkDataUrl(chunkIndex, out var chunkDataUrl))
            {
#if GAME_EDITOR
                lock (ObjectPlacementsChunkData.SerializerLock)     // Only need lock for the editor due to asset compiler serializing chunk data
#endif
                {
                    var fileProvider = _contentManager.FileProvider;
                    if (fileProvider.FileExists(chunkDataUrl))
                    {
                        using var chunkDataStream = fileProvider.OpenStream(chunkDataUrl, VirtualFileMode.Open, VirtualFileAccess.Read, VirtualFileShare.Read, StreamFlags.Seekable);
                        var reader = new BinarySerializationReader(chunkDataStream);
                        chunkData.Deserialize(reader);
                    }
                    else
                    {
                        Debug.WriteLine($"{nameof(ProceduralObjectPlacementComponent)}.{nameof(GenerateChunkObjects)}: Chunk data file does not exist for chunk index: {chunkIndex} - url: {chunkDataUrl}");
                        return;
                    }
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(ProceduralObjectPlacementComponent)}.{nameof(GenerateChunkObjects)}: Chunk data url does not exist for chunk index: {chunkIndex}");
                return;
            }
        }

        if (_chunkIndexToInstanceDataMap.TryGetValue(chunkIndex, out var chunkInstanceData))
        {
            // Reuse container
            chunkInstanceData.Clear();
        }
        else
        {
            chunkInstanceData = new ChunkInstanceData();
            _chunkIndexToInstanceDataMap[chunkIndex] = chunkInstanceData;
        }

        // Generate models
        var modelAssetUrlList = _overrideObjectPlacementMapData.ModelAssetUrlList ?? ObjectPlacementMap.ModelAssetUrlRefList;
        var prefabAssetUrlList = _overrideObjectPlacementMapData.PrefabAssetUrlList ?? ObjectPlacementMap.PrefabAssetUrlRefList;
        foreach (var modelPlacement in chunkData.ModelPlacements)
        {
            if (modelPlacement.Placements.Count == 0)
            {
                continue;
            }
            if (!modelAssetUrlList.TryGetValue(modelPlacement.ModelAssetUrlListIndex, out var assetUrlRef)
                || assetUrlRef is null)
            {
                continue;
            }
            var assetUrl = assetUrlRef.Url;
            if (string.IsNullOrEmpty(assetUrl))
            {
                continue;
            }

            var chunkAssetId = new ObjectPlacementChunkAssetId(chunkIndex, assetUrl);

            var worldTransformArray = new Matrix[modelPlacement.Placements.Count];
            var placementsSpan = CollectionsMarshal.AsSpan(modelPlacement.Placements);
            for (int i = 0; i < placementsSpan.Length; i++)
            {
                worldTransformArray[i] = placementsSpan[i].GetWorldTransformMatrix();
            }

            switch (modelPlacement.ModelType)
            {
                case ObjectPlacementModelType.Foliage:
                    {
                        var chunkIndex3d = new Int3(chunkIndex.X, y: 0, chunkIndex.Z);
                        var foliageChunkModelId = new FoliageChunkModelId(chunkIndex3d, assetUrl);
                        if (!TryCreateChunkInstanceFoliageGroupData(foliageChunkModelId, out var foliageGroupData, out var instancingEntity))
                        {
                            Debug.WriteLine($"Failed to load foliage model: {assetUrl}");
                            continue;
                        }

                        var foliageInstanceDataArray = new FoliageInstanceData[placementsSpan.Length];
                        for (int i = 0; i < placementsSpan.Length; i++)
                        {
                            foliageInstanceDataArray[i].SurfaceNormalModelSpace = placementsSpan[i].SurfaceNormalModelSpace;
                        }

                        var foliageChunkInstComp = foliageGroupData.FoliageChunkInstancingComponent;
                        foliageChunkInstComp.InstancingArray.UpdateWorldMatrices(worldTransformArray);
                        foliageChunkInstComp.InstanceDataBuffer = _graphicsDevice.CreateShaderBuffer<FoliageInstanceData>(foliageInstanceDataArray.Length);

                        chunkInstanceData.ChunkAssetIdToFoliageGroupData.Add(chunkAssetId, foliageGroupData);
                    }
                    break;
                case ObjectPlacementModelType.Static:
                default:
                    {
                        if (!TryCreateChunkInstanceStaticModelInstancingGroupData(assetUrlRef, out var staticModelInstancingGroupData, out var instancingEntity))
                        {
                            Debug.WriteLine($"Failed to load instancing model: {assetUrl}");
                            continue;
                        }

                        staticModelInstancingGroupData.InstancingArray.UpdateWorldMatrices(worldTransformArray);

                        chunkInstanceData.ChunkAssetIdToStaticModelInstancingGroupData.Add(chunkAssetId, staticModelInstancingGroupData);
                    }
                    break;
            }
        }

        // Generate prefabs
        Dictionary<string, InstancingComponent>? modelUrlToInstancingComponentMap = null;
        foreach (var prefabPlacement in chunkData.PrefabPlacements)
        {
            if (prefabPlacement.Placements.Count == 0)
            {
                continue;
            }
            if (!prefabAssetUrlList.TryGetValue(prefabPlacement.PrefabAssetUrlListIndex, out var assetUrlRef)
                || assetUrlRef is null)
            {
                continue;
            }
            var assetUrl = assetUrlRef.Url;
            if (string.IsNullOrEmpty(assetUrl))
            {
                continue;
            }

            var chunkAssetId = new ObjectPlacementChunkAssetId(chunkIndex, assetUrl);
            if (!TryCreateChunkInstancePrefabEntityGroupData(assetUrlRef, out var prefabEntityGroupData, out var prefab))
            {
                Debug.WriteLine($"Failed to load prefab: {assetUrl}");
                continue;
            }

            var placementsSpan = CollectionsMarshal.AsSpan(prefabPlacement.Placements);
            for (int i = 0; i < placementsSpan.Length; i++)
            {
                var prefabEntities = prefab.Instantiate();
                foreach (var entity in prefabEntities)
                {
                    var transformComp = entity.Transform;
                    transformComp.Position = placementsSpan[i].Position;
                    transformComp.Rotation = placementsSpan[i].Orientation;
                    transformComp.Scale = placementsSpan[i].Scale;

                    BuildPrefabModelInstancing(entity, ref modelUrlToInstancingComponentMap);

                    prefabEntityGroupData.PrefabEntities.Add(entity);
                }
            }

            chunkInstanceData.ChunkAssetIdToPrefabEntityGroupData.Add(chunkAssetId, prefabEntityGroupData);
        }
        // Collect all generated prefabs
        if (modelUrlToInstancingComponentMap is not null)
        {
            foreach (var (_, instancingComp) in modelUrlToInstancingComponentMap)
            {
                chunkInstanceData.PrefabInstancingEntities.Add(instancingComp.Entity);
            }
        }

        bool isVisible = IsEnabled;
#if GAME_EDITOR
        isVisible = isVisible && IsEditorEnabled;
#endif
        if (isVisible)
        {
            chunkInstanceData.SetEntityParent(Entity);
        }
    }

    public override void OnChunkNotVisible(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex)
    {
        Debug.WriteLine($"{nameof(ProceduralObjectPlacementComponent)}.{nameof(OnChunkNotVisible)}: {chunkIndex}");

        _visibleChunkIndexList.Remove(chunkIndex);
        if (ObjectPlacementMap is null)
        {
            return;
        }

        if (_chunkIndexToInstanceDataMap.TryGetValue(chunkIndex, out var chunkInstanceData))
        {
            chunkInstanceData.Clear();

            _chunkIndexToInstanceDataMap.Remove(chunkIndex);
        }
    }

    public void RebuildAllVisibleChunks(TerrainMap terrainMap)
    {
        foreach (var chunkIndex in _visibleChunkIndexList)
        {
            if (terrainMap.TryGetChunk(chunkIndex, out var chunk))
            {
                GenerateChunkObjects(terrainMap, chunkIndex, chunk);
            }
        }
    }

    internal void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
#if GAME_EDITOR
        if (!IsEditorEnabled)
        {
            return;
        }
#endif
    }

    private void BuildPrefabModelInstancing(Entity entity, ref Dictionary<string, InstancingComponent>? existingModelUrlToInstancingComponentMap)
    {
        if (entity.TryGetComponent<PrefabModelInstanceComponent>(out var prefabModelInstancingComp))
        {
            existingModelUrlToInstancingComponentMap ??= new();     // Lazy generate

            var modelUrlRef = prefabModelInstancingComp.ModelUrlRef;
            var modelUrl = modelUrlRef?.Url;
            if (modelUrlRef is not null && modelUrl is not null
                && !existingModelUrlToInstancingComponentMap.TryGetValue(modelUrl, out var instancingComponent))
            {
                if (TryCreateInstancingEntityForPrefab(modelUrlRef, out var instancingEntity))
                {
                    if (!entity.TryGetComponent<InstanceComponent>(out var instanceComponent))
                    {
                        instanceComponent = new InstanceComponent();
                        entity.Add(instanceComponent);
                    }
                    instanceComponent.Master = instancingComponent;
                }
            }
        }

        foreach (var childTransformComp in entity.Transform.Children)
        {
            BuildPrefabModelInstancing(childTransformComp.Entity, ref existingModelUrlToInstancingComponentMap);
        }
    }

    private bool TryCreateInstancingEntityForPrefab(
        UrlReference<Model> modelUrlRef,
        [NotNullWhen(true)] out Entity? instancingEntity)
    {
        var model = _contentManager.Load(modelUrlRef);
        if (model is null)
        {
            // The editor can sometimes bug out and not load the model...
            //throw new ApplicationException($"Failed to load model: {modelUrlRef.Url}");
            instancingEntity = null;
            return false;
        }

        instancingEntity = new Entity();

        var modelComp = new ModelComponent
        {
            Model = model,
            IsShadowCaster = model.Materials.FirstOrDefault()?.IsShadowCaster ?? false,
        };

        instancingEntity.Add(modelComp);
        var instancingEntityTransform = new InstancingEntityTransform();
        var instComp = new InstancingComponent
        {
            Type = instancingEntityTransform
        };
        instancingEntity.Add(instComp);

        return true;
    }

    private bool TryCreateChunkInstanceStaticModelInstancingGroupData(
        UrlReference<Model> modelUrlRef,
        [NotNullWhen(true)] out ChunkInstanceStaticModelInstancingGroupData? chunkInstanceStaticModelInstancingGroupData,
        [NotNullWhen(true)] out Entity? instancingEntity)
    {
        var model = _contentManager.Load(modelUrlRef);
        if (model is null)
        {
            // The editor can sometimes bug out and not load the model...
            //throw new ApplicationException($"Failed to load model: {modelUrlRef.Url}");
            chunkInstanceStaticModelInstancingGroupData = null;
            instancingEntity = null;
            return false;
        }

        instancingEntity = new Entity();

        var modelComp = new ModelComponent
        {
            Model = model,
            IsShadowCaster = model.Materials.FirstOrDefault()?.IsShadowCaster ?? false,
        };

        instancingEntity.Add(modelComp);
        var instancingArray = new InstancingUserArray();
        var instComp = new InstancingComponent
        {
            Type = instancingArray
        };
        instancingEntity.Add(instComp);

        chunkInstanceStaticModelInstancingGroupData = new()
        {
            ModelComponent = modelComp,
            InstancingComponent = instComp,
            InstancingArray = instancingArray,
        };

        return true;
    }

    private bool TryCreateChunkInstanceFoliageGroupData(
        FoliageChunkModelId chunkModelId,
        [NotNullWhen(true)] out ChunkInstanceFoliageGroupData? chunkInstanceFoliageGroupData,
        [NotNullWhen(true)] out Entity? instancingEntity)
    {
        var modelUrl = chunkModelId.ModelUrl;
        var model = _contentManager.Load<Model>(modelUrl);
        if (model is null)
        {
            // The editor can sometimes bug out and not load the model...
            //throw new ApplicationException($"Failed to load model: {modelUrl}");
            chunkInstanceFoliageGroupData = null;
            instancingEntity = null;
            return false;
        }

        instancingEntity = new Entity();

        var modelComp = new ModelComponent
        {
            Model = model,
            IsShadowCaster = model.Materials.FirstOrDefault()?.IsShadowCaster ?? false,
        };
        //// Because we divide instancing into chunks we need to clone the material
        //// so each model chunk have separate instancing data.
        //var material = modelComp.Model.Materials.FirstOrDefault();
        //if (material is not null)
        //{
        //    // Make a clone of the material
        //    var newMaterial = CloneMaterial(material.Material);
        //    modelComp.Materials.Add(key: 0, newMaterial);
        //}

        instancingEntity.Add(modelComp);
        var instancingArray = new InstancingUserArray();
        var instComp = new InstancingComponent
        {
            Type = instancingArray
        };
        instancingEntity.Add(instComp);

        var chunkInstancingComponent = new FoliageChunkInstancingComponent
        {
            ChunkModelId = chunkModelId,
            ModelComponent = modelComp,
            InstancingArray = instancingArray,
            InstanceDataBuffer = null       // Set later
        };
        instancingEntity.Add(chunkInstancingComponent);

        chunkInstanceFoliageGroupData = new()
        {
            FoliageChunkInstancingComponent = chunkInstancingComponent
        };

        return true;
    }

    private bool TryCreateChunkInstancePrefabEntityGroupData(
        UrlReference<Prefab> prefabUrlRef,
        [NotNullWhen(true)] out ChunkInstancePrefabEntityGroupData? chunkInstancePrefabEntityGroupData,
        [NotNullWhen(true)] out Prefab? prefab)
    {
        prefab = _contentManager.Load(prefabUrlRef);
        if (prefab is null)
        {
            // The editor can sometimes bug out and not load the prefab...
            //throw new ApplicationException($"Failed to load prefab: {prefabUrlRef.Url}");
            chunkInstancePrefabEntityGroupData = null;
            return false;
        }

        chunkInstancePrefabEntityGroupData = new();
        return true;
    }

    public void SetOverrideObjectPlacements(
        List<UrlReference<Model>> modelAssetUrlList,
        List<UrlReference<Prefab>> prefabAssetUrlList,
        Dictionary<TerrainChunkIndex2d, ObjectPlacementsChunkData> chunkIndexToChunkDataMap,
        TerrainMap terrainMap)
    {
        // HACK: We can't overwrite data in ObjectPlacementMap because Stride Editor
        // resets this to the asset's version during undo/redo, so we need the ability
        // to hold the 'in-progress' edited state.
        _overrideObjectPlacementMapData = new OverrideObjectPlacementMapData
        {
            ModelAssetUrlList = modelAssetUrlList,
            PrefabAssetUrlList = prefabAssetUrlList,
            ChunkIndexToChunkDataMap = chunkIndexToChunkDataMap,
        };

        RebuildAllVisibleChunks(terrainMap);
    }

    public void UnsetOverrideObjectPlacements(TerrainMap terrainMap)
    {
        _overrideObjectPlacementMapData = default;
        RebuildAllVisibleChunks(terrainMap);
    }

    private class ChunkInstanceData
    {
        public readonly Dictionary<ObjectPlacementChunkAssetId, ChunkInstanceFoliageGroupData> ChunkAssetIdToFoliageGroupData = [];
        public readonly Dictionary<ObjectPlacementChunkAssetId, ChunkInstanceStaticModelInstancingGroupData> ChunkAssetIdToStaticModelInstancingGroupData = [];
        public readonly Dictionary<ObjectPlacementChunkAssetId, ChunkInstancePrefabEntityGroupData> ChunkAssetIdToPrefabEntityGroupData = [];

        public readonly List<Entity> PrefabInstancingEntities = [];     // Instancing entities created from prefab entities that contained PrefabModelInstanceComponent

        public void Clear(bool removeFromScene = true)
        {
            if (removeFromScene)
            {
                RemoveEntitiesFromScene();
            }
            ChunkAssetIdToFoliageGroupData.Clear();
            ChunkAssetIdToStaticModelInstancingGroupData.Clear();
            ChunkAssetIdToPrefabEntityGroupData.Clear();
            PrefabInstancingEntities.Clear();
        }

        public void SetEntityParent(Entity parentEntity)
        {
            foreach (var (_, groupData) in ChunkAssetIdToFoliageGroupData)
            {
                var entity = groupData.FoliageChunkInstancingComponent.Entity;
                entity.SetParent(parentEntity);
            }
            foreach (var (_, groupData) in ChunkAssetIdToStaticModelInstancingGroupData)
            {
                var entity = groupData.ModelComponent.Entity;
                entity.SetParent(parentEntity);
            }

            foreach (var entity in PrefabInstancingEntities)
            {
                entity.SetParent(parentEntity);
            }
            foreach (var (_, groupData) in ChunkAssetIdToPrefabEntityGroupData)
            {
                foreach (var entity in groupData.PrefabEntities)
                {
                    entity.SetParent(parentEntity);
                }
            }
        }

        public void RemoveEntitiesFromScene()
        {
            foreach (var (_, groupData) in ChunkAssetIdToFoliageGroupData)
            {
                var entity = groupData.FoliageChunkInstancingComponent.Entity;
                entity.Scene = null;
            }
            foreach (var (_, groupData) in ChunkAssetIdToStaticModelInstancingGroupData)
            {
                var entity = groupData.ModelComponent.Entity;
                entity.Scene = null;
            }

            foreach (var entity in PrefabInstancingEntities)
            {
                entity.Scene = null;
            }
            foreach (var (_, groupData) in ChunkAssetIdToPrefabEntityGroupData)
            {
                foreach (var entity in groupData.PrefabEntities)
                {
                    entity.Scene = null;
                }
            }
        }
    }

    private class ChunkInstanceFoliageGroupData
    {
        //public required ModelComponent ModelComponent;
        //public required InstancingComponent InstancingComponent;
        public required FoliageChunkInstancingComponent FoliageChunkInstancingComponent;
    }

    private class ChunkInstanceStaticModelInstancingGroupData
    {
        public required ModelComponent ModelComponent;
        public required InstancingComponent InstancingComponent;
        public required InstancingUserArray InstancingArray;
    }

    private class ChunkInstancePrefabEntityGroupData
    {
        public readonly List<Entity> PrefabEntities = [];
    }

    private record struct OverrideObjectPlacementMapData
    {
        public List<UrlReference<Model>>? ModelAssetUrlList { get; init; }
        public List<UrlReference<Prefab>>? PrefabAssetUrlList { get; init; }
        public Dictionary<TerrainChunkIndex2d, ObjectPlacementsChunkData>? ChunkIndexToChunkDataMap { get; init; }
    }
}
