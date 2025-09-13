using Stride.Core.Presentation.Services;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using System.Diagnostics;

namespace StrideEdExt.GameStudioExt.StrideEditorExt.EditorRuntimeInterfacing;

class EditorToRuntimeMessagingService : IEditorToRuntimeMessagingService
{
    private readonly List<IRuntimeToEditorRequestHandler> _requestHandlers = [];
    private readonly Dictionary<Type, List<ISubscriberRegistration>> _requestTypeToSubscribers = [];
    private readonly IMessagingEndpoint _editorEndpoint;
    private readonly IDispatcherService _dispatcher;

    public EditorToRuntimeMessagingService(IMessagingEndpoint editorEndpoint, IDispatcherService dispatcher)
    {
        _editorEndpoint = editorEndpoint;
        _editorEndpoint.DataReceived += OnEndpointDataReceived;
        _dispatcher = dispatcher;
    }

    private void OnEndpointDataReceived(object? sender, object data)
    {
        if (data is not IRuntimeToEditorRequest request)
        {
            throw new ArgumentException($"Invalid data type received: {data.GetType().Name} - expected type: {typeof(IRuntimeToEditorRequest).Name}");
        }
        Debug.WriteLineIf(condition: true, $"Request received: {request.GetType().Name}");

        // Dispatcher ensures requests are processed on the UI thread.
        _dispatcher.Invoke(() =>
        {
            foreach (var reqHandler in _requestHandlers)
            {
                reqHandler.ProcessRequest(request);
            }
            if (_requestTypeToSubscribers.TryGetValue(request.GetType(), out var handlers))
            {
                foreach (var reqHandler in handlers)
                {
                    reqHandler.ReceiveRequest(request);
                }
            }
        });
    }

    public IDisposable RegisterHandler(IRuntimeToEditorRequestHandler requestHandler)
    {
        var handlerReg = new HandlerRegistration(this, requestHandler);
        _requestHandlers.Add(requestHandler);
        return handlerReg;
    }

    public IDisposable Subscribe<TRequest>(object recipient, Action<TRequest> requestHandler, Predicate<TRequest>? additionalConstraints = null)
        where TRequest : IRuntimeToEditorRequest
    {
        var subReg = new SubscriberRegistration<TRequest>(this, recipient, requestHandler, additionalConstraints);
        if (!_requestTypeToSubscribers.TryGetValue(typeof(TRequest), out var subscribers))
        {
            subscribers = new();
            _requestTypeToSubscribers[typeof(TRequest)] = subscribers;
        }
        subscribers.Add(subReg);
        return subReg;
    }


    public void Send<TMessage>(TMessage message)
        where TMessage : IEditorToRuntimeMessage
    {
        Debug.WriteLineIf(condition: true, $"Sending message: {message.GetType().Name}");
        _editorEndpoint.SendData(message);
    }

    private void Unsubscribe(IRuntimeToEditorRequestHandler requestHandler)
    {
        bool wasRemoved = _requestHandlers.Remove(requestHandler);
        if (!wasRemoved)
        {
            throw new ArgumentException($"Handler does not exist in the registered list: {requestHandler.GetType().Name}");
        }
    }

    private void Unsubscribe<TRequest>(SubscriberRegistration<TRequest> subscriberRegistration)
        where TRequest : IRuntimeToEditorRequest
    {
        if (_requestTypeToSubscribers.TryGetValue(typeof(TRequest), out var subscribers))
        {
            bool wasRemoved = subscribers.Remove(subscriberRegistration);
            if (!wasRemoved)
            {
                throw new ArgumentException($"Subscriber does not exist in the registered list: {subscriberRegistration.Recipient.GetType().Name}");
            }
        }
        else
        {
            throw new ArgumentException($"Request type to subscriber list does not exist: {typeof(TRequest).Name}");
        }
    }

    private interface IHandlerRegistration : IDisposable { }

    private class HandlerRegistration : IHandlerRegistration
    {
        private readonly EditorToRuntimeMessagingService _editorToRuntimeMessagingService;
        private bool _isDisposed;

        private readonly IRuntimeToEditorRequestHandler _requestHandler;

        public HandlerRegistration(EditorToRuntimeMessagingService editorToRuntimeMessagingService, IRuntimeToEditorRequestHandler requestHandler)
        {
            _editorToRuntimeMessagingService = editorToRuntimeMessagingService;
            _requestHandler = requestHandler;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _editorToRuntimeMessagingService.Unsubscribe(_requestHandler);
                _isDisposed = true;
            }
        }
    }

    private interface ISubscriberRegistration : IDisposable
    {
        void ReceiveRequest(IRuntimeToEditorRequest request);
    }

    private class SubscriberRegistration<TRequest> : ISubscriberRegistration
        where TRequest : IRuntimeToEditorRequest
    {
        private readonly EditorToRuntimeMessagingService _editorToRuntimeMessagingService;
        private bool _isDisposed;

        internal readonly object Recipient;
        internal readonly Action<TRequest> RequestHandler;
        internal readonly Predicate<TRequest>? AdditionalConstraints;

        public SubscriberRegistration(EditorToRuntimeMessagingService editorToRuntimeMessagingService, object recipient, Action<TRequest> requestHandler, Predicate<TRequest>? additionalConstraints)
        {
            _editorToRuntimeMessagingService = editorToRuntimeMessagingService;
            Recipient = recipient;
            RequestHandler = requestHandler;
            AdditionalConstraints = additionalConstraints;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _editorToRuntimeMessagingService.Unsubscribe(this);
                _isDisposed = true;
            }
        }

        public void ReceiveRequest(IRuntimeToEditorRequest request)
        {
            if (request is not TRequest castedMessage)
            {
                throw new ArgumentException($"Invalid data type received: {request.GetType().Name} - expected type: {typeof(TRequest).Name}");
            }
            if (AdditionalConstraints?.Invoke(castedMessage) == false)
            {
                return;
            }
            RequestHandler?.Invoke(castedMessage);
        }
    }
}
