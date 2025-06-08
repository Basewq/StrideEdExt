using SceneEditorExtensionExample.Rendering.RenderTextures;
using SceneEditorExtensionExample.StrideEditorExt;
using Stride.Core;
using Stride.Engine;

namespace SceneEditorExtensionExample;

internal class AppModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // HACK: we need to load our custom systems/services in both the standalone Game & the editor's Game,
        // but we don't have access to the editor's Game, so just listen to Game.GameStarted and add it when
        // the event is raised.
        Game.GameStarted += OnGameStarted;
    }

    private static void OnGameStarted(object? sender, System.EventArgs e)
    {
        if (sender is not Game game)
        {
            return;
        }

        var renderTextureJobSystem = new RenderTextureJobSystem(game.Services);
        game.GameSystems.Add(renderTextureJobSystem);

#if GAME_EDITOR
        game.Services.AddService<IStrideEditorService>(new StrideEditorService());
#endif
    }
}
