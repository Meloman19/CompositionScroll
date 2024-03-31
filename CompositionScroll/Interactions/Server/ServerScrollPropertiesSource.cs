using Avalonia;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using System;
using System.Xml.Linq;

namespace CompositionScroll.Interactions.Server
{
    internal class ServerScrollPropertiesSource : ServerObject, IDisposable
    {
        internal static CompositionProperty IdOfPositionProperty = CompositionProperty.Register();

        private readonly ServerInteractionTracker _tracker;

        private Vector3D _position;

        private ExpressionAnimationInstance _positionAnimation;

        public ServerScrollPropertiesSource(ServerCompositor compositor, ServerInteractionTracker tracker)
            : base(compositor)
        {
            _tracker = tracker;
        }

        public Vector3D Position
        {
            get => GetAnimatedValue(IdOfPositionProperty, ref _position);
            set => SetAnimatedValue(IdOfPositionProperty, out _position, value);
        }

        protected override void DeserializeChangesCore(BatchStreamReader reader, TimeSpan committedAt)
        {
            base.DeserializeChangesCore(reader, committedAt);

            if (IsActive)
                return;

            var expressionString = "Tracker.Position";
            var expression = ExpressionParser.Parse(expressionString.AsSpan());
            var set = new PropertySetSnapshot(new System.Collections.Generic.Dictionary<string, PropertySetSnapshot.Value>
            {
                { "Tracker", new PropertySetSnapshot.Value(_tracker) }
            });
            _positionAnimation = new ExpressionAnimationInstance(expression, this, null, set);

            SetAnimatedValue(IdOfPositionProperty, ref _position, committedAt, _positionAnimation);
            Activate();
        }

        public override ExpressionVariant GetPropertyForAnimation(string name)
        {
            switch (name)
            {
                case nameof(Position):
                    return Position;
            }

            return base.GetPropertyForAnimation(name);
        }

        public override CompositionProperty GetCompositionProperty(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Position):
                    return IdOfPositionProperty;
            }

            return base.GetCompositionProperty(fieldName);
        }

        public void Dispose()
        {
            Deactivate();
        }
    }
}