using Stride.Core;
using Stride.Engine;
using System.Diagnostics;

#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core.Annotations;
using Stride.Editor.EditorGame.Game;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing.Connection;
using StrideEdExt.Painting;
using StrideEdExt.Rendering;
using StrideEdExt.Rendering.PaintRenderer;
using StrideEdExt.Rendering.RenderTextures;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.StrideEditorExt.EditorRuntimeInterfacing;
#endif

namespace StrideEdExt;

internal class AppModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // HACK: we need to load our custom systems/services in both the standalone Game & the editor's Game,
        // but we don't have access to the editor's Game, so just listen to Game.GameStarted and add it when
        // the event is raised.
        Game.GameStarted += OnGameStarted;
        Game.GameDestroyed += OnGameDestroyed;
    }

#if GAME_EDITOR
    private static Dictionary<Game, List<IDisposable>> _gameToDisposables = [];
#endif
    private static void OnGameStarted(object? sender, EventArgs e)
    {
        Debug.WriteLineIf(condition: true, $"OnGameStarted: {sender?.GetType().Name}");
        if (sender is not Game game)
        {
            return;
        }

#if GAME_EDITOR
        var renderTextureJobSystem = new RenderTextureJobSystem(game.Services);
        game.GameSystems.Add(renderTextureJobSystem);
        var painterSystem = new PainterSystem(game.Services);
        game.GameSystems.Add(painterSystem);

        if (sender is SceneEditorGame sceneEditorGame)
        {
            // Note that the editor can run multiple SceneEditorGames since these are the opened scene assets
            var strideEditorService = new StrideEditorService();
            game.Services.AddService<IStrideEditorService>(strideEditorService);
            var viewModelServiceProvider = strideEditorService.ViewModelServiceProvider;
            if (viewModelServiceProvider is null)
            {
                throw new Exception("ViewModelServiceProvider was not set.");
            }
            var inprocessConnectionManager = viewModelServiceProvider.Get<InprocessConnectionManager>();
            var runtimeEndpoint = inprocessConnectionManager.CreateRuntimeEndpoint();
            var runtimeToEditorMessagingService = new RuntimeToEditorMessagingService(game.Services, runtimeEndpoint);
            game.GameSystems.Add(runtimeToEditorMessagingService);

            var disposables = new List<IDisposable>();
            _gameToDisposables[game] = disposables;
            disposables.Add(runtimeEndpoint);

            // HACK: forced to do a late service registration
            sceneEditorGame.Script.AddTask(async () =>
            {
                var paintingPickEditorGameService = new PaintingPickEditorGameService();
                sceneEditorGame.EditorServices.Add(paintingPickEditorGameService);
                await paintingPickEditorGameService.InitializeService(sceneEditorGame);
                paintingPickEditorGameService.UpdateGraphicsCompositor(sceneEditorGame);
            });
        }
#endif
    }

    private static void OnGameDestroyed(object? sender, EventArgs e)
    {
        if (sender is not Game game)
        {
            return;
        }

#if GAME_EDITOR
        if (_gameToDisposables.Remove(game, out var disposables))
        {
            foreach (var disp in disposables)
            {
                disp.Dispose();
            }
        }
#endif
    }

#if GAME_EDITOR
    class PaintingPickEditorGameService : EditorGameServiceBase
    {
        private RenderStage? _paintingRenderStage;
        private PaintingRenderFeature? _paintingRenderFeature;
        private PaintingSceneRenderer? _paintingSceneRenderer;
        private PaintingRenderStageSelector? _paintingStageSelector;

        protected async override Task<bool> Initialize([NotNull] EditorServiceGame game)
        {
            if (IsInitialized)
            {
                return true;
            }
            // This is only done to ensure the shaders are initially loaded/compiled properly
            LoadEffect(game, "PaintingPickOutputEffect");
            LoadEffect(game, "StrokeMapPaintingOutputEffect");

            return true;

            static void LoadEffect(EditorServiceGame game, string effectName)
            {
                game.Script.AddTask(async () =>
                {
                    var loadEffectTask = game.EffectSystem.LoadEffect(effectName);
                    var effect = await loadEffectTask.AwaitResult();
                    if (effect is null)
                    {
                        Debug.WriteLine($"Failed to load effect: {effectName}");
                    }
                });
            }
        }

        public override void UpdateGraphicsCompositor(EditorServiceGame game)
        {
            var graphicsCompositor = game.SceneSystem.GraphicsCompositor;
            _paintingRenderStage ??= new RenderStage("Painting", "Painting");
            AddDistinct(graphicsCompositor.RenderStages, _paintingRenderStage);

            // Meshes
            var meshRenderFeature = graphicsCompositor.RenderFeatures.OfType<MeshRenderFeature>().FirstOrDefault();
            if (meshRenderFeature is not null)
            {
                _paintingRenderFeature ??= new PaintingRenderFeature();
                AddDistinct(meshRenderFeature.RenderFeatures, _paintingRenderFeature);
                _paintingStageSelector ??= new PaintingRenderStageSelector
                {
                    EffectName = "PaintingPickOutputEffect",
                    RenderGroup = RenderGroupMask.All,
                    RenderStage = _paintingRenderStage,
                    Game = game
                };
                AddDistinct(meshRenderFeature.RenderStageSelectors, _paintingStageSelector);
            }

            _paintingSceneRenderer ??= new PaintingSceneRenderer
            {
                PaintingRenderStage = _paintingRenderStage,
            };
            var existingPaintRendererService = game.Services.GetService<IPaintRendererService>();
            if (existingPaintRendererService is null)
            {
                game.Services.AddService<IPaintRendererService>(_paintingSceneRenderer);
            }
            else if (existingPaintRendererService != _paintingSceneRenderer)
            {
                game.Services.RemoveService<IPaintRendererService>(existingPaintRendererService);
                game.Services.AddService<IPaintRendererService>(_paintingSceneRenderer);
            }

            if (graphicsCompositor.Game is EditorTopLevelCompositor editorCompositor)
            {
                AddDistinct(editorCompositor.PreGizmoCompositors, _paintingSceneRenderer);
            }
        }

        private static void AddDistinct<T>(IList<T> list, T item)
        {
            if (list.Contains(item))
            {
                return;
            }
            list.Add(item);
        }
    }
#endif
}
