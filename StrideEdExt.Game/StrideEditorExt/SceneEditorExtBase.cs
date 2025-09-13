using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StrideEdExt.StrideEditorExt;

[ComponentCategory("Scene Editors")]
[DataContract(Inherited = true)]
[DefaultEntityComponentProcessor(typeof(SceneEditorExtProcessor), ExecutionMode = ExecutionMode.Editor)]
public abstract class SceneEditorExtBase : EntityComponent, INotifyPropertyChanged
{
    internal bool IsInitialized { get; private set; }

    [DataMemberIgnore]
    protected internal IRuntimeToEditorMessagingService? RuntimeToEditorMessagingService { get; private set; }

#if GAME_EDITOR
    [DataMemberIgnore]
    protected internal IServiceRegistry Services { get; private set; } = default!;
    [DataMemberIgnore]
    protected internal IStrideEditorService StrideEditorService { get; private set; } = default!;
    protected internal UIComponent? UIComponent { get; private set; } = default!;

    private Scene? _rootScene;
    protected Scene RootScene
    {
        get
        {
            if (_rootScene is null)
            {
                var scene = Entity.Scene;
                while (scene.Parent is not null)
                {
                    scene = scene.Parent;
                }
                _rootScene = scene;
            }
            return _rootScene;
        }
    }

    protected internal abstract void Initialize();
    protected internal abstract void Deinitialize();
    protected internal virtual void Update(GameTime gameTime) { }

    internal void Initialize(IServiceRegistry services, UIComponent? uiComponent)
    {
        Services = services;
        StrideEditorService = services.GetSafeServiceAs<IStrideEditorService>();
        RuntimeToEditorMessagingService = services.GetSafeServiceAs<IRuntimeToEditorMessagingService>();
        UIComponent = uiComponent;

        Initialize();

        IsInitialized = true;
    }
#endif

    protected bool SetProperty<T>(
        ref T backingStore,
        T value,
        [CallerMemberName] string propertyName = default!,
        Action? onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        onChanged?.Invoke();
        OnPropertyChanged(propertyName);
        return true;
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = default!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}
