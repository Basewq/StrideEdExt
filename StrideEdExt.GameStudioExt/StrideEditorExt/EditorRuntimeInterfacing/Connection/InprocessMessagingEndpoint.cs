using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

namespace StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing.Connection;

public class InprocessMessagingEndpoint : IMessagingEndpoint, IDisposable
{
    private bool _isDisposed;
    private InprocessConnectionManager _inprocessConnectionManager;

    public event EventHandler<object>? DataReceived;

    internal InprocessMessagingEndpoint(InprocessConnectionManager inprocessConnectionManager)
    {
        _inprocessConnectionManager = inprocessConnectionManager;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _inprocessConnectionManager.RemoveEndpoint(this);
        _isDisposed = true;
    }

    public void SendData(object data)
    {
        _inprocessConnectionManager.SendData(this, data);
    }

    internal void ReceiveData(object data)
    {
        DataReceived?.Invoke(this, data);
    }
}
