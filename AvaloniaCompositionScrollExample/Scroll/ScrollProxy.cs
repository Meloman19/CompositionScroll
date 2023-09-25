using Avalonia;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;

namespace AvaloniaCompositionScrollExample.Scroll
{
    internal class ScrollProxy : InputElement
    {
        private readonly CompositionScrollDecorator _scrollDecorator;

        private Visual _visualChild;
        private Point _arrangeOffset;

        public ScrollProxy(CompositionScrollDecorator scrollDecorator)
        {
            _scrollDecorator = scrollDecorator;
            ClipToBounds = false;
        }

        public Visual VisualChild
        {
            get => _visualChild;
            set
            {
                if (_visualChild != value)
                {
                    if (_visualChild != null)
                    {
                        _visualChild.AttachedToVisualTree -= VisualChild_AttachedToVisualTree;
                        VisualChildren.Remove(_visualChild);
                    }

                    _visualChild = value;

                    if (_visualChild != null)
                    {
                        _visualChild.AttachedToVisualTree += VisualChild_AttachedToVisualTree;
                        VisualChildren.Add(_visualChild);
                    }
                }
            }
        }

        public InteractionTracker InteractionTracker { get; private set; }

        public CompositionCustomVisual InteractionTrackerVisual { get; private set; }

        private CompositionVisual CompositionVisual => ElementComposition.GetElementVisual(this);

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            CompositionVisual.ClipToBounds = false;

            InteractionTracker = new InteractionTracker(_scrollDecorator);
            InteractionTrackerVisual = CompositionVisual.Compositor.CreateCustomVisual(InteractionTracker);
            InteractionTrackerVisual.Size = new Vector(1, 1);
            ElementComposition.SetElementChildVisual(this, InteractionTrackerVisual);

            InteractionTracker.CompositionTargetVisual = CompositionVisual;

            UpdateSize();
        }

        private void VisualChild_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            var compositionVisual = ElementComposition.GetElementVisual(sender as Visual);
            InteractionTracker.CompositionBoundsVisual = compositionVisual;
        }

        protected override void ArrangeCore(Rect finalRect)
        {
            _arrangeOffset = finalRect.TopLeft;
            base.ArrangeCore(new Rect(finalRect.Size));
            UpdateSize();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var arrangeRect = new Rect(_arrangeOffset, finalSize);

            var visualChildren = VisualChildren;
            var visualCount = visualChildren.Count;

            for (var i = 0; i < visualCount; i++)
            {
                Visual visual = visualChildren[i];

                if (visual is Layoutable layoutable)
                {
                    layoutable.Arrange(arrangeRect);
                }
            }

            return finalSize;
        }

        private void UpdateSize()
        {
            var interaction = InteractionTrackerVisual;
            if (interaction == null)
                return;

            // Changing Size directly to ServerComposition through Tracker. 
            interaction.SendHandlerMessage(Bounds);
        }

        public bool TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond)
        {
            var interaction = InteractionTrackerVisual;
            if (interaction == null)
                return false;

            interaction.SendHandlerMessage(new InteractionTrackerRequest(RequestType.AddVelocity, velocityInPixelsPerSecond));
            return true;
        }

        public bool TryUpdatePositionBy(Vector3D deltaPosition, bool resetInertia)
        {
            var interaction = InteractionTrackerVisual;
            if (interaction == null)
                return false;

            if (resetInertia)
                interaction.SendHandlerMessage(new InteractionTrackerRequest(RequestType.ChangePositionByToIdle, deltaPosition));
            else
                interaction.SendHandlerMessage(new InteractionTrackerRequest(RequestType.ChangePositionBy, deltaPosition));

            return true;
        }

        public bool TryUpdatePosition(Vector3D newPosition, bool resetInertia)
        {
            var interaction = InteractionTrackerVisual;
            if (interaction == null)
                return false;

            if (resetInertia)
                interaction.SendHandlerMessage(new InteractionTrackerRequest(RequestType.ChangePositionToIdle, newPosition));
            else
                interaction.SendHandlerMessage(new InteractionTrackerRequest(RequestType.ChangePosition, newPosition));

            return true;
        }

        public void SetMaxPosition(Vector3D maxPosition)
        {
            var interaction = InteractionTracker;
            if (interaction == null)
                return;

            interaction.MaxPosition = maxPosition;
        }
    }
}