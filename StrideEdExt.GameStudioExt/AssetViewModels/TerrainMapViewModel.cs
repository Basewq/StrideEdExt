using SceneEditorExtensionExample.StrideAssetExt.Assets;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;

namespace SceneEditorExtensionExample.GameStudioExt.AssetViewModels;

[AssetViewModel<TerrainMapAsset>]
public class TerrainMapViewModel : AssetViewModel<TerrainMapAsset>
{
    public TerrainMapViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }

    protected override void OnSessionSaved()
    {
        var logger = new LoggerResult();

        var packageFolderPath = AssetItem.Package.FullPath.GetFullDirectory();
        Asset.SerializeIntermediateFiles(logger, packageFolderPath);
        if (Asset.HasLayerMetadataListChanged)
        {
            using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
            {
                UndoRedoService.SetName(undoRedoTransaction, "TerrainMapAsset - Update LayerMetadataList");
                var collectionNode = AssetRootNode[TerrainMapAsset.LayerMetadataListName];
                collectionNode.Target.ItemReferences.Refresh(collectionNode, NodeContainer);
                Asset.HasLayerMetadataListChanged = false;
            }
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
}
