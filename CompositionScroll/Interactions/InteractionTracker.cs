using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;
using CompositionScroll.Interactions.Server;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CompositionScroll.Interactions
{
    internal enum RequestType
    {
        AnimatePositionBy,
        ShiftPositionBy,
        UpdatePositionTo,
        AnimatePositionByVel,
        GestureStart,
        GestureEnd,
        GestureMove,
    }

    internal readonly struct InteractionTrackerRequest
    {
        public InteractionTrackerRequest(RequestType requestType, Vector3D value, long requestId)
        {
            Type = requestType;
            VectorValue = value;
            RequestId = requestId;
        }

        public RequestType Type { get; init; }

        public Vector3D? VectorValue { get; init; }

        public TimeSpan? TimeSpanValue { get; init; }

        public int? IntValue { get; init; }

        public long RequestId { get; init; }
    }

    public sealed class InteractionTracker : CompositionObject
    {
        private InteractionSource _interactionSource;

        private readonly IInteractionTrackerOwner _owner;
        private List<InteractionTrackerRequest> _messages;
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

        public InteractionSource InteractionSource
        {
            get => _interactionSource;
            set
            {
                if (_interactionSource != null)
                    _interactionSource.SetInteractionTracker(null);
                _interactionSource = value;
                if (_interactionSource != null)
                    _interactionSource.SetInteractionTracker(this);
            }
        }

        public void AnimatePositionBy(Vector3D deltaPosition)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.AnimatePositionBy,
                VectorValue = deltaPosition,
                RequestId = requestId,
            };
            SendHandlerMessage(request);
        }

        internal void AnimatePositionByVel(Vector3D vel)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.AnimatePositionByVel,
                VectorValue = vel,
                RequestId = requestId,
            };
            SendHandlerMessage(request);
        }

        public void AnimatePositionBy(Vector3D deltaPosition, TimeSpan duration)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.AnimatePositionBy,
                VectorValue = deltaPosition,
                TimeSpanValue = duration,
                RequestId = requestId,
            };
            SendHandlerMessage(request);
        }

        internal void BeginUserInteraction(int gestureId)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.GestureStart,
                RequestId = requestId,
                IntValue = gestureId
            };
            SendHandlerMessage(request);
        }

        internal void GestureEnd(int gestureId)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.GestureEnd,
                RequestId = requestId,
                IntValue = gestureId
            };
            SendHandlerMessage(request);
        }

        internal void GestureMove(int gestureId, Vector3D delta)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new InteractionTrackerRequest
            {
                Type = RequestType.GestureMove,
                RequestId = requestId,
                IntValue = gestureId,
                VectorValue = delta,
            };
            SendHandlerMessage(request);
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

        private void SendHandlerMessage(InteractionTrackerRequest message)
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
                    writer.Write(m);
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
            return compositor.CreateInteractionTracker(null);
        }

        public static InteractionTracker CreateInteractionTracker(this Compositor compositor, IInteractionTrackerOwner owner)
        {
            var tracker = new InteractionTracker(compositor, owner);
            tracker.Init();
            return tracker;
        }
    }
}