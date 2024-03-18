using Avalonia;
using System;

namespace CompositionScroll.Interactions.Server
{
    internal sealed partial class ServerInteractionTracker
    {
        private class Moving
        {
            private static readonly double INFLEXION = 0.35f;
            private static readonly double SCROLL_FRICTION = 0.010f;
            private static readonly double PHYSICAL_COEF = 64818f;
            private static readonly double DECELERATION_RATE = Math.Log(0.78) / Math.Log(0.9);

            private readonly ServerInteractionTracker _interactionTracker;
            private bool _isEnded = false;

            private Moving(ServerInteractionTracker interactionTracker)
            {
                _interactionTracker = interactionTracker;
            }

            public Vector3D StartPosition { get; private set; }

            public Vector3D CurrentPosition { get; private set; }

            public Vector3D EndPosition { get; private set; }

            private Vector3D Velocity { get; set; }

            private Vector3D Acceleration { get; set; }

            private double Duration { get; set; }

            public bool IsEnded => _isEnded;

            public int GestureId { get; private set; }

            public bool Gesture { get; private set; }

            public TimeSpan StartTime { get; private set; }

            public void Move(TimeSpan now)
            {
                if (_isEnded)
                    return;

                if (Gesture)
                {
                    return;
                }

                var deltaT = now - StartTime;
                var deltaTSec = deltaT.TotalSeconds;

                Vector3D newCurrentPosition;
                if (deltaTSec >= Duration)
                {
                    newCurrentPosition = EndPosition;
                    _isEnded = true;
                }
                else
                {
                    newCurrentPosition = StartPosition + Velocity * deltaTSec + Acceleration * (deltaTSec * deltaTSec / 2);
                }

                CurrentPosition = Vector3D.Clamp(newCurrentPosition, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

                if (newCurrentPosition != CurrentPosition)
                    _isEnded = true;
            }

            public static Moving Animate(ServerInteractionTracker interactionTracker, TimeSpan duration, Vector3D deltaPosition)
            {
                var startPosition = interactionTracker._position;
                var endPosition = startPosition + deltaPosition;
                var startVelocity = Vector3D.Divide(deltaPosition, 0.5d * duration.TotalSeconds);
                var acceleration = Vector3D.Divide(-startVelocity, duration.TotalSeconds);
                
                return new Moving(interactionTracker)
                {
                    StartPosition = startPosition,
                    CurrentPosition = startPosition,
                    EndPosition = endPosition,
                    Velocity = startVelocity,
                    Acceleration = acceleration,
                    Duration = duration.TotalSeconds,
                    StartTime = interactionTracker.Compositor.ServerNow,
                    Gesture = false
                };
            }

            public static Moving AnimateVelocity(ServerInteractionTracker interactionTracker, Vector3D velocity)
            {
                var startPosition = interactionTracker._position;
                var startVelocity = velocity;
                var durationSec = getSplineFlingDuration(startVelocity);
                var deltaPosition = startVelocity * (durationSec / 2);
                var endPosition = startPosition + deltaPosition;
                var acceleration = Vector3D.Divide(-startVelocity, durationSec);

                return new Moving(interactionTracker)
                {
                    StartPosition = startPosition,
                    CurrentPosition = startPosition,
                    EndPosition = endPosition,
                    Velocity = startVelocity,
                    Acceleration = acceleration,
                    Duration = durationSec,
                    StartTime = interactionTracker.Compositor.ServerNow,
                    Gesture = false
                };
            }

            public static Moving StartGesture(ServerInteractionTracker interactionTracker, int gestureId)
            {
                return new Moving(interactionTracker)
                {
                    StartPosition = interactionTracker.Position,
                    CurrentPosition = interactionTracker.Position,
                    EndPosition = interactionTracker.Position,
                    Gesture = true,
                    GestureId = gestureId
                };
            }

            public Moving AnimateBy(TimeSpan duration, Vector3D deltaPosition)
            {
                var endPosition = EndPosition + deltaPosition;
                var totalDeltaPosition = endPosition - CurrentPosition;
                var velocity = Vector3D.Divide(totalDeltaPosition, 0.5d * duration.TotalSeconds);
                var acceleration = Vector3D.Divide(-velocity, duration.TotalSeconds);

                return new Moving(_interactionTracker)
                {
                    StartPosition = CurrentPosition,
                    CurrentPosition = CurrentPosition,
                    EndPosition = endPosition,
                    Velocity = velocity,
                    Acceleration = acceleration,
                    Duration = duration.TotalSeconds,
                    StartTime = _interactionTracker.Compositor.ServerNow,
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

            public void Delta(Vector3D delta)
            {
                if (!Gesture)
                    return;

                var newCurrentPosition = StartPosition + delta;
                CurrentPosition = Vector3D.Clamp(newCurrentPosition, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
            }

            private static double getSplineDeceleration(double velocity)
                => Math.Log(INFLEXION * Math.Abs(velocity) / (SCROLL_FRICTION * PHYSICAL_COEF));

            private static double getSplineFlingDuration(Vector3D velocity)
            {
                var lenVelocity = velocity.Length;
                var decel = getSplineDeceleration(lenVelocity);
                double decelMinusOne = DECELERATION_RATE - 1.0;
                return Math.Exp(decel / decelMinusOne);
            }
        }
    }
}