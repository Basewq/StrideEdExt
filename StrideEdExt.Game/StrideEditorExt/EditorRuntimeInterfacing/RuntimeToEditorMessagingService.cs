using Stride.Core;
using Stride.Engine;
using Stride.Games;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using System.Diagnostics;

namespace StrideEdExt.StrideEditorExt.EditorRuntimeInterfacing;

class RuntimeToEditorMessagingService : GameSystem, IRuntimeToEditorMessagingService
{
    // Inherit GameSystem to ensure we pass the messages within the game thread (via Update method)

    private readonly Dictionary<Type, List<ISubscriberRegistration>> _messageTypeToSubscribers = [];
    private readonly IMessagingEndpoint _runtimeEndpoint;

    private readonly List<IEditorToRuntimeMessage> _pendingMessages = [];

    public RuntimeToEditorMessagingService(IServiceRegistry registry, IMessagingEndpoint runtimeEndpoint)
         : base(registry)
    {
        Enabled = true;
        UpdateOrder = -1000000;

        _runtimeEndpoint = runtimeEndpoint;
        _runtimeEndpoint.DataReceived += OnEndpointDataReceived;

        registry.AddService<IRuntimeToEditorMessagingService>(this);
    }

    private void OnEndpointDataReceived(object? sender, object data)
    {
        if (data is not IEditorToRuntimeMessage message)
        {
            throw new ArgumentException($"Invalid data type received: {data.GetType().Name} - expected type: {typeof(IRuntimeToEditorRequest).Name}");
        }
        Debug.WriteLineIf(condition: false, $"OnEndpointDataReceived: Message received: {message.GetType().Name}");

        lock (_pendingMessages)
        {
            _pendingMessages.Add(message);
        }
    }

    private readonly List<IEditorToRuntimeMessage> _processingMessages = [];
    public override void Update(GameTime gameTime)
    {
        lock (_pendingMessages)
        {
            if (_pendingMessages.Count > 0)
            {
                _processingMessages.AddRange(_pendingMessages);
                _pendingMessages.Clear();
            }
        }

        if (_processingMessages.Count > 0)
        {
            foreach (var message in _processingMessages)
            {
                Debug.WriteLineIf(condition: false, $"Message received: {message.GetType().Name}");
                if (_messageTypeToSubscribers.TryGetValue(message.GetType(), out var handlers))
                {
                    Debug.WriteLineIf(condition: handlers.Count > 0, $"Message handler ReceiveMessage: {message.GetType().Name}");
                    foreach (var msgHandler in handlers)
                    {
                        msgHandler.ReceiveMessage(message);
                    }
                }
            }
            _processingMessages.Clear();
        }
    }

    public IDisposable Subscribe<TMessage>(object recipient, Action<TMessage> messageHandler)
        where TMessage : IEditorToRuntimeMessage
    {
        var subReg = new SubscriberRegistration<TMessage>(this, recipient, messageHandler);
        if (!_messageTypeToSubscribers.TryGetValue(typeof(TMessage), out var subscribers))
        {
            subscribers = new();
            _messageTypeToSubscribers[typeof(TMessage)] = subscribers;
        }
        subscribers.Add(subReg);
        return subReg;
    }

    public void Send<TRequest>(TRequest request)
        where TRequest : IRuntimeToEditorRequest
    {
        Debug.WriteLineIf(condition: true, $"Sending request: {request.GetType().Name}");
        _runtimeEndpoint.SendData(request);
    }

    private void Unsubscribe<TMessage>(SubscriberRegistration<TMessage> subscriberRegistration)
        where TMessage : IEditorToRuntimeMessage
    {
        if (_messageTypeToSubscribers.TryGetValue(typeof(TMessage), out var subscribers))
        {
            bool wasRemoved = subscribers.Remove(subscriberRegistration);
            if (!wasRemoved)
            {
                throw new ArgumentException($"Subscriber does not exist in the registered list: {subscriberRegistration.Recipient.GetType().Name}");
            }
        }
        else
        {
            throw new ArgumentException($"Message type to subscriber list does not exist: {typeof(TMessage).Name}");
        }
    }

    private interface ISubscriberRegistration : IDisposable
    {
        void ReceiveMessage(IEditorToRuntimeMessage message);
    }

    private class SubscriberRegistration<TMessage> : ISubscriberRegistration
        where TMessage : IEditorToRuntimeMessage
    {
        private readonly RuntimeToEditorMessagingService _runtimeToEditorMessagingService;
        private bool _isDisposed;

        internal readonly object Recipient;
        internal readonly Action<TMessage> MessageHandler;

        public SubscriberRegistration(RuntimeToEditorMessagingService runtimeToEditorMessagingService, object recipient, Action<TMessage> messageHandler)
        {
            _runtimeToEditorMessagingService = runtimeToEditorMessagingService;
            Recipient = recipient;
            MessageHandler = messageHandler;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _runtimeToEditorMessagingService.Unsubscribe(this);
                _isDisposed = true;
            }
        }

        public void ReceiveMessage(IEditorToRuntimeMessage message)
        {
            if (message is not TMessage castedMessage)
            {
                throw new ArgumentException($"Invalid data type received: {message.GetType().Name} - expected type: {typeof(TMessage).Name}");
            }
            MessageHandler?.Invoke(castedMessage);
        }
    }
}
