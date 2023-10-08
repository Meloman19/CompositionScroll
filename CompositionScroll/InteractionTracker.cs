using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Threading;

namespace CompositionScroll
{
    internal enum RequestType
    {
        AnimatePositionBy,
        ShiftPositionBy,
        UpdatePositionTo
    }

    internal readonly struct InteractionTrackerRequest
    {
        public InteractionTrackerRequest(RequestType requestType, Vector3D value, long requestId)
        {
            Type = requestType;
            Value = value;
            RequestId = requestId;
        }

        public RequestType Type { get; }

        public Vector3D Value { get; }

        public long RequestId { get; }
    }

    public sealed class InteractionTracker : CompositionObject
    {
        private readonly IInteractionTrackerOwner _owner;
        private List<object> _messages;
        private long _requestId = 0;

        internal InteractionTracker(Compositor compositor, IInteractionTrackerOwner owner)
            : base(compositor, new ServerInteractionTracker(compositor.Server))
        {
            _owner = owner;
        }

        internal void Init()
        {
            var server = Server as ServerInteractionTracker;
            server.Init(this);
        }


        public void AnimatePositionBy(Vector3D deltaPosition)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            SendHandlerMessage(new InteractionTrackerRequest(RequestType.AnimatePositionBy, deltaPosition, requestId));
        }

        public void ShiftPositionBy(Vector3D shift)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            SendHandlerMessage(new InteractionTrackerRequest(RequestType.ShiftPositionBy, shift, requestId));
        }

        public long UpdatePosition(Vector3D newPosition)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            SendHandlerMessage(new InteractionTrackerRequest(RequestType.UpdatePositionTo, newPosition, requestId));
            return requestId;
        }

        public void SetMaxPosition(Vector3D maxPosition)
        {
            (Server as ServerInteractionTracker).MaxPosition = maxPosition;
        }
        private void SendHandlerMessage(object message)
        {
            (_messages ??= new()).Add(message);
            RegisterForSerialization();
        }

        public override void SerializeChangesCore(BatchStreamWriter writer)
        {
            base.SerializeChangesCore(writer);
            if (_messages == null || _messages.Count == 0)
                writer.Write(0);
            else
            {
                writer.Write(_messages.Count);
                foreach (var m in _messages)
                    writer.WriteObject(m);
                _messages.Clear();
            }
        }

        private long _uiRequestId = 0;
        internal void ValuesChanged(Vector3D position, long requestId)
        {
            if (_owner == null)
                return;

            var arg = new InteractionTrackerValuesChangedArgs(position, requestId);
            var uiRequestId = Interlocked.Increment(ref _uiRequestId);
            Compositor.Dispatcher.InvokeAsync(() => UIValuesChanged(uiRequestId, arg), DispatcherPriority.Background);
        }

        private void UIValuesChanged(long requestId, InteractionTrackerValuesChangedArgs arg)
        {
            var currentRequestId = Interlocked.Read(ref _uiRequestId);
            if (currentRequestId == requestId)
                _owner.ValuesChanged(this, arg);
        }
    }

    public static class Factory
    {
        public static InteractionTracker CreateInteractionTracker(this Compositor compositor)
        {
            return CreateInteractionTracker(compositor, null);
        }

        public static InteractionTracker CreateInteractionTracker(this Compositor compositor, IInteractionTrackerOwner owner)
        {
            var tracker = new InteractionTracker(compositor, owner);
            tracker.Init();
            return tracker;
        }
    }
}