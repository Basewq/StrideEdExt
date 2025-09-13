namespace StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

/// <summary>
/// Interface for an editor tool on the runtime game to send requests to the editor.
/// </summary>
public interface IRuntimeToEditorMessagingService
{
    /// <summary>
    /// Register a listener and message handler for a particular message type sent from the editor.
    /// </summary>
    /// <returns>Subscription that should be disposed to unsubscribe.</returns>
    IDisposable Subscribe<TMessage>(object recipient, Action<TMessage> messageHandler)
        where TMessage : IEditorToRuntimeMessage;

    /// <summary>
    /// Send a request to the editor.
    /// </summary>
    void Send<TRequest>(TRequest request)
        where TRequest : IRuntimeToEditorRequest;
}
