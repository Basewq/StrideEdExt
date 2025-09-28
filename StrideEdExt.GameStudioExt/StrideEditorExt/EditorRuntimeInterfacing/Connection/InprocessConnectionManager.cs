namespace StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing.Connection;

public class InprocessConnectionManager
{
    // Only one editor endpoint exists
    private readonly InprocessMessagingEndpoint _editorEndpoint;
    private readonly List<InprocessMessagingEndpoint> _listenerEndpoints = [];

    public InprocessConnectionManager()
    {
        _editorEndpoint = new(this);
    }

    public InprocessMessagingEndpoint GetEditorEndpoint()
    {
        return _editorEndpoint;
    }

    public InprocessMessagingEndpoint CreateEditorListenerEndpoint()
    {
        var endpoint = new InprocessMessagingEndpoint(this);
        lock (_listenerEndpoints)
        {
            _listenerEndpoints.Add(endpoint);
        }
        return endpoint;
    }

    internal void SendData(InprocessMessagingEndpoint endpoint, object data)
    {
        if (endpoint == _editorEndpoint)
        {
            foreach (var runtimeEndpoint in _listenerEndpoints)
            {
                runtimeEndpoint.ReceiveData(data);
            }
        }
        else
        {
            _editorEndpoint.ReceiveData(data);
        }
    }

    internal void RemoveEndpoint(InprocessMessagingEndpoint endpoint)
    {
        if (endpoint == _editorEndpoint)
        {
            throw new ArgumentException("Cannot remove the editor endpoint.");
        }
        else
        {
            lock (_listenerEndpoints)
            {
                bool wasRemoved = _listenerEndpoints.Remove(endpoint);
                if (!wasRemoved)
                {
                    throw new ArgumentException("Endpoint was not registered.");
                }
            }
        }
    }
}
