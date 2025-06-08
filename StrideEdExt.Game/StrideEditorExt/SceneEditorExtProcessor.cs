using Stride.Engine;

#if GAME_EDITOR
using Stride.Core.Annotations;
using Stride.Games;
#endif

namespace SceneEditorExtensionExample.StrideEditorExt;

class SceneEditorExtProcessor : EntityProcessor<SceneEditorExtBase>
{
#if GAME_EDITOR
    public SceneEditorExtProcessor()
    {

    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] SceneEditorExtBase component, [NotNull] SceneEditorExtBase data)
    {
        if (component.IsInitialized)
        {
            component.Deinitialize();
        }
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var kv in ComponentDatas)
        {
            var sceneEditorComp = kv.Key;
            if (!sceneEditorComp.IsInitialized)
            {
                var uiComponent = sceneEditorComp.Entity.Get<UIComponent>();
                if (uiComponent is not null && uiComponent.Page?.RootElement is null)
                {
                    // Can't initialize yet, UI not ready
                }
                else
                {
                    sceneEditorComp.Initialize(Services, uiComponent);
                }
                continue;   // Don't need to update immediately after being initialized (doesn't really matter)
            }

            sceneEditorComp.Update(gameTime);
        }
    }
#endif
}
