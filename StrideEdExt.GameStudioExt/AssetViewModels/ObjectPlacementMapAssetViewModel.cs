using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.ViewModels;
using Stride.Assets.Presentation.ViewModel;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Assets.Quantum;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Engine;
using StrideEdExt.GameStudioExt.Assets.Transaction;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.AssetSerialization;
using StrideEdExt.SharedData.ProceduralPlacement;
using StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;
using StrideEdExt.SharedData.ProceduralPlacement.Layers;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.DensityMaps;
using StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Half = System.Half;

namespace StrideEdExt.GameStudioExt.AssetViewModels;

[AssetViewModel<ObjectPlacementMapAsset>]
public class ObjectPlacementMapAssetViewModel : AssetViewModel<ObjectPlacementMapAsset>
{
    delegate void AssetMemberChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetMemberNodeChangeEventArgs e);
    delegate void AssetNodeItemChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetItemNodeChangeEventArgs e);

    private readonly Logger _logger;

    private IEditorToRuntimeMessagingService? _editorToRuntimeMessagingService;
    private List<IDisposable> _onDestroyDisposables = [];

    private bool _onTransactionFinished_IsSpawnersUpdateRequired;
    private readonly List<PlacementLayerTree> _pendingLayerTreeUpdateList = [];

    private readonly Dictionary<Guid, PlacementLayerTree> _editorEntityIdToPlacementLayerTree = [];

    public ObjectPlacementMapAssetViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
        _logger = GlobalLogger.GetLogger(GetType().FullName!);
    }

    protected override void Initialize()
    {
        base.Initialize();
        var pluginService = ServiceProvider.Get<IAssetsPluginService>();
        var gameAssetsEditorPlugin = (GameAssetsEditorPlugin)pluginService.Plugins.First(x => x is GameAssetsEditorPlugin);
        Task.Run(async () =>
        {
            await gameAssetsEditorPlugin.ServicesRegistrationCompleted;
            _editorToRuntimeMessagingService = ServiceProvider.Get<IEditorToRuntimeMessagingService>();
            SubscribeRuntimeMessagingRequests();
        });

        if (Asset.ResourceFolderPath is not null)
        {
            var packageFolderPath = AssetItem.Package.FullPath.GetFullDirectory();
            Asset.EnsureFinalizeContentDeserialization(_logger, packageFolderPath);
        }

        Session.UndoRedoService.Done += OnUndoRedoServiceTransactionFinished;
        Session.UndoRedoService.Undone += OnUndoRedoServiceTransactionFinished;
        Session.UndoRedoService.Redone += OnUndoRedoServiceTransactionFinished;

        if (PropertyGraph is not null)
        {
            PropertyGraph.Changed += OnAssetPropertyGraphChanged;
        }
    }

    private void OnAssetPropertyGraphChanged(object? sender, AssetMemberNodeChangeEventArgs e)
    {
        if (UndoRedoService.TransactionInProgress)
        {
            //// Don't create action items if the change comes from the Base
            //if (!PropertyGraph.UpdatingPropertyFromBase)
            //{
            //    var overrideChange = new AssetContentValueChangeOperation(node, e.ChangeType, index, e.OldValue, e.NewValue, assetNodeChange.PreviousOverride, assetNodeChange.NewOverride, assetNodeChange.ItemId, Dirtiables);
            //    UndoRedoService.PushOperation(overrideChange);
            //}

            string memberName = e.Member.Name;
            if (memberName == nameof(ObjectPlacementMapAsset.TerrainMap))
            {

            }
            ////if (memberName == nameof(ObjectPlacementMapAsset.DensityMapTextureSize))
            ////{
            ////    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

            ////    var densitymapTextureSize = Asset.DensityMapTextureSize.ToSize2();
            ////    //Asset.ResizeMap(Asset.MapSize, assetTransactionBuilder);
            ////    if (Asset.DensityMapLayerDataList?.Count > 0)
            ////    {
            ////        foreach (var layerData in Asset.DensityMapLayerDataList)
            ////        {
            ////            layerData.UpdateForTerrainMapResized(densitymapTextureSize, assetTransactionBuilder);
            ////            layerData.IsSerializeIntermediateFileRequired = true;
            ////        }
            ////        assetTransactionBuilder.AddPostExecuteAction(() =>
            ////        {
            ////            _onTransactionFinished_IsDensityMapLayersUpdateRequired = true;
            ////        });
            ////        _onTransactionFinished_IsDensityMapLayersUpdateRequired = true;
            ////    }
            ////    if (Asset.SpawnerDataList?.Count > 0)
            ////    {
            ////        foreach (var layerData in Asset.SpawnerDataList)
            ////        {
            ////            layerData.UpdateForTerrainMapResized(densitymapTextureSize, assetTransactionBuilder);
            ////            layerData.IsSerializeIntermediateFileRequired = true;
            ////        }
            ////        assetTransactionBuilder.AddPostExecuteAction(() =>
            ////        {
            ////            _onTransactionFinished_IsSpawnersUpdateRequired = true;
            ////        });
            ////        _onTransactionFinished_IsSpawnersUpdateRequired = true;
            ////    }

            ////    Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
            ////    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
            ////    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
            ////    UndoRedoService.PushOperation(trxOp);
            ////}
        }
    }

    public override void Destroy()
    {
        Session.UndoRedoService.Done -= OnUndoRedoServiceTransactionFinished;
        Session.UndoRedoService.Undone -= OnUndoRedoServiceTransactionFinished;
        Session.UndoRedoService.Redone -= OnUndoRedoServiceTransactionFinished;

        if (PropertyGraph is not null)
        {
            PropertyGraph.Changed -= OnAssetPropertyGraphChanged;
        }

        foreach (var sub in _onDestroyDisposables)
        {
            sub.Dispose();
        }
        _onDestroyDisposables.Clear();

        base.Destroy();
    }

    protected override void OnSessionSaved()
    {
        var logger = new LoggerResult();

        var packageFolderPath = AssetItem.Package.FullPath.GetFullDirectory();

        using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
        {
            UndoRedoService.SetName(undoRedoTransaction, "ObjectPlacementMapAsset - Serialized Layer Data");
            var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

            Asset.SerializeIntermediateFiles(logger, packageFolderPath);

            var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
            var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
            UndoRedoService.PushOperation(trxOp);
        }
        if (Asset.HasObjectPlacementLayerSerializedIntermediateFiles)
        {
            Asset.HasObjectPlacementLayerSerializedIntermediateFiles = false;
            // HACK: OnSessionSaved occurs after the asset has already been serialized to disk,
            // but we've made additional changes so need to save it again.
            var assetAnalysisParams = new AssetAnalysisParameters()
            {
                IsProcessingUPaths = true,
                ConvertUPathTo = UPathType.Relative,
            };
            AssetAnalysis.Run(AssetItem, logger, assetAnalysisParams);      // Ensures we save our file/folder paths as relative to the package it belongs to.
            Package.SaveSingleAsset(AssetItem, logger);
            UpdateDirtiness(false);
        }

        if (logger.HasErrors)
        {
            var dialogService = ServiceProvider.Get<IDialogService>();
            _ = dialogService.MessageBoxAsync("Failed to save Object Placement Map asset.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnUndoRedoServiceTransactionFinished(object? sender, Stride.Core.Transactions.TransactionEventArgs e)
    {
        if (_onTransactionFinished_IsSpawnersUpdateRequired)
        {
            foreach (var pendingLayerTreeUpdate in _pendingLayerTreeUpdateList)
            {
                var layerTree = pendingLayerTreeUpdate;
                UpdatePlacementLayerTree(layerTree);
                var editorEntityId = pendingLayerTreeUpdate.EditorEntityId;
                _editorEntityIdToPlacementLayerTree[editorEntityId] = layerTree;
            }

            _onTransactionFinished_IsSpawnersUpdateRequired = false;
            UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);
        }
    }

    private void SubscribeRuntimeMessagingRequests()
    {
        if (_editorToRuntimeMessagingService is null)
        {
            return;
        }

        // IMPORTANT NOTE: When dealing with object placement changes due to property changes from the Stride Editor,
        // eg. entity placement changed, component property changes, etc, you should make terrain transaction
        // changes in OnEditorSceneMemberChanged method instead of here.
        // This is because it needs to be within the existing Stride transaction so undo/redo behaves correctly.

        RegisterRequestHandler<ObjectPlacementMapEditorReadyRequest>(req =>
        {
            var assetEditorsManager = ServiceProvider.Get<IAssetEditorsManager>();
            if (assetEditorsManager is null)
            {
                return;
            }
            var sceneViewModels = Session.AllAssets.Where(x => x is SceneViewModel sceneViewModel).ToList();
            foreach (var sceneVm in sceneViewModels)
            {
                if (!assetEditorsManager.TryGetAssetEditor<SceneEditorViewModel>(sceneVm, out var sceneEditorViewModel))
                {
                    continue;
                }
                var sceneTopRootVm = sceneEditorViewModel.RootPart as SceneRootViewModel;
                if (sceneTopRootVm is null)
                {
                    return;
                }
                var sceneRootVmList = GetAllRootScenes(sceneTopRootVm);
                foreach (var sceneRootVm in sceneRootVmList)
                {
                    if (sceneRootVm?.Id.ObjectId != req.SceneId)
                    {
                        continue;
                    }
                    if (sceneRootVm?.Asset?.Id is AssetId sceneRootAssetId)
                    {
                        var graph = Session.GraphContainer.TryGetGraph(sceneRootAssetId);
                        if (graph is not null)
                        {
                            var editorEntityId = req.EditorEntityId;
                            var graphSub = new SceneAssetPropertyGraphSubscription(graph, editorEntityId, sceneRootVm, OnEditorSceneMemberChanged, OnEditorSceneNodeChanged);
                            _onDestroyDisposables.Add(graphSub);

                            var layerTree = new PlacementLayerTree
                            {
                                ObjectPlacementMapAssetId = req.ObjectPlacementMapAssetId,
                                EditorEntityId = editorEntityId,
                                SceneRootViewModel = sceneRootVm,
                            };
                            UpdatePlacementLayerTree(layerTree);
                            _editorEntityIdToPlacementLayerTree[editorEntityId] = layerTree;
                        }
                        break;
                    }
                }
            }
            TryGetTerrainMapAsset(Asset.TerrainMap, out var terrainMapAsset);   // TODO wait until it's ready?
            var terrainMapTextureSize = terrainMapAsset?.HeightmapTextureSize.ToSize2() ?? Size2.Zero;

            // Ensure intermediate data is loaded
            var dmLayers = Asset.DensityMapLayerDataList;
            if (dmLayers is not null)
            {
                foreach (var layerData in dmLayers)
                {
                    EnsureLayerIntermediateFileDeserialized(layerData, terrainMapTextureSize);
                }
            }
            var spawnerLayers = Asset.SpawnerDataList;
            if (spawnerLayers is not null)
            {
                foreach (var layerData in spawnerLayers)
                {
                    EnsureLayerIntermediateFileDeserialized(layerData, terrainMapTextureSize);
                }
            }
            // Provide the object placement data to the run-time editor tool(s)
            if (_editorToRuntimeMessagingService is not null)
            {
                // Placement data
                var modelAssetUrlList = Asset.ModelAssetUrlList;
                var prefabAssetUrlList = Asset.PrefabAssetUrlList;
                var spawnerDataList = Asset.SpawnerDataList?.ToList() ?? [];
                SendUpdatedObjectPlacementObjectData(modelAssetUrlList, prefabAssetUrlList, spawnerDataList);
            }
        });
        RegisterRequestHandler<ObjectPlacementMapEditorDestroyedRequest>(req =>
        {
            _editorEntityIdToPlacementLayerTree.Remove(req.EditorEntityId);
        });
        RegisterRequestHandler<GetOrCreateObjectPlacementLayerDataRequest>(req =>
        {
            if (!TryGetTerrainMapAsset(Asset.TerrainMap, out var terrainMapAsset))
            {
                return;
            }
            var terrainMapTextureSize = terrainMapAsset.HeightmapTextureSize.ToSize2();
            if (req.LayerDataType == typeof(PainterObjectDensityMapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<PainterObjectDensityMapLayerData>(req.LayerId);
                var layerDensityMapData = GetOrLoadDensityMapData(layerData, ref layerData.ObjectDensityMapData, terrainMapTextureSize);
                RebuildPlacementLayerTrees();

                var updateLayerMsg = new SetPainterObjectDensityMapDataMessage
                {
                    ObjectPlacementMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    ObjectDensityMapData = layerDensityMapData.Clone(),
                    ObjectDensityMapTexturePixelStartPosition = layerData.ObjectDensityMapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(TextureObjectDensityMapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<TextureObjectDensityMapLayerData>(req.LayerId);
                var layerDensityMapData = GetOrLoadDensityMapData(layerData, ref layerData.ObjectDensityMapData, terrainMapTextureSize);
                RebuildPlacementLayerTrees();

                var updateLayerMsg = new SetTextureObjectDensityMapDataMessage
                {
                    ObjectPlacementMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    ObjectDensityMapData = layerDensityMapData.Clone(),
                    ObjectDensityMapTexturePixelStartPosition = layerData.ObjectDensityMapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(ModelInstancingSpawnerData))
            {
                var layerData = Asset.GetOrCreateLayerData<ModelInstancingSpawnerData>(req.LayerId);
                RebuildPlacementLayerTrees();
            }
            else if (req.LayerDataType == typeof(PrefabSpawnerData))
            {
                var layerData = Asset.GetOrCreateLayerData<PrefabSpawnerData>(req.LayerId);
                RebuildPlacementLayerTrees();
            }
            else
            {
                throw new NotImplementedException($"Unhandled LayerDataType: {req.LayerDataType.Name}");
            }
        });

        // DensityMap modification requests
        RegisterRequestHandler<UpdateObjectDensityMapTextureStartPositionRequest>(req =>
        {
            if (Asset.TryGetDensityMapLayerData<ObjectDensityMapLayerDataBase>(req.LayerId, out var layerData))
            {
                layerData.ObjectDensityMapTexturePixelStartPosition = req.ObjectDensityMapTexturePixelStartPosition;
                //layerData.IsSerializeIntermediateFileRequired = true;
                UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);
            }
        });
        RegisterRequestHandler<UpdateTextureObjectDensityMapRequest>(req =>
        {
            if (Asset.TryGetDensityMapLayerData<TextureObjectDensityMapLayerData>(req.LayerId, out var layerData))
            {
                layerData.ObjectDensityMapData = req.ObjectDensityMapData;
                layerData.ObjectDensityMapTexturePixelStartPosition = req.ObjectDensityMapTexturePixelStartPosition;
                layerData.ObjectDensityMapTextureScale = req.ObjectDensityMapTextureScale;
                layerData.IsSerializeIntermediateFileRequired = true;
                UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);
            }
        });
        RegisterRequestHandler<AdjustPainterObjectDensityMapRequest>(req =>
        {
            if (Asset.TryGetDensityMapLayerData<PainterObjectDensityMapLayerData>(req.LayerId, out var layerData))
            {
                using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
                {
                    UndoRedoService.SetName(undoRedoTransaction, "ObjectPlacementMapAsset - AdjustPainterObjectDensityMapRequest");
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                    foreach (var adjustmentRegion in req.ObjectDensityMapAdjustmentRegions)
                    {
                        layerData.ApplyObjectDensityMapAdjustments(adjustmentRegion.AdjustmentObjectDensityMapData, adjustmentRegion.StartPosition, assetTransactionBuilder);
                    }
                    layerData.IsSerializeIntermediateFileRequired = true;
                    UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);

                    assetTransactionBuilder.AddPostExecuteAction(() => UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true));
                    Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(AdjustPainterObjectDensityMapRequest)}: Failed to find layer ID: {req.LayerId}");
            }
        });

        // Spawner modification requests
        RegisterRequestHandler<UpdateObjectPlacementModelInstacingSpawnerDataRequest>(req =>
        {
            if (Asset.TryGetObjectSpawnerData<ModelInstancingSpawnerData>(req.LayerId, out var modelInstancingSpawnerData))
            {
                modelInstancingSpawnerData.ModelType = req.ModelType;
                AssetReplaceableExt.ReplaceList(sourceList: req.ObjectSpawnAssetDefinitionList, destinationList: modelInstancingSpawnerData.SpawnAssetDefinitionList);

                modelInstancingSpawnerData.IsSerializeIntermediateFileRequired = true;
                UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);
            }
            else
            {
                Debug.WriteLine($"{nameof(UpdateObjectPlacementModelInstacingSpawnerDataRequest)}: Failed to find layer ID: {req.LayerId}");
            }
        });
        RegisterRequestHandler<UpdateObjectPlacementPrefabSpawnerDataRequest>(req =>
        {
            if (Asset.TryGetObjectSpawnerData<PrefabSpawnerData>(req.LayerId, out var prefabSpawnerData))
            {
                AssetReplaceableExt.ReplaceList(sourceList: req.ObjectSpawnAssetDefinitionList, destinationList: prefabSpawnerData.SpawnAssetDefinitionList);

                prefabSpawnerData.IsSerializeIntermediateFileRequired = true;
                UpdateObjectPlacementsFromSpawnerLayers(sendSetObjectPlacementObjectDataMessage: true);
            }
            else
            {
                Debug.WriteLine($"{nameof(UpdateObjectPlacementPrefabSpawnerDataRequest)}: Failed to find layer ID: {req.LayerId}");
            }
        });

        void RegisterRequestHandler<TRequest>(Action<TRequest> requestHandler)
            where TRequest : ObjectPlacementMapRequestBase
        {
            var sub = _editorToRuntimeMessagingService.Subscribe<TRequest>(this, requestHandler, additionalConstraints: msg => msg.ObjectPlacementMapAssetId == Asset.Id);
            _onDestroyDisposables.Add(sub);
        }
    }

    private void OnEditorSceneMemberChanged(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetMemberNodeChangeEventArgs e)
    {
        if (UndoRedoService.TransactionInProgress)
        {
            // HACK: In order to integrate our asset change with Stride's undo/redo system seemlessly,
            // we listen to the node changes when UndoRedoService.TransactionInProgress is true
            // then splice in our transaction to ensure undo/redo operations are detected at the same time.
            var memberContainerObj = e.Member.Parent.Retrieve();
            if (memberContainerObj is TransformComponent transformComponent)
            {
                var entity = transformComponent.Entity;
                if (entity.TryGetComponent<IObjectPlacementLayer>(out var objectPlacementLayer))
                {
                    if (e.Member.Name == nameof(TransformComponent.Position)
                        || e.Member.Name == nameof(TransformComponent.Rotation)
                        || e.Member.Name == nameof(TransformComponent.Scale))
                    {
                        // Note that we don't actually modify any data, because this actually occurs via the
                        // request handlers (code in SubscribeMessagingRequests method).
                        var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
                        if (Asset.TryGetLayerData(objectPlacementLayer.LayerId, out var layerData))
                        {
                            layerData.IsSerializeIntermediateFileRequired = true;
                        }
                        Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
                        var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                        var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                        UndoRedoService.PushOperation(trxOp);
                    }
                }
                return;
            }
            //else if (memberContainerObj is IObjectDensityMapLayer densityMapLayer)
            //{
            //    if (e.Member.Name == nameof(IObjectDensityMapLayer.LayerBlendType))
            //    {
            //        var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
            //        if (Asset.TryGetDensityMapLayerData<ObjectDensityMapLayerDataBase>(densityMapLayer.LayerId, out var layerData))
            //        {
            //            layerData.LayerBlendType = (TerrainDensityMapLayerBlendType)e.NewValue;
            //            layerData.IsSerializeIntermediateFileRequired = true;
            //            UpdateDensityMapFromLayers(sendDensityMapUpdateMessage: true);
            //        }
            //        assetTransactionBuilder.AddPostExecuteAction(() => UpdateDensityMapFromLayers(sendDensityMapUpdateMessage: true));
            //        Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
            //        var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
            //        var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
            //        UndoRedoService.PushOperation(trxOp);
            //    }
            //    return;
            //}
            //else if (memberContainerObj is IObjectSpawnerLayer objectSpawnerLayer)
            //{
            //    if (e.Member.Name == nameof(IObjectSpawnerLayer.MaterialName))
            //    {
            //        var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
            //        if (Asset.TryGetMaterialMapLayerData<TerrainMaterialMapLayerDataBase>(objectSpawnerLayer.LayerId, out var layerData))
            //        {
            //            layerData.MaterialName = e.NewValue as string;
            //        }
            //        assetTransactionBuilder.AddPostExecuteAction(() => UpdateSpawnerFromLayers(sendSpawnerUpdateMessage: true));
            //        Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
            //        var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
            //        var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
            //        UndoRedoService.PushOperation(trxOp);
            //    }
            //    return;
            //}
        }
    }

    private void OnEditorSceneNodeChanged(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetItemNodeChangeEventArgs e)
    {
        if (UndoRedoService.TransactionInProgress)
        {
            // HACK: In order to integrate our asset change with Stride's undo/redo system seemlessly,
            // we listen to the node changes when UndoRedoService.TransactionInProgress is true
            // then splice in our transaction to ensure undo/redo operations are detected at the same time.
            // Note that we don't actually modify any data, because this actually occurs via the
            // request handlers (code in SubscribeMessagingRequests method).
            if (e.ChangeType == ContentChangeType.CollectionAdd || e.ChangeType == ContentChangeType.CollectionRemove)
            {
                // This detects layer ordering changes (eg. new layer, delete layer, reorder layer)
                bool isItemRemoved = e.ChangeType == ContentChangeType.CollectionRemove;
                var entity = isItemRemoved
                    ? (e.OldValue as TransformComponent)?.Entity
                    : (e.NewValue as TransformComponent)?.Entity;
                if (entity is not null && entity.TryGetComponent<IObjectPlacementLayer>(out var objectPlacementLayer))
                {
                    // Layer changed (either added or removed - reordered is done with remove + add)
                    var layerId = objectPlacementLayer.LayerId;
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                    bool isDensityMapLayer = objectPlacementLayer is IObjectDensityMapLayer;
                    bool isSpawnerLayer = objectPlacementLayer is IObjectSpawnerLayer;
                    if (!isDensityMapLayer && !isSpawnerLayer)
                    {
                        throw new NotImplementedException($"Unhandled layer type: {objectPlacementLayer.GetType().Name}");
                    }
                    if (isItemRemoved)
                    {
                        bool wasRemoved = Asset.TryRemoveLayerData(layerId);
                        if (!wasRemoved)
                        {
                            throw new InvalidOperationException($"Layer to remove was missing - LayerId: {layerId}");
                        }
                    }
                    else
                    {
                        _ = Asset.GetOrCreateLayerData(layerId, objectPlacementLayer.LayerDataType);
                        if (isDensityMapLayer)
                        {
                            var layerOrdering = GetLayerOrdering<IObjectDensityMapLayer>(editorEntityId, sceneRootVm);
                            Asset.SetDensityMapLayerOrdering(layerOrdering);
                        }
                        else
                        {
                            var layerOrdering = GetLayerOrdering<IObjectSpawnerLayer>(editorEntityId, sceneRootVm);
                            Asset.SetSpawnerOrdering(layerOrdering);
                        }
                    }
                    assetTransactionBuilder.AddPostExecuteAction(() =>
                    {
                        _onTransactionFinished_IsSpawnersUpdateRequired = true;
                        var pendingUpdate = new PlacementLayerTree
                        {
                            ObjectPlacementMapAssetId = Asset.Id,
                            EditorEntityId = editorEntityId,
                            SceneRootViewModel = sceneRootVm
                        };
                        _pendingLayerTreeUpdateList.Add(pendingUpdate);
                    });

                    Asset.LastUserModifiedDateTimeUtc = DateTimeOffset.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);

                    // In the case of reordering layers, Stride raises a remove and add event, but we only need to refresh
                    // at the end of the transaction
                    _onTransactionFinished_IsSpawnersUpdateRequired = true;
                    {
                        var pendingUpdate = new PlacementLayerTree
                        {
                            ObjectPlacementMapAssetId = Asset.Id,
                            EditorEntityId = editorEntityId,
                            SceneRootViewModel = sceneRootVm
                        };
                        _pendingLayerTreeUpdateList.Add(pendingUpdate);
                    }
                    return;
                }
            }
        }
    }

    private void RebuildPlacementLayerTrees()
    {
        foreach (var (_editorEntityId, placementLayerTree) in _editorEntityIdToPlacementLayerTree)
        {
            UpdatePlacementLayerTree(placementLayerTree);
        }
    }

    private void UpdatePlacementLayerTree(PlacementLayerTree layerTree)
    {
        layerTree.RootLayers.Clear();

        Guid editorEntityId = layerTree.EditorEntityId;
        var sceneRootVm = layerTree.SceneRootViewModel;
        if (!TryFindEntityViewModel(editorEntityId, sceneRootVm, out var entityViewModel))
        {
            return;
        }

        //var layerIds = new List<Guid>();
        var layerNodes = layerTree.RootLayers;
        // Must use the entity view model to check the actual hierarchy because the runtime loses the ordering.
        CollectLayers(entityViewModel, layerNodes, parentDensityMapLayerNode: null);

        return;

        static void CollectLayers(EntityViewModel entityViewModel, List<ObjectPlacementLayerTreeNode> layerNodesOutput, ObjectPlacementLayerTreeNode? parentDensityMapLayerNode)
        {
            var assetSideEntity = entityViewModel.AssetSideEntity;
            var nextParentDensityMapLayerNode = parentDensityMapLayerNode;
            for (int i = 0; i < assetSideEntity.Components.Count; i++)
            {
                var comp = assetSideEntity.Components[i];
                if (comp is IObjectDensityMapLayer densityMapLayer)
                {
                    var layerNode = new ObjectPlacementLayerTreeNode
                    {
                        Layer = densityMapLayer,
                        ParentDensityMapLayerNode = parentDensityMapLayerNode,
                    };
                    if (parentDensityMapLayerNode is not null)
                    {
                        parentDensityMapLayerNode.Children.Add(layerNode);
                    }
                    else
                    {
                        layerNodesOutput.Add(layerNode);
                    }
                    nextParentDensityMapLayerNode = layerNode;
                    // Entity should only contain one density map component
                    break;
                }
            }
            var spawnerParentDensityMapLayerNode = nextParentDensityMapLayerNode;   // If an entity contains both a density map & spawner component, we want the spawner to be a child of the current density map component
            for (int i = 0; i < assetSideEntity.Components.Count; i++)
            {
                var comp = assetSideEntity.Components[i];
                if (comp is IObjectSpawnerLayer spawnerLayer)
                {
                    var layerNode = new ObjectPlacementLayerTreeNode
                    {
                        Layer = spawnerLayer,
                        ParentDensityMapLayerNode = spawnerParentDensityMapLayerNode,
                    };
                    if (spawnerParentDensityMapLayerNode is not null)
                    {
                        spawnerParentDensityMapLayerNode.Children.Add(layerNode);
                    }
                    else
                    {
                        layerNodesOutput.Add(layerNode);
                    }
                }
            }
            foreach (var childEntViewModel in entityViewModel.TransformChildren)
            {
                CollectLayers(childEntViewModel, layerNodesOutput, nextParentDensityMapLayerNode);
            }
        }
    }

    private static List<Guid> GetLayerOrdering<TLayer>(Guid editorEntityId, SceneRootViewModel sceneRootVm)
        where TLayer : IObjectPlacementLayer
    {
        if (!TryFindEntityViewModel(editorEntityId, sceneRootVm, out var entityViewModel))
        {
            return [];
        }

        var layerIds = new List<Guid>();
        // Must use the entity view model to check the actual hierarchy because the runtime loses the ordering.
        var entities = entityViewModel.EntityHierarchy.EnumerateChildParts(entityViewModel.AssetSideEntity, isRecursive: true);
        foreach (var assetSideEnt in entities)
        {
            for (int i = 0; i < assetSideEnt.Components.Count; i++)
            {
                var comp = assetSideEnt.Components[i];
                if (comp is TLayer layer)
                {
                    layerIds.Add(layer.LayerId);
                }
            }
        }
        return layerIds;
    }

    private static List<SceneRootViewModel> GetAllRootScenes(SceneRootViewModel sceneRootVm)
    {
        var sceneRootVmListOutput = new List<SceneRootViewModel>();
        GetAllRootScenesIntl(sceneRootVm, sceneRootVmListOutput);
        return sceneRootVmListOutput;

        static void GetAllRootScenesIntl(SceneRootViewModel sceneRootVm, List<SceneRootViewModel> sceneRootVmList)
        {
            sceneRootVmList.Add(sceneRootVm);
            foreach (var childScene in sceneRootVm.ChildScenes)
            {
                GetAllRootScenesIntl(childScene, sceneRootVmList);
            }
        }
    }

    private static bool TryFindEntityViewModel(Guid sceneEntityId, EntityHierarchyItemViewModel entityHierarchy, out EntityViewModel entityViewModel)
    {
        if (entityHierarchy is EntityViewModel evm && evm.AssetSideEntity.Id == sceneEntityId)
        {
            entityViewModel = evm;
            return true;
        }
        // This will also look into child scenes since they're part of the hierarchy
        foreach (var childHierarchy in entityHierarchy.Children)
        {
            if (TryFindEntityViewModel(sceneEntityId, childHierarchy, out entityViewModel))
            {
                return true;
            }
        }

        entityViewModel = null!;
        return false;
    }

    private Array2d<TValue> GetOrLoadDensityMapData<TValue>(ObjectDensityMapLayerDataBase layerData, ref Array2d<TValue>? densityMapData, Size2 terrainMapTextureSize)
    {
        if (densityMapData is not null)
        {
            return densityMapData;
        }

        EnsureLayerIntermediateFileDeserialized(layerData, terrainMapTextureSize);
        densityMapData ??= new Array2d<TValue>(terrainMapTextureSize);
        return densityMapData;
    }

    private void EnsureLayerIntermediateFileDeserialized(ObjectPlacementLayerDataBase layerData, Size2 terrainMapTextureSize)
    {
        if (layerData.IsDeserializeIntermediateFileRequired && Asset.ResourceFolderPath is not null)
        {
            var packageFolderPath = AssetItem.Package.FullPath.GetFullDirectory();
            var resourceFolderFullPath = UPath.Combine(packageFolderPath, Asset.ResourceFolderPath).ToOSPath();
            layerData.DeserializeIntermediateFile(resourceFolderFullPath, Asset, terrainMapTextureSize, _logger);
        }
    }

    private bool UpdateObjectPlacementsFromSpawnerLayers(bool sendSetObjectPlacementObjectDataMessage)
    {
        if (!TryGetTerrainMapAsset(Asset.TerrainMap, out var terrainMapAsset))
        {
            return false;
        }
        var editorEntityIdToLayerTree = _editorEntityIdToPlacementLayerTree.FirstOrDefault();  // There shouldn't be more than one
        var placementLayerTree = editorEntityIdToLayerTree.Value;
        if (placementLayerTree is null || terrainMapAsset.HeightmapData is null)
        {
            return false;
        }

        var terrainMapTextureSize = terrainMapAsset.HeightmapTextureSize.ToSize2();
        LoadLayerDataAndClearObjectPlacementData(placementLayerTree.RootLayers, Asset, terrainMapTextureSize);

        var spawnerLayerNodeList = new List<ObjectPlacementLayerTreeNode>();
        CollectSpawners(placementLayerTree.RootLayers, spawnerLayerNodeList);

        var quadPerChunk = terrainMapAsset.QuadsPerMesh * terrainMapAsset.MeshPerChunk.GetSingleAxisLength();
        var chunkWorldSizeVec2 = terrainMapAsset.MeshQuadSize * (Vector2)quadPerChunk;

        var terrainMapHeightmapData = terrainMapAsset.HeightmapData;
        var terrainMapHeightRange = terrainMapAsset.HeightRange;
        var terrainMapMeshQuadSize = terrainMapAsset.MeshQuadSize;
        var terrainMapQuadCount = terrainMapHeightmapData.Length2d.Subtract(Int2.One);
        var terrainMapWorldSize = terrainMapMeshQuadSize * terrainMapQuadCount.ToVector2();

        bool hasChanged = false;

        // Track pending placements and separate into chunks to speed up collision check
        var chunkIndexToPendingObjects = new Dictionary<TerrainChunkIndex2d, List<PendingObjectPlacement>>();

        var modelAssetUrlList = new List<string>();
        var prefabAssetUrlList = new List<string>();
        var spawnerDataList = new List<ObjectSpawnerDataBase>();

        var posToChunkIndex = 1f / chunkWorldSizeVec2;

        for (int curSpawnerLayerIdx = 0; curSpawnerLayerIdx < spawnerLayerNodeList.Count; curSpawnerLayerIdx++)
        {
            var spawnerLayerNode = spawnerLayerNodeList[curSpawnerLayerIdx];
            if (spawnerLayerNode?.Layer is not IObjectSpawnerLayer spawnerLayer)
            {
                continue;
            }
            if (spawnerLayer.ObjectSpacing == 0)
            {
                continue;
            }

            var spawnerData = (ObjectSpawnerDataBase)Asset.GetOrCreateLayerData(spawnerLayer.LayerId, spawnerLayer.LayerDataType);
            spawnerDataList.Add(spawnerData);

            int[] spawnListIndexToUrlRefListIndex;
            if (spawnerData is ModelInstancingSpawnerData modelInstancingSpawnerData)
            {
                spawnListIndexToUrlRefListIndex = GetOrCreateSpawnListIndexToModelUrlRefListIndex(modelInstancingSpawnerData, modelAssetUrlList);

            }
            else if (spawnerData is PrefabSpawnerData prefabSpawnerData)
            {
                spawnListIndexToUrlRefListIndex = GetOrCreateSpawnListIndexToPrefabUrlRefListIndex(prefabSpawnerData, prefabAssetUrlList);
            }
            else
            {
                Debug.WriteLine($"{nameof(UpdateObjectPlacementsFromSpawnerLayers)}: Unhandled spawner data type: {spawnerData?.GetType().Name}");
                continue;
            }

            var rndSeed = spawnerLayer.SpawnerRandomSeed;
            var random = new Random(rndSeed);

            var objCountVec2 = terrainMapWorldSize / spawnerLayer.ObjectSpacing;
            int objCountX = (int)Math.Floor(objCountVec2.X);
            int objCountY = (int)Math.Floor(objCountVec2.Y);

            float minimumDensityValueThreshold = spawnerLayer.MinimumDensityValueThreshold;

            float rndPosOffsetMinRadius = spawnerLayer.PositionOffsetMinimumRadius;
            float rndPosOffsetMaxRadius = spawnerLayer.PositionOffsetMaximumRadius;
            float rndPosOffsetMinAngleRad = MathUtil.DegreesToRadians(spawnerLayer.RotationYOffsetMinimumAngleDegrees);
            float rndPosOffsetMaxAngleRad = MathUtil.DegreesToRadians(spawnerLayer.RotationYOffsetMaximumAngleDegrees);
            float rndScaleMin = spawnerLayer.ScaleMinimum;
            float rndScaleMax = spawnerLayer.ScaleMaximum;

            float rndSurfaceNormalMinAngleDeg = spawnerLayer.SurfaceNormalMinimumAngleDegrees;
            float rndSurfaceNormalMaxAngleDeg = spawnerLayer.SurfaceNormalMaximumAngleDegrees;
            bool alignWithSurfaceNormal = spawnerLayer.AlignWithSurfaceNormal;

            for (int y = 0; y < objCountY; y++)
            {
                for (int x = 0; x < objCountX; x++)
                {
                    var worldPosXZ = new Vector2(x * spawnerLayer.ObjectSpacing, y * spawnerLayer.ObjectSpacing);

                    // Execute all random values to make a mostly deterministic placement
                    double rndSpawnAssetSelectorValue = random.NextDouble();
                    float rndPosOffsetRadiusRndValue = random.NextSingle();
                    float rndPosOffsetRadius = MathUtil.Lerp(rndPosOffsetMinRadius, rndPosOffsetMaxRadius, rndPosOffsetRadiusRndValue);
                    float rndPosOffsetAngleRndValue = random.NextSingle();
                    float rndPosOffsetAngle = rndPosOffsetAngleRndValue * MathUtil.TwoPi;

                    float rndOrientationYAxisRndValue = random.NextSingle();

                    float rndScaleRndValue = random.NextSingle();

                    var rndPosOffsetRotation = Quaternion.RotationY(rndPosOffsetAngle);
                    var rndPosOffsetVec = rndPosOffsetRotation * Vector3.UnitZ * rndPosOffsetRadius;

                    var mapStartPosition = Vector3.Zero;    // Assume zero
                    var objWorldPosXZ = worldPosXZ + rndPosOffsetVec.XZ();

                    float densityValue = CalculateDensityValue(objWorldPosXZ, terrainMapWorldSize, spawnerLayerNode.ParentDensityMapLayerNode, Asset);
                    if (densityValue == 0)
                    {
                        continue;
                    }
                    if (minimumDensityValueThreshold > densityValue)
                    {
                        continue;
                    }

                    if (!TerrainRaycast.TryGetHeight(
                        objWorldPosXZ,
                        terrainMapHeightmapData, terrainMapHeightRange, terrainMapMeshQuadSize, mapStartPosition,
                        out float heightValue, out Vector3 surfaceNormal))
                    {
                        continue;
                    }

                    // Check surface normal threshold
                    float upVecDotSurfaceNormalValue = Vector3.Dot(Vector3.UnitY, surfaceNormal);
                    float upVecToSurfaceNormalAngleDiffNormalized = (1f - upVecDotSurfaceNormalValue) * 0.5f;   // Change from [1, -1] to [0, 1] range
                    float upVecToSurfaceNormalAngleDiffDegrees = upVecToSurfaceNormalAngleDiffNormalized * 180;
                    if (upVecToSurfaceNormalAngleDiffDegrees < rndSurfaceNormalMinAngleDeg
                        || upVecToSurfaceNormalAngleDiffDegrees > rndSurfaceNormalMaxAngleDeg)
                    {
                        continue;
                    }

                    // Pick random spawn object
                    ObjectSpawnAssetDefinition? selectedAssetDef = null;
                    int selectedAssetDefIndex = -1;

                    double totalSpawnWeightValue = 0;
                    foreach (var spawnAssetDef in spawnerData.SpawnAssetDefinitionList)
                    {
                        totalSpawnWeightValue += spawnAssetDef.SpawnWeightValue;
                    }
                    double spawnValue = rndSpawnAssetSelectorValue * totalSpawnWeightValue;
                    double nextWeightThreshold = 0;
                    for (int i = 0; i < spawnerData.SpawnAssetDefinitionList.Count; i++)
                    {
                        var spawnAssetDef = spawnerData.SpawnAssetDefinitionList[i];
                        nextWeightThreshold += spawnAssetDef.SpawnWeightValue;
                        if (spawnValue < nextWeightThreshold)
                        {
                            selectedAssetDef = spawnAssetDef;
                            selectedAssetDefIndex = i;
                            break;
                        }
                    }
                    if (selectedAssetDef is null)
                    {
                        continue;       // Nothing selected
                    }

                    var nextObjWorldPos = new Vector3(objWorldPosXZ.X, heightValue, objWorldPosXZ.Y);
                    // Check if it has already been occupied
                    bool canPlaceObject = true;
                    var minChunkIndex = MathExt.ToInt2Floor((nextObjWorldPos.XZ() - new Vector2(selectedAssetDef.CollisionRadius)) * posToChunkIndex);
                    var maxChunkIndex = MathExt.ToInt2Floor((nextObjWorldPos.XZ() + new Vector2(selectedAssetDef.CollisionRadius)) * posToChunkIndex);
                    var nextObjSphere = new BoundingSphere(nextObjWorldPos, selectedAssetDef.CollisionRadius);
                    for (int chunkIndexY = minChunkIndex.Y; chunkIndexY <= maxChunkIndex.Y; chunkIndexY++)
                    {
                        for (int chunkIndexX = minChunkIndex.X; chunkIndexX <= maxChunkIndex.X; chunkIndexX++)
                        {
                            var chunkIdx = new TerrainChunkIndex2d(chunkIndexX, chunkIndexY);
                            if (!chunkIndexToPendingObjects.TryGetValue(chunkIdx, out var objectList))
                            {
                                continue;
                            }
                            foreach (var prevObjPlc in objectList)
                            {
                                if (prevObjPlc.IsOccupied(nextObjSphere))
                                {
                                    canPlaceObject = false;
                                    goto ExitIsOccupiedCheck;
                                }
                            }
                        }
                    }
                ExitIsOccupiedCheck:

                    if (canPlaceObject)
                    {
                        float rndScaleValue = MathUtil.Lerp(rndScaleMin, rndScaleMax, rndScaleRndValue);
                        var scaleVec = new Vector3(rndScaleValue);

                        float rndOrientationYAxisValue = MathUtil.Lerp(rndPosOffsetMinAngleRad, rndPosOffsetMaxAngleRad, rndOrientationYAxisRndValue);
                        float orientationValue = (float)MathUtil.Lerp(0, MathUtil.TwoPi, rndOrientationYAxisValue);
                        var orientationQuat = Quaternion.RotationY(orientationValue);
                        if (alignWithSurfaceNormal)
                        {
                            var upVecToSurfaceNormalRotation = Quaternion.BetweenDirections(Vector3.UnitY, surfaceNormal);
                            orientationQuat = orientationQuat * upVecToSurfaceNormalRotation;
                        }

                        Matrix.Transformation(in scaleVec, in orientationQuat, in nextObjWorldPos, out var worldTransform);
                        var surfaceNormalModelSpace = Vector3.TransformNormal(surfaceNormal, Matrix.Invert(worldTransform));
                        surfaceNormalModelSpace.Normalize();

                        int assetUrlListIndex = spawnListIndexToUrlRefListIndex[selectedAssetDefIndex];
                        var objPlcData = new ObjectPlacementSpawnPlacementData
                        {
                            AssetUrlListIndex = assetUrlListIndex,
                            Position = nextObjWorldPos,
                            Orientation = orientationQuat,
                            Scale = scaleVec,
                            SurfaceNormalModelSpace = surfaceNormalModelSpace,
                        };
                        spawnerData.SpawnPlacementDataList.Add(objPlcData);

                        // Place in all occupied chunks (assume box rather than sphere)
                        for (int chunkIndexY = minChunkIndex.Y; chunkIndexY <= maxChunkIndex.Y; chunkIndexY++)
                        {
                            for (int chunkIndexX = minChunkIndex.X; chunkIndexX <= maxChunkIndex.X; chunkIndexX++)
                            {
                                var chunkIdx = new TerrainChunkIndex2d(chunkIndexX, chunkIndexY);
                                if (!chunkIndexToPendingObjects.TryGetValue(chunkIdx, out var pendingObjList))
                                {
                                    pendingObjList = [];
                                    chunkIndexToPendingObjects[chunkIdx] = pendingObjList;
                                }
                                var pendingObj = new PendingObjectPlacement
                                {
                                    LayerId = spawnerLayer.LayerId,
                                    CollisionSphere = nextObjSphere,
                                    ObjectPlacementData = objPlcData
                                };
                                pendingObjList.Add(pendingObj);
                            }
                        }
                        hasChanged = true;
                    }
                }
            }
        }

        if (hasChanged && sendSetObjectPlacementObjectDataMessage && _editorToRuntimeMessagingService is not null)
        {
            AssetReplaceableExt.ReplaceList(sourceList: modelAssetUrlList, destinationList: Asset.ModelAssetUrlList);
            AssetReplaceableExt.ReplaceList(sourceList: prefabAssetUrlList, destinationList: Asset.PrefabAssetUrlList);
            SendUpdatedObjectPlacementObjectData(modelAssetUrlList, prefabAssetUrlList, spawnerDataList);
        }
        //if (sendUpdateLayerTreeMessage)
        //{
        //    SendMaterialLayerIndexListMessage(terrainMapAsset);
        //}
        return hasChanged;

        void LoadLayerDataAndClearObjectPlacementData(
            List<ObjectPlacementLayerTreeNode> placementLayerTreeList, ObjectPlacementMapAsset asset, Size2 terrainMapTextureSize)
        {
            foreach (var layerNode in placementLayerTreeList)
            {
                var layerData = asset.GetOrCreateLayerData(layerNode.Layer.LayerId, layerNode.Layer.LayerDataType);
                EnsureLayerIntermediateFileDeserialized(layerData, terrainMapTextureSize);
                if (layerData is ObjectSpawnerDataBase spawnerData)
                {
                    spawnerData.SpawnPlacementDataList.Clear();
                }

                LoadLayerDataAndClearObjectPlacementData(layerNode.Children, asset, terrainMapTextureSize);
            }
        }

        static void CollectSpawners(
            List<ObjectPlacementLayerTreeNode> placementLayerTreeList,
            List<ObjectPlacementLayerTreeNode> spawnerLayerNodeListOutput)
        {
            foreach (var layerNode in placementLayerTreeList)
            {
                if (layerNode.Layer is IObjectSpawnerLayer spawnerLayer)
                {
                    spawnerLayerNodeListOutput.Add(layerNode);
                }

                CollectSpawners(layerNode.Children, spawnerLayerNodeListOutput);
            }
        }

        static int[] GetOrCreateSpawnListIndexToModelUrlRefListIndex(ModelInstancingSpawnerData modelInstancingSpawnerData, List<string> modelAssetUrlList)
        {
            var spawnAssetDefinitionList = modelInstancingSpawnerData.SpawnAssetDefinitionList;
            var spawnAssetDefListIndexToUrlRefListIndex = new int[spawnAssetDefinitionList.Count];

            for (int i = 0; i < spawnAssetDefinitionList.Count; i++)
            {
                var spawnAssetDef = spawnAssetDefinitionList[i];
                var assetUrl = spawnAssetDef.AssetUrl;
                if (!string.IsNullOrEmpty(assetUrl))
                {
                    if (!modelAssetUrlList.TryFindIndex(assetUrl, StringComparer.OrdinalIgnoreCase, out int assetUrlListIndex))
                    {
                        modelAssetUrlList.Add(assetUrl);
                        assetUrlListIndex = modelAssetUrlList.Count - 1;
                    }
                    spawnAssetDefListIndexToUrlRefListIndex[i] = assetUrlListIndex;
                }
                else
                {
                    spawnAssetDefListIndexToUrlRefListIndex[i] = -1;
                }
            }

            return spawnAssetDefListIndexToUrlRefListIndex;
        }

        static int[] GetOrCreateSpawnListIndexToPrefabUrlRefListIndex(PrefabSpawnerData prefabSpawnerData, List<string> prefabAssetUrlList)
        {
            var spawnAssetDefinitionList = prefabSpawnerData.SpawnAssetDefinitionList;
            var spawnAssetDefListIndexToUrlRefListIndex = new int[spawnAssetDefinitionList.Count];

            for (int i = 0; i < spawnAssetDefinitionList.Count; i++)
            {
                var spawnAssetDef = spawnAssetDefinitionList[i];
                var assetUrl = spawnAssetDef.AssetUrl;
                if (!string.IsNullOrEmpty(assetUrl))
                {
                    if (!prefabAssetUrlList.TryFindIndex(assetUrl, StringComparer.OrdinalIgnoreCase, out int assetUrlListIndex))
                    {
                        prefabAssetUrlList.Add(assetUrl);
                        assetUrlListIndex = prefabAssetUrlList.Count - 1;
                    }
                    spawnAssetDefListIndexToUrlRefListIndex[i] = assetUrlListIndex;
                }
                else
                {
                    spawnAssetDefListIndexToUrlRefListIndex[i] = -1;
                }
            }

            return spawnAssetDefListIndexToUrlRefListIndex;
        }
    }

    private void SendUpdatedObjectPlacementObjectData(
        List<string> modelAssetUrlList, List<string> prefabAssetUrlList,
        List<ObjectSpawnerDataBase> spawnerDataList)
    {
        if (_editorToRuntimeMessagingService is null)
        {
            return;
        }
        var layerIdToModelPlacementDataList = new Dictionary<Guid, List<ModelObjectPlacementsData>>();
        var layerIdToPrefabPlacementDataList = new Dictionary<Guid, List<PrefabObjectPlacementsData>>();

        foreach (var spawnerData in spawnerDataList)
        {
            if (spawnerData is ModelInstancingSpawnerData modelInstancingSpawnerData)
            {
                if (!layerIdToModelPlacementDataList.TryGetValue(spawnerData.LayerId, out var msgObjPlcDataList))
                {
                    msgObjPlcDataList = [];
                    layerIdToModelPlacementDataList[spawnerData.LayerId] = msgObjPlcDataList;
                }

                foreach (var spawnPlacementData in spawnerData.SpawnPlacementDataList)
                {
                    int assetUrlListIndex = spawnPlacementData.AssetUrlListIndex;
                    var modelType = modelInstancingSpawnerData.ModelType;
                    ModelObjectPlacementsData modelObjPlacementsData;
                    if (msgObjPlcDataList.TryFindIndex(x => x.ModelAssetUrlListIndex == assetUrlListIndex && x.ModelType == modelType, out int idx))
                    {
                        modelObjPlacementsData = msgObjPlcDataList[idx];
                    }
                    else
                    {
                        modelObjPlacementsData = new ModelObjectPlacementsData()
                        {
                            ModelAssetUrlListIndex = assetUrlListIndex,
                            ModelType = modelType,
                            Placements = []
                        };
                        msgObjPlcDataList.Add(modelObjPlacementsData);
                    }

                    var objPlcData = spawnPlacementData.ToObjectPlacementData();
                    modelObjPlacementsData.Placements.Add(objPlcData);
                }
            }
            else if (spawnerData is PrefabSpawnerData prefabSpawnerData)
            {
                if (!layerIdToPrefabPlacementDataList.TryGetValue(spawnerData.LayerId, out var msgObjPlcDataList))
                {
                    msgObjPlcDataList = [];
                    layerIdToPrefabPlacementDataList[spawnerData.LayerId] = msgObjPlcDataList;
                }

                foreach (var spawnPlacementData in spawnerData.SpawnPlacementDataList)
                {
                    int assetUrlListIndex = spawnPlacementData.AssetUrlListIndex;
                    PrefabObjectPlacementsData prefabObjPlacementsData;
                    if (msgObjPlcDataList.TryFindIndex(x => x.PrefabAssetUrlListIndex == assetUrlListIndex, out int idx))
                    {
                        prefabObjPlacementsData = msgObjPlcDataList[idx];
                    }
                    else
                    {
                        prefabObjPlacementsData = new PrefabObjectPlacementsData()
                        {
                            PrefabAssetUrlListIndex = assetUrlListIndex,
                            Placements = []
                        };
                        msgObjPlcDataList.Add(prefabObjPlacementsData);
                    }

                    var objPlcData = spawnPlacementData.ToObjectPlacementData();
                    prefabObjPlacementsData.Placements.Add(objPlcData);
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(UpdateObjectPlacementsFromSpawnerLayers)}: Unhandled spawner data type: {spawnerData?.GetType().Name}");
            }
        }
        var setObjectPlacementObjectDataMsg = new SetObjectPlacementObjectDataMessage
        {
            ObjectPlacementMapAssetId = Asset.Id,
            ModelAssetUrlList = modelAssetUrlList,
            PrefabAssetUrlList = prefabAssetUrlList,
            LayerIdToModelPlacementDataList = layerIdToModelPlacementDataList,
            LayerIdToPrefabPlacementDataList = layerIdToPrefabPlacementDataList,
        };
        _editorToRuntimeMessagingService.Send(setObjectPlacementObjectDataMsg);
    }

    private static float CalculateDensityValue(
        Vector2 worldPositionXZ, Vector2 terrainMapWorldSize,
        ObjectPlacementLayerTreeNode? densityMapLayerNode,
        ObjectPlacementMapAsset objectPlacementMapAsset)
    {
        if (densityMapLayerNode is null)
        {
            return 0;
        }

        var blendType = ObjectDensityMapBlendType.Multiply;
        float densityValue = 0;
        if (densityMapLayerNode.Layer is IObjectDensityMapLayer densityMapLayer)
        {
            blendType = densityMapLayer.BlendType;
            if (objectPlacementMapAsset.TryGetDensityMapLayerData<ObjectDensityMapLayerDataBase>(densityMapLayer.LayerId, out var dmLayerData))
            {
                var texturePixelStartPosition = dmLayerData.ObjectDensityMapTexturePixelStartPosition ?? Int2.Zero;
                bool isInverted = densityMapLayer.IsInverted;
                if (dmLayerData is PainterObjectDensityMapLayerData painterLayerData)
                {
                    if (painterLayerData.ObjectDensityMapData is Array2d<Half> objectDensityMapData)
                    {
                        var densityMapUv = worldPositionXZ / terrainMapWorldSize;
                        var pixelToUv = new Vector2(1f / objectDensityMapData.LengthX, 1f / objectDensityMapData.LengthY);
                        var uvOffset = MathExt.ToVec2(texturePixelStartPosition) * pixelToUv;
                        var actualUv = densityMapUv - uvOffset;
                        densityValue = GetLayerDensityValue(objectDensityMapData, actualUv, isInverted);
                    }
                    else
                    {
                        Debug.WriteLine($"{nameof(CalculateDensityValue)}: painterLayerData.ObjectDensityMapData was not loaded. LayerId: {densityMapLayer.LayerId}");
                    }
                }
                else if (dmLayerData is TextureObjectDensityMapLayerData textureLayerData)
                {
                    if (textureLayerData.ObjectDensityMapData is Array2d<Half> objectDensityMapData)
                    {
                        var densityMapUv = worldPositionXZ / objectDensityMapData.Length2d.ToVector2();     // Texture is default 1 unit = 1 px
                        var textureScale = textureLayerData.ObjectDensityMapTextureScale;
                        var pixelToUv = new Vector2(1f / objectDensityMapData.LengthX, 1f / objectDensityMapData.LengthY);
                        var uvOffset = MathExt.ToVec2(texturePixelStartPosition) * pixelToUv;
                        var actualUv = (densityMapUv - uvOffset) / textureScale;
                        densityValue = GetLayerDensityValue(objectDensityMapData, actualUv, isInverted);
                    }
                    else
                    {
                        Debug.WriteLine($"{nameof(CalculateDensityValue)}: textureLayerData.ObjectDensityMapData was not loaded. LayerId: {densityMapLayer.LayerId}");
                    }
                }
                else
                {
                    throw new NotImplementedException($"Unhandled LayerDataType: {dmLayerData.GetType().Name}");
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(CalculateDensityValue)}: DensityMapLayerData was not found. LayerId: {densityMapLayer.LayerId}");
            }
        }

        if (densityMapLayerNode.ParentDensityMapLayerNode is not null)
        {
            float parentDentityValue = CalculateDensityValue(worldPositionXZ, terrainMapWorldSize, densityMapLayerNode.ParentDensityMapLayerNode, objectPlacementMapAsset);
            switch (blendType)
            {
                case ObjectDensityMapBlendType.Multiply:
                    densityValue *= parentDentityValue;
                    break;
                case ObjectDensityMapBlendType.Add:
                    // Despite being called 'Add', this should only get the max value between
                    // the current layer vs the parent layer, in order to retain soft edges
                    // when combining the layers.
                    densityValue = Math.Max(densityValue, parentDentityValue);
                    break;
                case ObjectDensityMapBlendType.Subtract:
                    densityValue = MathUtil.Clamp(parentDentityValue - densityValue, min: 0, max: 1);
                    break;
                default:
                    throw new NotImplementedException($"Unhandled BlendType: {blendType}");
            }
        }
        return densityValue;
    }

    private static float GetLayerDensityValue(Array2d<Half> layerDensityMapData, Vector2 uv, bool isInverted)
    {
        float densityValue = 0;
        if (layerDensityMapData is not null)
        {
            var pixelToUv = new Vector2(1f / layerDensityMapData.LengthX, 1f / layerDensityMapData.LengthY);
            if (uv.X < 0f || uv.X > 1 || uv.Y < 0f || uv.Y > 1)
            {
                // Out of bounds
            }
            else
            {
                var arrayIdxVec2 = uv * layerDensityMapData.Length2d.ToVector2();
                var arrayIdx = MathExt.ToInt2Floor(arrayIdxVec2, out _);
                arrayIdx.X = MathUtil.Clamp(arrayIdx.X, min: 0, max: layerDensityMapData.LengthX - 1);
                arrayIdx.Y = MathUtil.Clamp(arrayIdx.Y, min: 0, max: layerDensityMapData.LengthY - 1);
                densityValue = (float)layerDensityMapData[arrayIdx];
            }
        }

        if (isInverted)
        {
            densityValue = 1 - densityValue;
        }
        return densityValue;
    }

    private bool TryGetTerrainMapAsset(TerrainMap? terrainMapProxyObject, [NotNullWhen(true)] out TerrainMapAsset? terrainMapAsset)
    {
        if (terrainMapProxyObject is null)
        {
            terrainMapAsset = null;
            return false;
        }

        var terrainMapAssetItem = (Session as IAssetFinder)?.FindAssetFromProxyObject(terrainMapProxyObject);
        terrainMapAsset = terrainMapAssetItem?.Asset as TerrainMapAsset;
        return terrainMapAsset is not null;
    }

    private record PlacementLayerTree
    {
        public required AssetId ObjectPlacementMapAssetId { get; init; }
        public required Guid EditorEntityId { get; init; }
        public required SceneRootViewModel SceneRootViewModel { get; init; }
        public List<ObjectPlacementLayerTreeNode> RootLayers { get; } = [];
    }

    private record struct TerrainMapData
    {
        public required Array2d<float> HeightmapData;
        public required Vector2 HeightRange;
        public required Int2 QuadsPerMesh;
        public required Vector2 MeshQuadSize;
        public required TerrainMeshPerChunk MeshPerChunk;
        public required Vector2 ChunkWorldSizeVec2;

        public readonly Size2 MapQuadCount => HeightmapData.Length2d.Subtract(Int2.One);
        public readonly Vector2 MapWorldSize => MeshQuadSize *  MapQuadCount.ToVector2();
    }

    private record struct PendingObjectPlacement
    {
        public required Guid LayerId;
        public required BoundingSphere CollisionSphere;
        public required ObjectPlacementSpawnPlacementData ObjectPlacementData;

        public readonly bool IsOccupied(BoundingSphere sphereOther)
        {
            if (CollisionHelper.SphereIntersectsSphere(in sphereOther, in CollisionSphere))
            {
                return true;
            }
            return false;
        }
    }
}
