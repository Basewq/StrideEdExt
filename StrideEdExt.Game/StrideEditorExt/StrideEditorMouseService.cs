﻿#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core.Annotations;
using Stride.Editor.EditorGame.Game;
using Stride.Games;
using System.Diagnostics;
using System.Reflection;
#endif

namespace StrideEdExt.StrideEditorExt;

// HACK: This class is required so our painter has exclusive control over the mouse.
#if GAME_EDITOR
class StrideEditorMouseService : EditorGameMouseServiceBase, IStrideEditorMouseService
{
    public object? Owner { get; set; }

    public override bool IsControllingMouse { get; protected set; }

    protected override Task<bool> Initialize([NotNull] EditorServiceGame editorGame)
    {
        Debug.WriteLine("InternalEditorMouseService Initialize");
        return Task.FromResult(true);
    }

    public void SetIsControllingMouse(bool isControllingMouse, object owner)
    {
        if (isControllingMouse)
        {
            Owner = owner;
            IsControllingMouse = isControllingMouse;
        }
        else if (!isControllingMouse && Owner == owner)
        {
            Owner = null;
            IsControllingMouse = isControllingMouse;
        }
    }

    public static IStrideEditorMouseService GetOrCreate(Stride.Core.IServiceRegistry services)
    {
        var sceneEditorGame = (services.GetService<IGame>() as SceneEditorGame)!;
        var editorMouseService = sceneEditorGame.EditorServices.Get<StrideEditorMouseService>();
        if (editorMouseService is null)
        {
            Debug.WriteLine($"{nameof(StrideEditorMouseService)} added.");

            editorMouseService = new();

            // HACK: Every EditorGameMouseServiceBase derived classes hold a LOCAL copy of
            // every other mouse service instead of reading from some common registry...
            // This code manually goes through every other mouse service and add our one in.
            var mouseServiceType = typeof(EditorGameMouseServiceBase);
            var mouseSvceListFieldInfo = mouseServiceType.GetField("mouseServices", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(mouseSvceListFieldInfo is not null);

            foreach (var editorService in sceneEditorGame.EditorServices.Services)
            {
                if (editorService is not EditorGameMouseServiceBase mouseSvce)
                {
                    continue;
                }
                var mouseSvceList = mouseSvceListFieldInfo.GetValue(mouseSvce) as List<IEditorGameMouseService>;
                if (mouseSvceList is not null)
                {
                    Debug.WriteLine($"Found mouse services: {mouseSvceList.Count}");
                    mouseSvceList.Add(editorMouseService);
                }
            }

            editorMouseService.InitializeService(sceneEditorGame);

            var editorServiceType = typeof(EditorGameServiceBase);
            var editorSvceRegisterMouseServicesMethodInfo = mouseServiceType.GetMethod("RegisterMouseServices", BindingFlags.Instance | BindingFlags.NonPublic);
            EditorGameServiceRegistry serviceRegistry = sceneEditorGame.EditorServices;
            editorSvceRegisterMouseServicesMethodInfo?.Invoke(editorMouseService, [serviceRegistry]);
            sceneEditorGame.EditorServices.Add(editorMouseService);
        }

        return editorMouseService;
    }
}
#else
class StrideEditorMouseService : IStrideEditorMouseService
{
    public void SetIsControllingMouse(bool isControllingMouse, object owner)
    {
        // Nothing
    }

    public static IStrideEditorMouseService GetOrCreate(Stride.Core.IServiceRegistry services)
    {
        return new StrideEditorMouseService();
    }
}
#endif
