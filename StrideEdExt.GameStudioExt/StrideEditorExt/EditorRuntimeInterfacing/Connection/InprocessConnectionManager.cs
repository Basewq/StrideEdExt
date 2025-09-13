namespace StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing.Connection;

public class InprocessConnectionManager
{
    // Only one exists
    private readonly InprocessMessagingEndpoint _editorEndpoint;
    private readonly List<InprocessMessagingEndpoint> _runtimeEndpoints = [];

    public InprocessConnectionManager()
    {
        _editorEndpoint = new(this);
    }

    public InprocessMessagingEndpoint GetEditorEndpoint()
    {
        return _editorEndpoint;
    }

    public InprocessMessagingEndpoint CreateRuntimeEndpoint()
    {
        var endpoint = new InprocessMessagingEndpoint(this);
        lock (_runtimeEndpoints)
        {
            _runtimeEndpoints.Add(endpoint);
        }
        return endpoint;
    }

    internal void SendData(InprocessMessagingEndpoint endpoint, object data)
    {
        if (endpoint == _editorEndpoint)
        {
            foreach (var runtimeEndpoint in _runtimeEndpoints)
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
            lock (_runtimeEndpoints)
            {
                bool wasRemoved = _runtimeEndpoints.Remove(endpoint);
                if (!wasRemoved)
                {
                    throw new ArgumentException("Endpoint was not registered.");
                }
            }
        }
    }
}
