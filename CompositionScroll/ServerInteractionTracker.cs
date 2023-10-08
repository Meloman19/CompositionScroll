using Avalonia;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using System;

namespace CompositionScroll
{
    internal sealed class ServerInteractionTracker : ServerObject, IDisposable, IServerClockItem
    {
        private class Moving
        {
            private TimeSpan? _startTime;
            private bool _isEnded = false;

            private Moving()
            {

            }

            public Vector3D StartPosition { get; private set; }

            public Vector3D CurrentPosition { get; private set; }

            public Vector3D EndPosition { get; private set; }

            private Vector3D Velocity { get; set; }

            private Vector3D Acceleration { get; set; }

            private double Duration { get; set; }

            public bool IsStarted => _startTime.HasValue;

            public bool IsEnded => _isEnded;

            public bool Gesture { get; private set; }

            public void Move(TimeSpan now)
            {
                if (!_startTime.HasValue)
                    _startTime = now;

                if (_isEnded)
                    return;

                var deltaT = now - _startTime.Value;
                var deltaTSec = deltaT.TotalSeconds;

                if (deltaTSec >= Duration)
                {
                    CurrentPosition = EndPosition;
                    _isEnded = true;
                    return;
                }

                CurrentPosition = StartPosition + Velocity * deltaTSec + Acceleration * (deltaTSec * deltaTSec / 2);
            }

            public static Moving Animate(Vector3D startPosition, TimeSpan duration, Vector3D deltaPosition)
            {
                var endPosition = startPosition + deltaPosition;
                var startVelocity = Vector3D.Divide(deltaPosition, 0.5d * duration.TotalSeconds);
                var acceleration = Vector3D.Divide(-startVelocity, duration.TotalSeconds);

                return new Moving
                {
                    StartPosition = startPosition,
                    CurrentPosition = startPosition,
                    EndPosition = endPosition,
                    Velocity = startVelocity,
                    Acceleration = acceleration,
                    Duration = duration.TotalSeconds,
                    Gesture = false
                };
            }

            public Moving AnimateBy(TimeSpan duration, Vector3D deltaPosition)
            {
                var endPosition = EndPosition + deltaPosition;
                var totalDeltaPosition = endPosition - CurrentPosition;
                var velocity = Vector3D.Divide(totalDeltaPosition, 0.5d * duration.TotalSeconds);
                var acceleration = Vector3D.Divide(-velocity, duration.TotalSeconds);

                return new Moving
                {
                    StartPosition = CurrentPosition,
                    CurrentPosition = CurrentPosition,
                    EndPosition = endPosition,
                    Velocity = velocity,
                    Acceleration = acceleration,
                    Duration = duration.TotalSeconds,
                    Gesture = false
                };
            }

            public Moving ShiftBy(Vector3D shift)
            {
                this.StartPosition += shift;
                this.CurrentPosition += shift;
                this.EndPosition += shift;
                return this;
            }
        }

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

        private readonly TimeSpan _defaultDuration = TimeSpan.FromMilliseconds(200);
        private void OnMessage(object message)
        {
            if (message is not InteractionTrackerRequest request)
                return;

            switch (request.Type)
            {
                case RequestType.AnimatePositionBy:
                    if (_moving == null)
                        _moving = Moving.Animate(_position, _defaultDuration, request.Value);
                    else if (!_moving.Gesture)
                        _moving = _moving.AnimateBy(_defaultDuration, request.Value);
                    break;
                case RequestType.UpdatePositionTo:
                    if (_moving == null || !_moving.Gesture)
                    {
                        _moving = null;
                        SetPosition(request.Value, request.RequestId);
                    }
                    break;
                case RequestType.ShiftPositionBy:
                    if (_moving == null)
                        SetPosition(_position + request.Value, request.RequestId);
                    else
                        _moving = _moving.ShiftBy(request.Value);
                    break;
                default:
                    break;
            }
        }

        private void SetPosition(Vector3D newPosition, long requestId)
        {
            newPosition = Vector3D.Clamp(newPosition, MinPosition, MaxPosition);

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
                    OnMessage(reader.ReadObject()!);
                }
                catch (Exception e)
                {
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