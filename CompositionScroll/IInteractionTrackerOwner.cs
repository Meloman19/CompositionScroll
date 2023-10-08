using Avalonia;

namespace CompositionScroll
{
    public interface IInteractionTrackerOwner
    {
        void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args);
    }

    public sealed class InteractionTrackerValuesChangedArgs
    {
        public InteractionTrackerValuesChangedArgs(Vector3D position, long requestId)
        {
            Position = position;
            RequestId = requestId;
        }

        public Vector3D Position { get; }

        public long RequestId { get; }
    }
}