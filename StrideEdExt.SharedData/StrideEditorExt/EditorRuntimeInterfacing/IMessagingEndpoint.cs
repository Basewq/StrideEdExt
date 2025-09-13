namespace StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

public interface IMessagingEndpoint : IDisposable
{
    event EventHandler<object>? DataReceived;

    void SendData(object data);
}
