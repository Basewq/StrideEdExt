#if GAME_EDITOR
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core.Annotations;
using Stride.Editor.EditorGame.Game;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;

// HACK: This class is required so our painter has exclusive control over the mouse.
// Refer to TerrainMapPainterProcessor.OnSystemAdd & ProcessEditor to see how it is used.
class TerrainMapPainterEditorMouseService : EditorGameMouseServiceBase
{
    public override bool IsControllingMouse { get; protected set; }

    protected override Task<bool> Initialize([NotNull] EditorServiceGame editorGame)
    {
        Debug.WriteLine($"{nameof(TerrainMapPainterEditorMouseService)} Initialize");
        return Task.FromResult(true);
    }

    public void SetIsControllingMouse(bool isControllingMouse)
    {
        IsControllingMouse = isControllingMouse;
    }
}
#endif
