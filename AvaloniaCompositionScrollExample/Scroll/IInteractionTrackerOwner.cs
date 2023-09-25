using Avalonia;

namespace AvaloniaCompositionScrollExample.Scroll
{
    public interface IInteractionTrackerOwner
    {
        void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args);
    }

    public sealed class InteractionTrackerValuesChangedArgs
    {
        public InteractionTrackerValuesChangedArgs(Vector3D position)
        {
            Position = position;
        }

        public Vector3D Position { get; }
    }
}