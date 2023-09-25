using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace AvaloniaCompositionScrollExample.Scroll
{
    internal enum RequestType
    {
        AddVelocity,
        ChangePositionBy,
        ChangePositionByToIdle,
        ChangePosition,
        ChangePositionToIdle
    }

    internal readonly struct InteractionTrackerRequest
    {
        public InteractionTrackerRequest(RequestType requestType, Vector3D value)
        {
            Type = requestType;
            Value = value;
        }

        public RequestType Type { get; }

        public Vector3D Value { get; }
    }

    public sealed class InteractionTracker : CompositionCustomVisualHandler
    {
        internal const double InertialScrollSpeedEnd = 5;
        internal const double InertialResistance = 0.15;

        private enum State
        {
            Idle,
            Inertia
        }

        private readonly IInteractionTrackerOwner _owner;

        private Vector3D _acceleration;

        private State _state = State.Idle;
        private Vector3D _position;
        private Vector3D _velocity;
        private TimeSpan _prevTick;

        public InteractionTracker(IInteractionTrackerOwner owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            _owner = owner;
        }

        public Vector3D MinPosition { get; set; }

        public Vector3D MaxPosition { get; set; }

        public Vector3D Position => _position;

        public Vector3D PositionInertiaDecayRate { get; set; } = new Vector3D(4, 4, 4);

        public override void OnAnimationFrameUpdate()
        {
            RegisterForNextAnimationFrameUpdate();

            var oldPosition = _position;

            var prev = _prevTick;
            var now = CompositionNow;
            _prevTick = now;

            switch (_state)
            {
                case State.Idle:
                    break;
                case State.Inertia:
                    if (_velocity.Length < InertialScrollSpeedEnd)
                    {
                        _velocity = new();
                        _acceleration = new();
                        _state = State.Idle;
                        break;
                    }

                    {
                        var deltaT = now - prev;
                        var deltaTSec = deltaT.TotalSeconds;

                        var oldVelocity = _velocity;
                        var deltaVelocity = _acceleration * deltaTSec;
                        Vector3D newVelocity;
                        Vector3D newPosition;
                        if (deltaVelocity.Abs().Length > _velocity.Abs().Length)
                        {
                            newVelocity = new();
                            deltaTSec = Math.Abs(_velocity.Length / _acceleration.Length);
                        }
                        else
                        {
                            newVelocity = _velocity + deltaVelocity;
                        }

                        newPosition = _position + (oldVelocity * deltaTSec) + Vector3D.Divide(_acceleration * deltaTSec * deltaTSec, 2);

                        _velocity = newVelocity;

                        _position = Vector3D.Clamp(newPosition, MinPosition, MaxPosition);
                        if (_position == MaxPosition || _position == MinPosition)
                            _velocity = new();


                    }

                    break;
            }

            if (_position != oldPosition)
            {
                ValuesChanged(_position);
            }
            SetPosition(_position);
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnMessage(object message)
        {
            if (message is Rect bounds)
            {
                if (_targetSize == null)
                    return;

                _targetSize.SetValue(_targetServer, new Vector(bounds.Width, bounds.Height));

                return;
            }

            if (message is not InteractionTrackerRequest request)
                return;

            switch (request.Type)
            {
                case RequestType.AddVelocity:
                    _velocity += request.Value;
                    _acceleration = Vector3D.Multiply(_velocity, PositionInertiaDecayRate) * -1;
                    _state = State.Inertia;
                    break;
                case RequestType.ChangePosition:
                    _position = request.Value;
                    break;
                case RequestType.ChangePositionToIdle:
                    _position = request.Value;
                    _velocity = new();
                    _acceleration = new();
                    _state = State.Idle;
                    break;
                case RequestType.ChangePositionBy:
                    _position += request.Value;
                    break;
                case RequestType.ChangePositionByToIdle:
                    _position += request.Value;
                    _velocity = new();
                    _acceleration = new();
                    _state = State.Idle;
                    break;
            }
        }

        #region SetOffset Hack

        private CompositionVisual _boundsVisual;
        private object _boundsServer;
        private PropertyInfo _boundsOffset;

        public CompositionVisual CompositionBoundsVisual
        {
            get => _boundsVisual;
            set
            {
                if (_boundsVisual != value)
                {
                    _boundsServer = null;
                    _boundsOffset = null;

                    _boundsVisual = value;

                    if (_boundsVisual != null)
                    {
                        var serverProps = typeof(CompositionVisual).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var serverProp = serverProps.First(p => p.Name == "Server");
                        _boundsServer = serverProp.GetValue(_boundsVisual);
                    }

                    if (_boundsServer != null)
                    {
                        _boundsOffset = _boundsServer.GetType().GetProperty("Offset", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    }
                }
            }
        }

        private CompositionVisual _targetVisual;
        private object _targetServer;
        private PropertyInfo _targetOffset;
        private PropertyInfo _targetSize;
        private MethodInfo _targerInvalidate;
        private FieldInfo _combinedTransformDirtyField;

        public CompositionVisual CompositionTargetVisual
        {
            get => _targetVisual;
            set
            {
                if (_targetVisual != value)
                {
                    _targetServer = null;
                    _targetOffset = null;
                    _targerInvalidate = null;

                    _targetVisual = value;

                    if (_targetVisual != null)
                    {
                        var serverProps = typeof(CompositionVisual).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var serverProp = serverProps.First(p => p.Name == "Server");
                        _targetServer = serverProp.GetValue(_targetVisual);
                    }

                    if (_targetServer != null)
                    {
                        _targerInvalidate = _targetServer.GetType().GetMethod("ValuesInvalidated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        _combinedTransformDirtyField = _targetServer.GetType().BaseType.BaseType.GetField("_combinedTransformDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        _targetOffset = _targetServer.GetType().GetProperty("Offset", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        _targetSize = _targetServer.GetType().GetProperty("Size", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                        var targetClibToBounds = _targetServer.GetType().GetProperty("ClipToBounds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        targetClibToBounds.SetValue(_targetServer, false);
                    }
                }
            }
        }

        // Ideally, this stuff can be replaced with a simple ExpressionAnimation.
        // But first, this Tracker must become an independent composition object with its animated Position property.
        // Second, ExpressionAnimation should start working with a changing Offset (issue 12939)
        private void SetPosition(Vector3D position)
        {
            Vector3D boundsOffset = new();
            if (_boundsOffset != null)
                boundsOffset = (Vector3D)_boundsOffset.GetValue(_boundsServer);

            if (_targetOffset != null)
            {
                var newPosition = position + boundsOffset;

                _targetOffset.SetValue(_targetServer, -newPosition);
                _targerInvalidate.Invoke(_targetServer, null);
                _combinedTransformDirtyField.SetValue(_targetServer, true);
            }
        }

        #endregion

        #region Owner

        private long _requestId = 0;

        private void ValuesChanged(Vector3D position)
        {
            var arg = new InteractionTrackerValuesChangedArgs(position);
            var requestId = Interlocked.Increment(ref _requestId);
            Dispatcher.UIThread.InvokeAsync(() => UIValuesChanged(requestId, arg), DispatcherPriority.Background);
        }

        private void UIValuesChanged(long requestId, InteractionTrackerValuesChangedArgs arg)
        {
            var currentRequestId = Interlocked.Read(ref _requestId);
            if (currentRequestId == requestId)
                _owner.ValuesChanged(this, arg);
        }

        #endregion
    }
}