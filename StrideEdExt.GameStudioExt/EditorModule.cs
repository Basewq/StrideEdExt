using Stride.Core;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Services;
using Stride.Core.Reflection;
using Stride.Editor;
using StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing.Connection;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing.EditorToRuntimeMessages;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing.RuntimeToEditorRequests;

namespace StrideEdExt.GameStudioExt;

/**
 * This class is used to make CompilerApp & Game Studio aware of this assembly to make it recognize our custom asset(s).
 */
internal class EditorModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AssemblyRegistry.Register(typeof(EditorModule).Assembly, AssemblyCommonCategories.Assets);

        // Custom plugin needed to be registered so our custom AssetViewModel can be detected (ie. TerrainMapViewModel)
        AssetsPlugin.RegisterPlugin(typeof(GameAssetsEditorPlugin));
    }
}

internal class GameAssetsEditorPlugin : StrideAssetsPlugin, IRuntimeToEditorRequestHandler
{
    private EditorToRuntimeMessagingService? _editorToRuntimeMessagingService;

    private TaskCompletionSource _servicesRegistrationCompletedTcs = new();
    public Task ServicesRegistrationCompleted => _servicesRegistrationCompletedTcs.Task;

    protected override void Initialize(ILogger logger)
    {
        // Unused
    }

    /// <inheritdoc />
    public override void InitializeSession([Stride.Core.Annotations.NotNull] SessionViewModel session)
    {
        // Editor runs the game in the same process
        var inprocessConnectionManager = new InprocessConnectionManager();
        session.ServiceProvider.RegisterService(inprocessConnectionManager);
        var editorEndpoint = inprocessConnectionManager.GetEditorEndpoint();
        var dispatcher = session.ServiceProvider.Get<IDispatcherService>();
        _editorToRuntimeMessagingService = new EditorToRuntimeMessagingService(editorEndpoint, dispatcher);
        session.ServiceProvider.RegisterService(_editorToRuntimeMessagingService);
        _editorToRuntimeMessagingService.RegisterHandler(this);

        _servicesRegistrationCompletedTcs.SetResult();
    }

    /// <inheritdoc />
    public override void RegisterAssetPreviewViewTypes(IDictionary<Type, Type> assetPreviewViewTypes)
    {
        // Unused
    }

    public void ProcessRequest(IRuntimeToEditorRequest request)
    {
        if (_editorToRuntimeMessagingService is null)
        {
            throw new InvalidOperationException("EditorToRuntimeMessagingService was null.");
        }

        if (request is TestRuntimeToEditorRequest testReq)
        {
            System.Diagnostics.Debug.WriteLine($"Message was: {testReq.Message}");
            var msg = new TestEditorToRuntimeMessage
            {
                Message = "Editor has received request."
            };
            _editorToRuntimeMessagingService.Send(msg);
        }
    }
}
