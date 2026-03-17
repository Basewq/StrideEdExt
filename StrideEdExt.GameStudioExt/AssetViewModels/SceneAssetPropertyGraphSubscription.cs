using Stride.Assets.Presentation.AssetEditors.SceneEditor.ViewModels;
using Stride.Core.Assets.Quantum;

namespace StrideEdExt.GameStudioExt.AssetViewModels;

internal class SceneAssetPropertyGraphSubscription : IDisposable
{
    internal delegate void AssetMemberChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetMemberNodeChangeEventArgs e);
    internal delegate void AssetNodeItemChangedEventHandler(Guid editorEntityId, SceneRootViewModel sceneRootVm, AssetItemNodeChangeEventArgs e);

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
