using Avalonia;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using System;
using System.IO;

namespace CompositionScroll.Interactions.Server
{
    internal sealed partial class ServerInteractionTracker : ServerObject, IDisposable, IServerClockItem
    {
        private enum State
        {
            Idle,
            Inertia
        }

        private Moving _moving;
        private State _state = State.Idle;
        private InteractionTracker _interactionTracker;

        public ServerInteractionTracker(ServerCompositor compositor)
            : base(compositor)
        {
        }

        internal static CompositionProperty IdOfPositionProperty = CompositionProperty.Register();
        private Vector3D _position;
        public Vector3D Position
        {
            get => GetAnimatedValue(IdOfPositionProperty, ref _position);
            set => SetAnimatedValue(IdOfPositionProperty, out _position, value);
        }

        public Vector3D MinPosition { get; set; }

        public Vector3D MaxPosition { get; set; }

        public void Init(InteractionTracker tracker)
        {
            _interactionTracker = tracker;
            Compositor.AddToClock(this);
        }

        public void Dispose()
        {
            Compositor.RemoveFromClock(this);
        }

        public void OnTick()
        {
            if (_moving == null)
                return;

            var now = Compositor.ServerNow;
            _moving.Move(now);

            var newPosition = _moving.CurrentPosition;

            if (_moving.IsEnded)
                _moving = null;

            SetPosition(newPosition, 0);
        }

        private readonly TimeSpan _defaultDuration = TimeSpan.FromMilliseconds(250);
        private void OnMessage(object message)
        {
            if (message is not InteractionTrackerRequest request)
                return;

            switch (request.Type)
            {
                case RequestType.AnimatePositionBy:
                    if (_moving == null)
                        _moving = Moving.Animate(this, request.TimeSpanValue ?? _defaultDuration, request.VectorValue.Value);
                    else if (!_moving.Gesture)
                        _moving = _moving.AnimateBy(request.TimeSpanValue ?? _defaultDuration, request.VectorValue.Value);
                    break;
                case RequestType.AnimatePositionByVel:
                    if (_moving == null)
                        _moving = Moving.AnimateVelocity(this, request.VectorValue.Value);
                    break;
                case RequestType.UpdatePositionTo:
                    if (_moving == null || !_moving.Gesture)
                    {
                        _moving = null;
                        SetPosition(request.VectorValue.Value, request.RequestId);
                    }
                    break;
                case RequestType.ShiftPositionBy:
                    if (_moving == null)
                        SetPosition(_position + request.VectorValue.Value, request.RequestId);
                    else
                        _moving = _moving.ShiftBy(request.VectorValue.Value);
                    break;
                case RequestType.GestureStart:
                    if (_moving == null || !_moving.Gesture)
                        _moving = Moving.StartGesture(this, request.IntValue.Value);
                    break;
                case RequestType.GestureEnd:
                    if (_moving != null && _moving.Gesture)
                        _moving = null;
                    break;
                case RequestType.GestureMove:
                    if (_moving != null && _moving.Gesture)
                        _moving.Delta(request.VectorValue.Value);
                    break;
                default:
                    break;
            }
        }

        private void SetPosition(Vector3D newPosition, long requestId)
        {
            if (_position == newPosition)
                return;

            Position = newPosition;
            _interactionTracker.ValuesChanged(_position, requestId);
        }

        protected override void DeserializeChangesCore(BatchStreamReader reader, TimeSpan committedAt)
        {
            base.DeserializeChangesCore(reader, committedAt);
            var count = reader.Read<int>();
            for (var c = 0; c < count; c++)
            {
                try
                {
                    OnMessage(reader.Read<InteractionTrackerRequest>());
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }

        public override ExpressionVariant GetPropertyForAnimation(string name)
        {
            if (name == nameof(Position))
                return Position;

            return base.GetPropertyForAnimation(name);
        }

        public override CompositionProperty GetCompositionProperty(string fieldName)
        {
            if (fieldName == nameof(Position))
                return IdOfPositionProperty;

            return base.GetCompositionProperty(fieldName);
        }
    }
}