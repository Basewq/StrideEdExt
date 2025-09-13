using Stride.Core.Serialization;

namespace StrideEdExt.StrideEditorExt;

static class EditorExtensions
{
    /// <summary>
    /// HACK: Editor assigns a proxy object before properly loading the object.
    /// We are required to check for this to ensure the proper data is being used.
    /// </summary>
    public static bool IsRuntimeAssetLoaded(object runtimeAsset)
    {
#if GAME_EDITOR
        var attachedRef = AttachedReferenceManager.GetAttachedReference(runtimeAsset);
        if (attachedRef?.IsProxy ?? false)
        {
            return false;     // Editor still loading the item
        }
#endif
        return true;
    }
}
