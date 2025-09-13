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
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Engine;
using StrideEdExt.GameStudioExt.Assets.Transaction;
using StrideEdExt.SharedData;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;
using StrideEdExt.SharedData.Terrain3d.Layers;
using StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.Heightmaps;
using StrideEdExt.StrideAssetExt.Assets.Terrain3d.Layers.MaterialMaps;
using StrideEdExt.StrideAssetExt.Assets.Transaction;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Half = System.Half;

namespace StrideEdExt.GameStudioExt.AssetViewModels;

[AssetViewModel<TerrainMapAsset>]
public class TerrainMapAssetViewModel : AssetViewModel<TerrainMapAsset>
{
    delegate void AssetMemberChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetMemberNodeChangeEventArgs e);
    delegate void AssetNodeItemChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetItemNodeChangeEventArgs e);

    private readonly Logger _logger;

    private IEditorToRuntimeMessagingService? _editorToRuntimeMessagingService;
    private List<IDisposable> _onDestroyDisposables = [];

    private bool _onTransactionFinished_IsHeightmapLayersUpdateRequired;
    private bool _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired;

    public TerrainMapAssetViewModel(AssetViewModelConstructionParameters parameters)
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
            SubscribeMessagingRequests();
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
            if (memberName == nameof(TerrainMapAsset.HeightmapTextureSize))
            {
                var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                var heightmapTextureSize = Asset.HeightmapTextureSize.ToSize2();
                //Asset.ResizeMap(Asset.MapSize, assetTransactionBuilder);
                if (Asset.HeightmapLayerDataList?.Count > 0)
                {
                    foreach (var layerData in Asset.HeightmapLayerDataList)
                    {
                        layerData.UpdateForTerrainMapResized(heightmapTextureSize, assetTransactionBuilder);
                        layerData.IsSerializeIntermediateFileRequired = true;
                    }
                    assetTransactionBuilder.AddPostExecuteAction(() =>
                    {
                        _onTransactionFinished_IsHeightmapLayersUpdateRequired = true;
                    });
                    _onTransactionFinished_IsHeightmapLayersUpdateRequired = true;
                }
                if (Asset.MaterialWeightMapLayerDataList?.Count > 0)
                {
                    foreach (var layerData in Asset.MaterialWeightMapLayerDataList)
                    {
                        layerData.UpdateForTerrainMapResized(heightmapTextureSize, assetTransactionBuilder);
                        layerData.IsSerializeIntermediateFileRequired = true;
                    }
                    assetTransactionBuilder.AddPostExecuteAction(() =>
                    {
                        _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired = true;
                    });
                    _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired = true;
                }

                Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                UndoRedoService.PushOperation(trxOp);
            }
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
            UndoRedoService.SetName(undoRedoTransaction, "TerrainMapAsset - Serialized Layer Data");
            var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

            Asset.SerializeIntermediateFiles(logger, packageFolderPath);

            var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
            var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
            UndoRedoService.PushOperation(trxOp);
        }
        if (Asset.HasTerrainMapLayerSerializedIntermediateFiles)
        {
            Asset.HasTerrainMapLayerSerializedIntermediateFiles = false;
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
            _ = dialogService.MessageBoxAsync("Failed to save Terrain Map asset.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnUndoRedoServiceTransactionFinished(object? sender, Stride.Core.Transactions.TransactionEventArgs e)
    {
        if (_onTransactionFinished_IsHeightmapLayersUpdateRequired)
        {
            _onTransactionFinished_IsHeightmapLayersUpdateRequired = false;
            UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true);
        }
        if (_onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired)
        {
            _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired = false;
            UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true);
        }
    }

    private void SubscribeMessagingRequests()
    {
        if (_editorToRuntimeMessagingService is null)
        {
            return;
        }

        // IMPORTANT NOTE: When dealing with terrain changes due to property changes from the Stride Editor,
        // eg. entity placement changed, component property changes, etc, you should make terrain transaction
        // changes in OnEditorSceneMemberChanged method instead of here.
        // This is because it needs to be within the existing Stride transaction so undo/redo behaves correctly.

        RegisterRequestHandler<TerrainMapEditorReadyRequest>(req =>
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
                var sceneRootVmList = new List<SceneRootViewModel>();
                GetAllRootScenes(sceneTopRootVm, sceneRootVmList);
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
                        };
                    }
                }
            }
            // Ensure intermediate data is loaded
            var hmLayers = Asset.HeightmapLayerDataList;
            if (hmLayers is not null)
            {
                foreach (var layerData in hmLayers)
                {
                    EnsureLayerIntermediateFileDeserialized(layerData);
                }
            }
            var mwmLayers = Asset.MaterialWeightMapLayerDataList;
            if (mwmLayers is not null)
            {
                foreach (var layerData in mwmLayers)
                {
                    EnsureLayerIntermediateFileDeserialized(layerData);
                }
            }
            // Provide the material layer ordering to the run-time editor tool(s)
            if (_editorToRuntimeMessagingService is not null)
            {
                if (Asset.HeightmapData is not null)
                {
                    var updateHeightmapMsg = new SetTerrainMapHeightmapDataMessage
                    {
                        TerrainMapAssetId = Asset.Id,
                        HeightmapTextureSize = Asset.HeightmapTextureSize.ToSize2(),
                        HeightmapData = Asset.HeightmapData.Clone()
                    };
                    _editorToRuntimeMessagingService.Send(updateHeightmapMsg);
                }
                UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true, sendMaterialLayerIndexListMessage: true);
            }
        });
        RegisterRequestHandler<GetOrCreateLayerDataRequest>(req =>
        {
            if (req.LayerDataType == typeof(ModelHeightmapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<ModelHeightmapLayerData>(req.LayerId);
                var layerHeightmapData = GetOrLoadHeightmapData(layerData, ref layerData.HeightmapData);

                var updateLayerMsg = new SetModelHeightmapDataMessage
                {
                    TerrainMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    LayerHeightmapData = layerHeightmapData.Clone(),
                    HeightmapTexturePixelStartPosition = layerData.HeightmapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(PainterHeightmapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<PainterHeightmapLayerData>(req.LayerId);
                var layerHeightmapData = GetOrLoadHeightmapData(layerData, ref layerData.HeightmapData);

                var updateLayerMsg = new SetPainterHeightmapDataMessage
                {
                    TerrainMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    LayerHeightmapData = layerHeightmapData.Clone(),
                    HeightmapTexturePixelStartPosition = layerData.HeightmapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(TextureHeightmapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<TextureHeightmapLayerData>(req.LayerId);
                var layerHeightmapData = GetOrLoadHeightmapData(layerData, ref layerData.HeightmapData);

                var updateLayerMsg = new SetTextureHeightmapDataMessage
                {
                    TerrainMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    LayerHeightmapData = layerHeightmapData.Clone(),
                    HeightmapTexturePixelStartPosition = layerData.HeightmapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(PainterMaterialWeightMapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<PainterMaterialWeightMapLayerData>(req.LayerId);
                var layerMaterialWeightMapData = GetOrLoadMaterialWeightMapData(layerData, ref layerData.MaterialWeightMapData);

                var updateLayerMsg = new SetPainterMaterialWeightMapDataMessage
                {
                    TerrainMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    MaterialWeightMapData = layerMaterialWeightMapData.Clone(),
                    MaterialWeightMapTexturePixelStartPosition = layerData.MaterialWeightMapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);
            }
            else if (req.LayerDataType == typeof(TextureMaterialWeightMapLayerData))
            {
                var layerData = Asset.GetOrCreateLayerData<TextureMaterialWeightMapLayerData>(req.LayerId);
                var layerMaterialWeightMapData = GetOrLoadMaterialWeightMapData(layerData, ref layerData.MaterialWeightMapData);

                var updateLayerMsg = new SetTextureMaterialWeightMapDataMessage
                {
                    TerrainMapAssetId = Asset.Id,
                    LayerId = layerData.LayerId,
                    MaterialWeightMapData = layerMaterialWeightMapData.Clone(),
                    MaterialWeightMapTexturePixelStartPosition = layerData.MaterialWeightMapTexturePixelStartPosition
                };
                _editorToRuntimeMessagingService.Send(updateLayerMsg);

            }
            else
            {
                throw new NotImplementedException($"Unhandled LayerDataType: {req.LayerDataType.Name}");
            }
        });

        // Heightmap modification requests
        RegisterRequestHandler<UpdateHeightmapTextureStartPositionRequest>(req =>
        {
            if (Asset.TryGetHeightmapLayerData<TerrainHeightmapLayerDataBase>(req.LayerId, out var layerData))
            {
                layerData.HeightmapTexturePixelStartPosition = req.HeightmapTexturePixelStartPosition;
                //layerData.IsSerializeIntermediateFileRequired = true;
                UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true);
            }
        });
        RegisterRequestHandler<UpdateModelHeightmapRequest>(req =>
        {
            if (Asset.TryGetHeightmapLayerData<ModelHeightmapLayerData>(req.LayerId, out var layerData))
            {
                layerData.HeightmapData = req.HeightmapData;
                layerData.HeightmapTexturePixelStartPosition = req.HeightmapTexturePixelStartPosition;
                //layerData.IsSerializeIntermediateFileRequired = true;
                UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true);
            }
        });
        RegisterRequestHandler<AdjustPainterHeightmapRequest>(req =>
        {
            if (Asset.HeightmapData is null)
            {
                Debug.WriteLine($"{nameof(AdjustPainterHeightmapRequest)}: Asset.HeightmapData was not loaded.");
                return;
            }
            if (Asset.TryGetHeightmapLayerData<PainterHeightmapLayerData>(req.LayerId, out var layerData))
            {
                using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
                {
                    UndoRedoService.SetName(undoRedoTransaction, "TerrainMapAsset - AdjustPainterHeightmapRequest");
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                    foreach (var adjustmentRegion in req.HeightmapAdjustmentRegions)
                    {
                        layerData.ApplyHeightmapAdjustments(adjustmentRegion.AdjustmentHeightmapData, adjustmentRegion.StartPosition, Asset.HeightmapData, assetTransactionBuilder);
                    }
                    layerData.IsSerializeIntermediateFileRequired = true;
                    UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true);

                    assetTransactionBuilder.AddPostExecuteAction(() => UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true));
                    Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(AdjustPainterHeightmapRequest)}: Failed to find layer ID: {req.LayerId}");
            }
        });

        // Material weight map modification requests
        RegisterRequestHandler<UpdateMaterialWeightMapTextureStartPositionRequest>(req =>
        {
            if (Asset.TryGetMaterialMapLayerData<TerrainMaterialMapLayerDataBase>(req.LayerId, out var layerData))
            {
                layerData.MaterialWeightMapTexturePixelStartPosition = req.MaterialWeightMapTexturePixelStartPosition;
                //layerData.IsSerializeIntermediateFileRequired = true;
                UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true);
            }
        });
        RegisterRequestHandler<AdjustPainterMaterialWeightMapRequest>(req =>
        {
            if (Asset.TryGetMaterialMapLayerData<PainterMaterialWeightMapLayerData>(req.LayerId, out var layerData))
            {
                using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
                {
                    UndoRedoService.SetName(undoRedoTransaction, "TerrainMapAsset - AdjustPainterMaterialWeightMapRequest");
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                    foreach (var adjustmentRegion in req.WeightMapAdjustmentRegions)
                    {
                        layerData.ApplyWeightMapAdjustments(adjustmentRegion.AdjustmentWeightMapData, adjustmentRegion.StartPosition, assetTransactionBuilder);
                    }
                    layerData.IsSerializeIntermediateFileRequired = true;
                    UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true);
                    SendUpdateMaterialLayerWeightMapMessage(layerData);

                    assetTransactionBuilder.AddPostExecuteAction(() =>
                    {
                        UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true);
                        SendUpdateMaterialLayerWeightMapMessage(layerData);
                    });
                    Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);
                }
            }
            else
            {
                Debug.WriteLine($"{nameof(AdjustPainterMaterialWeightMapRequest)}: Failed to find layer ID: {req.LayerId}");
            }
        });

        void RegisterRequestHandler<TRequest>(Action<TRequest> requestHandler)
            where TRequest : TerrainMapRequestBase
        {
            var sub = _editorToRuntimeMessagingService.Subscribe<TRequest>(this, requestHandler, additionalConstraints: msg => msg.TerrainMapAssetId == Asset.Id);
            _onDestroyDisposables.Add(sub);
        }
    }

    private void SendUpdateMaterialLayerWeightMapMessage(PainterMaterialWeightMapLayerData layerData)
    {
        if (_editorToRuntimeMessagingService is null)
        {
            return;
        }

        var layerMaterialWeightMapData = GetOrLoadMaterialWeightMapData(layerData, ref layerData.MaterialWeightMapData);

        var updateLayerMsg = new SetPainterMaterialWeightMapDataMessage
        {
            TerrainMapAssetId = Asset.Id,
            LayerId = layerData.LayerId,
            MaterialWeightMapData = layerMaterialWeightMapData.Clone(),
            MaterialWeightMapTexturePixelStartPosition = layerData.MaterialWeightMapTexturePixelStartPosition
        };
        _editorToRuntimeMessagingService.Send(updateLayerMsg);
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
                if (entity.TryGetComponent<ITerrainMapLayer>(out var terrainMapLayer))
                {
                    if (e.Member.Name == nameof(TransformComponent.Position)
                        || e.Member.Name == nameof(TransformComponent.Rotation)
                        || e.Member.Name == nameof(TransformComponent.Scale))
                    {
                        // Note that we don't actually modify any data, because this actually occurs via the
                        // request handlers (code in SubscribeMessagingRequests method).
                        var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
                        if (Asset.TryGetLayerData(terrainMapLayer.LayerId, out var layerData))
                        {
                            layerData.IsSerializeIntermediateFileRequired = true;
                        }
                        Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                        var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                        var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                        UndoRedoService.PushOperation(trxOp);
                    }
                }
                return;
            }
            else if (memberContainerObj is ITerrainMapHeightmapLayer heightmapLayer)
            {
                if (e.Member.Name == nameof(ITerrainMapHeightmapLayer.LayerBlendType))
                {
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
                    if (Asset.TryGetHeightmapLayerData<TerrainHeightmapLayerDataBase>(heightmapLayer.LayerId, out var layerData))
                    {
                        layerData.LayerBlendType = (TerrainHeightmapLayerBlendType)e.NewValue;
                        layerData.IsSerializeIntermediateFileRequired = true;
                        UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true);
                    }
                    assetTransactionBuilder.AddPostExecuteAction(() => UpdateHeightmapFromLayers(sendHeightmapUpdateMessage: true));
                    Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);
                }
                return;
            }
            else if (memberContainerObj is ITerrainMapMaterialWeightMapLayer materialWeightMapLayer)
            {
                if (e.Member.Name == nameof(ITerrainMapMaterialWeightMapLayer.MaterialName))
                {
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);
                    if (Asset.TryGetMaterialMapLayerData<TerrainMaterialMapLayerDataBase>(materialWeightMapLayer.LayerId, out var layerData))
                    {
                        layerData.MaterialName = e.NewValue as string;
                    }
                    assetTransactionBuilder.AddPostExecuteAction(() => UpdateMaterialIndexMapFromLayers(sendMaterialMapUpdateMessage: true));
                    Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);
                }
                return;
            }
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
                if (entity is not null && entity.TryGetComponent<ITerrainMapLayer>(out var terrainMapLayer))
                {
                    // Layer changed (either added or removed - reordered is done with remove + add)
                    var layerId = terrainMapLayer.LayerId;
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(Asset);

                    bool isHeightmapLayer = terrainMapLayer is ITerrainMapHeightmapLayer;
                    bool isMaterialWeightMapLayer = terrainMapLayer is ITerrainMapMaterialWeightMapLayer;
                    if (!isHeightmapLayer && !isMaterialWeightMapLayer)
                    {
                        throw new NotSupportedException($"Unhandled layer type: {terrainMapLayer.GetType().Name}");
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
                        _ = Asset.GetOrCreateLayerData(layerId, terrainMapLayer.LayerDataType);
                        if (isHeightmapLayer)
                        {
                            var layerOrdering = GetLayerOrdering<ITerrainMapHeightmapLayer>(editorEntityId, sceneRootVm);
                            Asset.SetHeightmapLayerOrdering(layerOrdering);
                        }
                        else
                        {
                            var layerOrdering = GetLayerOrdering<ITerrainMapMaterialWeightMapLayer>(editorEntityId, sceneRootVm);
                            Asset.SetMaterialWeightMapLayerOrdering(layerOrdering);
                        }
                    }
                    if (isHeightmapLayer)
                    {
                        assetTransactionBuilder.AddPostExecuteAction(() =>
                        {
                            _onTransactionFinished_IsHeightmapLayersUpdateRequired = true;
                        });
                    }
                    else
                    {
                        assetTransactionBuilder.AddPostExecuteAction(() =>
                        {
                            _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired = true;
                        });
                    }

                    Asset.LastUserModifiedDateTimeUtc = DateTime.UtcNow;
                    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
                    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
                    UndoRedoService.PushOperation(trxOp);

                    // In the case of reordering layers, Stride raises a remove and add event, but we only need to refresh
                    // at the end of the transaction
                    if (isHeightmapLayer)
                    {
                        _onTransactionFinished_IsHeightmapLayersUpdateRequired = true;
                    }
                    else
                    {
                        _onTransactionFinished_IsMaterialIndexMapLayersUpdateRequired = true;
                    }

                    return;
                }
            }
        }
    }

    private List<Guid> GetLayerOrdering<TLayer>(Guid editorEntityId, SceneRootViewModel sceneRootVm)
        where TLayer : ITerrainMapLayer
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

    private static void GetAllRootScenes(SceneRootViewModel sceneRootVm, List<SceneRootViewModel> sceneRootVmListOutput)
    {
        sceneRootVmListOutput.Add(sceneRootVm);
        foreach (var childScene in sceneRootVm.ChildScenes)
        {
            GetAllRootScenes(childScene, sceneRootVmListOutput);
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

    private Array2d<TValue> GetOrLoadHeightmapData<TValue>(TerrainHeightmapLayerDataBase layerData, ref Array2d<TValue>? heightmapData)
    {
        if (heightmapData is not null)
        {
            return heightmapData;
        }

        EnsureLayerIntermediateFileDeserialized(layerData);
        heightmapData ??= new Array2d<TValue>(Asset.HeightmapTextureSize);
        return heightmapData;
    }

    private Array2d<TValue> GetOrLoadMaterialWeightMapData<TValue>(TerrainMaterialMapLayerDataBase layerData, ref Array2d<TValue>? materialWeightMapData)
    {
        if (materialWeightMapData is not null)
        {
            return materialWeightMapData;
        }

        EnsureLayerIntermediateFileDeserialized(layerData);
        materialWeightMapData ??= new Array2d<TValue>(Asset.HeightmapTextureSize);
        return materialWeightMapData;
    }

    private void EnsureLayerIntermediateFileDeserialized(TerrainMapLayerDataBase layerData)
    {
        if (layerData.IsDeserializeIntermediateFileRequired && Asset.ResourceFolderPath is not null)
        {
            var packageFolderPath = AssetItem.Package.FullPath.GetFullDirectory();
            var resourceFolderFullPath = UPath.Combine(packageFolderPath, Asset.ResourceFolderPath).ToOSPath();
            layerData.DeserializeIntermediateFile(resourceFolderFullPath, Asset, _logger);
        }
    }

    private bool UpdateHeightmapFromLayers(bool sendHeightmapUpdateMessage, bool sendMaterialLayerIndexListMessage = false)
    {
        // New data
        var heightmapData = new Array2d<float>(Asset.HeightmapTextureSize);
        var heightRange = Asset.HeightRange;
        var layers = Asset.HeightmapLayerDataList;
        if (layers is null)
        {
            return false;
        }

        // Done in reverse like paint software (ie. layers are built from bottom then upwards)
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var heightmapLayerData = layers[i];
            EnsureLayerIntermediateFileDeserialized(heightmapLayerData);
            heightmapLayerData.ApplyHeightmapModifications(heightmapData, heightRange);
        }

        Asset.HeightmapData = heightmapData;
        bool hasChanged = layers.Count > 0;
        if (hasChanged && sendHeightmapUpdateMessage && _editorToRuntimeMessagingService is not null)
        {
            var updateHeightmapMsg = new SetTerrainMapHeightmapDataMessage
            {
                TerrainMapAssetId = Asset.Id,
                HeightmapTextureSize = Asset.HeightmapTextureSize.ToSize2(),
                HeightmapData = heightmapData.Clone()
            };
            Debug.WriteLineIf(condition: true && heightmapData.LengthX > 0 && heightmapData.LengthY > 0, $"UpdateHeightmapFromLayers - SetTerrainMapHeightmapDataMessage.HeightmapData: {heightmapData[0, 0]}");
            _editorToRuntimeMessagingService.Send(updateHeightmapMsg);
        }
        if (sendMaterialLayerIndexListMessage && TryGetMaterialAsset(Asset.TerrainMaterial, out var terrainMaterialAsset))
        {
            SendMaterialLayerIndexListMessage(terrainMaterialAsset);
        }
        return hasChanged;
    }

    private bool UpdateMaterialIndexMapFromLayers(bool sendMaterialMapUpdateMessage, bool sendMaterialLayerIndexListMessage = false)
    {
        if (!TryGetMaterialAsset(Asset.TerrainMaterial, out var terrainMaterialAsset))
        {
            return false;
        }
        var materialWeightMapData = new Array2d<Half>(Asset.HeightmapTextureSize);
        var materialIndexMapData = new Array2d<byte>(Asset.HeightmapTextureSize);
        var layers = Asset.MaterialWeightMapLayerDataList;
        if (layers is null)
        {
            return false;
        }
        var materialLayers = terrainMaterialAsset.MaterialLayers;

        // Done in reverse like paint software (ie. layers are built from bottom then upwards)
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var materialWeightMapLayerData = layers[i];
            EnsureLayerIntermediateFileDeserialized(materialWeightMapLayerData);
            materialWeightMapLayerData.ApplyLayerMaterialMapModifications(materialWeightMapData, materialIndexMapData, materialLayers);
        }

        Asset.MaterialIndexMapData = materialIndexMapData;
        Asset.MaterialWeightMapData = materialWeightMapData;
        bool hasChanged = layers.Count > 0;
        if (hasChanged && sendMaterialMapUpdateMessage && _editorToRuntimeMessagingService is not null)
        {
            var updateMaterialIndexMapMsg = new SetTerrainMapMaterialIndexMapDataMessage
            {
                TerrainMapAssetId = Asset.Id,
                HeightmapTextureSize = Asset.HeightmapTextureSize.ToSize2(),
                MaterialIndexMapData = materialIndexMapData.Clone(),
                MaterialWeightMapData = materialWeightMapData.Clone()
            };
            _editorToRuntimeMessagingService.Send(updateMaterialIndexMapMsg);
        }
        if (sendMaterialLayerIndexListMessage)
        {
            SendMaterialLayerIndexListMessage(terrainMaterialAsset);
        }
        return layers.Count > 0;
    }

    private void SendMaterialLayerIndexListMessage(TerrainMaterialAsset terrainMaterialAsset)
    {
        if (_editorToRuntimeMessagingService is not null)
        {
            var materialLayers = new List<SetTerrainMapMaterialLayerData>();
            var matLayerDefinitions = terrainMaterialAsset.MaterialLayers;
            for (int i = 0; i < matLayerDefinitions.Count; i++)
            {
                var matLayerDef = matLayerDefinitions[i];
                if (string.IsNullOrWhiteSpace(matLayerDef.MaterialName))
                {
                    continue;
                }
                var setMatLayerData = new SetTerrainMapMaterialLayerData
                {
                    MaterialName = matLayerDef.MaterialName,
                    MaterialIndex = (byte)i,
                };
                materialLayers.Add(setMatLayerData);
            }
            var setMaterialLayerIndexListMsg = new SetTerrainMapMaterialLayerIndexListMessage
            {
                TerrainMapAssetId = Asset.Id,
                MaterialLayers = materialLayers
            };
            _editorToRuntimeMessagingService.Send(setMaterialLayerIndexListMsg);
        }
    }

    private bool TryGetMaterialAsset(TerrainMaterial? terrainMaterial, [NotNullWhen(true)] out TerrainMaterialAsset? terrainMaterialAsset)
    {
        if (terrainMaterial is null)
        {
            terrainMaterialAsset = null;
            return false;
        }

        var terrainMaterialAssetItem = (Session as IAssetFinder)?.FindAssetFromProxyObject(terrainMaterial);
        terrainMaterialAsset = terrainMaterialAssetItem?.Asset as TerrainMaterialAsset;
        return terrainMaterialAsset is not null;
    }

    private class SceneAssetPropertyGraphSubscription : IDisposable
    {
        private AssetPropertyGraph _assetPropertyGraph;
        private Guid _editorEntityId;
        private SceneRootViewModel _sceneRootVm;
        private AssetMemberChangedEventHandler _memberChangedEventHandler;
        private AssetNodeItemChangedEventHandler _nodeChangedEventHandler;

        public SceneAssetPropertyGraphSubscription(
            AssetPropertyGraph assetPropertyGraph,
            Guid editorEntityId,
            SceneRootViewModel sceneRootVm,
            AssetMemberChangedEventHandler memberChangedEventHandler,
            AssetNodeItemChangedEventHandler nodeChangedEventHandler)
        {
            _assetPropertyGraph = assetPropertyGraph;
            _editorEntityId = editorEntityId;
            _sceneRootVm = sceneRootVm;
            _memberChangedEventHandler = memberChangedEventHandler;
            _assetPropertyGraph.Changed += OnAssetPropertyGraphChanged;
            _nodeChangedEventHandler = nodeChangedEventHandler;
            _assetPropertyGraph.ItemChanged += OnAssetPropertyGraphItemChanged;
        }

        public void Dispose()
        {
            _assetPropertyGraph.Changed -= OnAssetPropertyGraphChanged;
            _assetPropertyGraph.ItemChanged -= OnAssetPropertyGraphItemChanged;
        }

        private void OnAssetPropertyGraphChanged(object? sender, AssetMemberNodeChangeEventArgs e)
        {
            _memberChangedEventHandler(_editorEntityId, _sceneRootVm, e);
        }

        private void OnAssetPropertyGraphItemChanged(object? sender, AssetItemNodeChangeEventArgs e)
        {
            _nodeChangedEventHandler(_editorEntityId, _sceneRootVm, e);
        }
    }
}
