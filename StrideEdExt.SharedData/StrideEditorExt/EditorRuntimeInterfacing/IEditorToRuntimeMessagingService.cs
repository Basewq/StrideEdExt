namespace StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

/// <summary>
/// Interface for the editor to send messages to any runtime games running.
/// </summary>
public interface IEditorToRuntimeMessagingService
{
    /// <summary>
    /// Register a request handler for processing all requests sent from the runtime game.
    /// </summary>
    /// <returns>Subscription that should be disposed to unregister.</returns>
    IDisposable RegisterHandler(IRuntimeToEditorRequestHandler requestHandler);

    /// <summary>
    /// Register a listener and message request for a particular request type sent from the runtime game.
    /// </summary>
    /// <returns>Subscription that should be disposed to unsubscribe.</returns>
    IDisposable Subscribe<TRequest>(object recipient, Action<TRequest> messageHandler, Predicate<TRequest>? additionalConstraints = null)
        where TRequest : IRuntimeToEditorRequest;

    /// <summary>
    /// Send a message to all subscribers of this message type.
    /// </summary>
    void Send<TMessage>(TMessage message)
        where TMessage : IEditorToRuntimeMessage;
}
