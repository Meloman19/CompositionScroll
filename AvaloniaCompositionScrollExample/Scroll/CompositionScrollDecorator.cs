using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvaloniaCompositionScrollExample.Scroll
{
    public sealed class CompositionScrollDecorator : Control, ILogicalScrollable, IScrollAnchorProvider, IInteractionTrackerOwner
    {
        public static readonly Easing ScrollEasing = new CubicEaseOut();
        public static readonly TimeSpan ScrollDuration = TimeSpan.FromMilliseconds(300);

        private const double DefaultSmallChange = 16;
        private const double EdgeDetectionTolerance = 0.1;

        #region ILogicalScrollable StyledProperty

        public static readonly StyledProperty<bool> CanHorizontallyScrollProperty =
            AvaloniaProperty.Register<CompositionScrollDecorator, bool>(nameof(CanHorizontallyScroll));

        public static readonly StyledProperty<bool> CanVerticallyScrollProperty =
            AvaloniaProperty.Register<CompositionScrollDecorator, bool>(nameof(CanVerticallyScroll));

        public static readonly DirectProperty<CompositionScrollDecorator, Size> ExtentProperty =
            ScrollViewer.ExtentProperty.AddOwner<CompositionScrollDecorator>(
                o => o.Extent);

        public static readonly StyledProperty<Vector> OffsetProperty =
           ScrollViewer.OffsetProperty.AddOwner<CompositionScrollDecorator>(new(coerce: CoerceOffset));

        public static readonly DirectProperty<CompositionScrollDecorator, Size> ViewportProperty =
            ScrollViewer.ViewportProperty.AddOwner<CompositionScrollDecorator>(
                o => o.Viewport);

        #endregion

        public static readonly StyledProperty<SnapPointsType> HorizontalSnapPointsTypeProperty =
            ScrollViewer.HorizontalSnapPointsTypeProperty.AddOwner<CompositionScrollDecorator>();

        public static readonly StyledProperty<SnapPointsType> VerticalSnapPointsTypeProperty =
           ScrollViewer.VerticalSnapPointsTypeProperty.AddOwner<CompositionScrollDecorator>();

        public static readonly StyledProperty<SnapPointsAlignment> HorizontalSnapPointsAlignmentProperty =
            ScrollViewer.HorizontalSnapPointsAlignmentProperty.AddOwner<CompositionScrollDecorator>();

        public static readonly StyledProperty<SnapPointsAlignment> VerticalSnapPointsAlignmentProperty =
            ScrollViewer.VerticalSnapPointsAlignmentProperty.AddOwner<CompositionScrollDecorator>();

        public static readonly StyledProperty<Control> ChildProperty =
            AvaloniaProperty.Register<CompositionScrollDecorator, Control>(nameof(Child));

        static CompositionScrollDecorator()
        {
            AffectsMeasure<Decorator>(ChildProperty);
            ChildProperty.Changed.AddClassHandler<CompositionScrollDecorator>((x, e) => x.ChildChanged(e));

            OffsetProperty.Changed.AddClassHandler<CompositionScrollDecorator>((x, e) => x.OffsetChanged(e));
        }

        private readonly ScrollProxy _scrollProxy;

        private bool _arranging;
        private bool _compositionUpdate;
        private Size _extent;
        private Size _viewport;
        private bool _isAnchorElementDirty;

        private Dictionary<int, Vector> _activeLogicalGestureScrolls;
        private Dictionary<int, Vector> _scrollGestureSnapPoints;
        private HashSet<Control> _anchorCandidates;
        private Control _anchorElement;
        private Rect _anchorElementBounds;
        private bool _areVerticalSnapPointsRegular;
        private bool _areHorizontalSnapPointsRegular;
        private IReadOnlyList<double> _horizontalSnapPoints;
        private double _horizontalSnapPoint;
        private IReadOnlyList<double> _verticalSnapPoints;
        private double _verticalSnapPoint;
        private double _verticalSnapPointOffset;
        private double _horizontalSnapPointOffset;
        private ScrollViewer _owner;
        private IScrollSnapPointsInfo _scrollSnapPointsInfo;
        private bool _isSnapPointsUpdated;

        private Size _childDesiredSize;

        public CompositionScrollDecorator()
        {
            _scrollProxy = HackClassBuilder.CreateClass(this);
            VisualChildren.Add(_scrollProxy);
        }

        #region ILogicalScrollable Property

        public bool CanHorizontallyScroll
        {
            get => GetValue(CanHorizontallyScrollProperty);
            set => SetValue(CanHorizontallyScrollProperty, value);
        }

        public bool CanVerticallyScroll
        {
            get => GetValue(CanVerticallyScrollProperty);
            set => SetValue(CanVerticallyScrollProperty, value);
        }

        public Size Extent
        {
            get { return _extent; }
            private set
            {
                if (SetAndRaise(ExtentProperty, ref _extent, value))
                {
                    CoerceValue(OffsetProperty);
                    RaiseScrollInvalidated();
                }
            }
        }

        public Vector Offset
        {
            get => GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }

        public Size Viewport
        {
            get { return _viewport; }
            private set
            {
                if (SetAndRaise(ViewportProperty, ref _viewport, value))
                {
                    CoerceValue(OffsetProperty);
                    RaiseScrollInvalidated();
                }
            }
        }

        public bool IsLogicalScrollEnabled => true;

        public Size ScrollSize => new Size(16, 16);

        public Size PageScrollSize => new Size(16, 16);

        #endregion

        public SnapPointsType HorizontalSnapPointsType
        {
            get => GetValue(HorizontalSnapPointsTypeProperty);
            set => SetValue(HorizontalSnapPointsTypeProperty, value);
        }

        public SnapPointsType VerticalSnapPointsType
        {
            get => GetValue(VerticalSnapPointsTypeProperty);
            set => SetValue(VerticalSnapPointsTypeProperty, value);
        }

        public SnapPointsAlignment HorizontalSnapPointsAlignment
        {
            get => GetValue(HorizontalSnapPointsAlignmentProperty);
            set => SetValue(HorizontalSnapPointsAlignmentProperty, value);
        }

        public SnapPointsAlignment VerticalSnapPointsAlignment
        {
            get => GetValue(VerticalSnapPointsAlignmentProperty);
            set => SetValue(VerticalSnapPointsAlignmentProperty, value);
        }

        [Content]
        public Control Child
        {
            get { return GetValue(ChildProperty); }
            set { SetValue(ChildProperty, value); }
        }

        Control IScrollAnchorProvider.CurrentAnchor
        {
            get
            {
                EnsureAnchorElementSelection();
                return _anchorElement;
            }
        }

        private void OffsetChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_arranging)
            {
                InvalidateArrange();
            }

            if (!_compositionUpdate)
            {
                var offset = e.GetNewValue<Vector>();
                _scrollProxy.TryUpdatePosition(new Vector3D(offset.X, offset.Y, 0), true);
            }

            RaiseScrollInvalidated();
        }

        void IScrollAnchorProvider.RegisterAnchorCandidate(Control element)
        {
            if (!this.IsVisualAncestorOf(element))
            {
                throw new InvalidOperationException(
                    "An anchor control must be a visual descendent of the ScrollContentPresenter.");
            }

            _anchorCandidates ??= new();
            _anchorCandidates.Add(element);
            _isAnchorElementDirty = true;
        }

        void IScrollAnchorProvider.UnregisterAnchorCandidate(Control element)
        {
            _anchorCandidates?.Remove(element);
            _isAnchorElementDirty = true;

            if (_anchorElement == element)
            {
                _anchorElement = null;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var constraint = new Size(
                CanHorizontallyScroll ? double.PositiveInfinity : availableSize.Width,
                CanVerticallyScroll ? double.PositiveInfinity : availableSize.Height);

            _scrollProxy.Measure(constraint);
            _childDesiredSize = Child.DesiredSize;

            return Child.DesiredSize.Constrain(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = new Size(
                CanHorizontallyScroll ? Math.Max(Child!.DesiredSize.Width, finalSize.Width) : finalSize.Width,
                CanVerticallyScroll ? Math.Max(Child!.DesiredSize.Height, finalSize.Height) : finalSize.Height);

            Vector TrackAnchor()
            {
                // If we have an anchor and its position relative to Child has changed during the
                // arrange then that change wasn't just due to scrolling (as scrolling doesn't adjust
                // relative positions within Child).
                if (_anchorElement != null &&
                    TranslateBounds(_anchorElement, Child!, out var updatedBounds) &&
                    updatedBounds.Position != _anchorElementBounds.Position)
                {
                    var offset = updatedBounds.Position - _anchorElementBounds.Position;
                    return offset;
                }

                return default;
            }

            var isAnchoring = Offset.X >= EdgeDetectionTolerance || Offset.Y >= EdgeDetectionTolerance;

            if (isAnchoring)
            {
                // Calculate the new anchor element if necessary.
                EnsureAnchorElementSelection();

                // Do the arrange.
                _scrollProxy.Arrange(new Rect(new Point(-Offset.X, -Offset.Y), size));

                // If the anchor moved during the arrange, we need to adjust the offset and do another arrange.
                var anchorShift = TrackAnchor();

                if (anchorShift != default)
                {
                    var newOffset = Offset + anchorShift;
                    var newExtent = Extent;
                    var maxOffset = new Vector(Extent.Width - Viewport.Width, Extent.Height - Viewport.Height);

                    if (newOffset.X > maxOffset.X)
                    {
                        newExtent = newExtent.WithWidth(newOffset.X + Viewport.Width);
                    }

                    if (newOffset.Y > maxOffset.Y)
                    {
                        newExtent = newExtent.WithHeight(newOffset.Y + Viewport.Height);
                    }

                    Extent = newExtent;

                    try
                    {
                        _arranging = true;
                        _compositionUpdate = true;
                        _scrollProxy.TryUpdatePositionBy(new Vector3D(anchorShift.X, anchorShift.Y, 0), false);
                        SetCurrentValue(OffsetProperty, newOffset);
                    }
                    finally
                    {
                        _arranging = false;
                        _compositionUpdate = false;
                    }

                    _scrollProxy.Arrange(new Rect(new Point(-Offset.X, -Offset.Y), size));
                }
            }
            else
            {
                _scrollProxy.Arrange(new Rect(new Point(-Offset.X, -Offset.Y), size));
            }

            Viewport = finalSize;
            Extent = ComputeExtent(finalSize);

            var scrollableHeight = Extent.Height - Viewport.Height;
            _scrollProxy.SetMaxPosition(new Vector3D(0, (float)scrollableHeight, 0));
            _isAnchorElementDirty = true;

            return finalSize;
        }

        private Size ComputeExtent(Size viewportSize)
        {
            var childMargin = Child!.Margin;

            if (Child.UseLayoutRounding)
            {
                var scale = LayoutHelper.GetLayoutScale(Child);
                childMargin = LayoutHelper.RoundLayoutThickness(childMargin, scale, scale);
            }

            var extent = Child!.Bounds.Size.Inflate(childMargin);

            if (MathUtilities.AreClose(extent.Width, viewportSize.Width, LayoutHelper.LayoutEpsilon))
                extent = extent.WithWidth(viewportSize.Width);

            if (MathUtilities.AreClose(extent.Height, viewportSize.Height, LayoutHelper.LayoutEpsilon))
                extent = extent.WithHeight(viewportSize.Height);

            return extent;
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(new SolidColorBrush(Colors.Transparent), Bounds);
            base.Render(context);
        }

        private void ChildChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var oldChild = (Control)e.OldValue;
            var newChild = (Control)e.NewValue;

            if (oldChild != null)
            {
                ((ISetLogicalParent)oldChild).SetParent(null);
                LogicalChildren.Clear();
                _scrollProxy.VisualChild = null;
            }

            if (newChild != null)
            {
                ((ISetLogicalParent)newChild).SetParent(this);
                _scrollProxy.VisualChild = newChild;
                LogicalChildren.Add(newChild);
            }
        }

        #region ILogicalScrollable Methods

        public bool BringIntoView(Control target, Rect targetRect)
        {
            return false;
        }

        public Control GetControlInDirection(NavigationDirection direction, Control from)
        {
            return null;
        }

        #endregion

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (Extent.Height > Viewport.Height || Extent.Width > Viewport.Width)
            {
                var x = Offset.X;
                var y = Offset.Y;
                var delta = e.Delta;

                // KeyModifiers.Shift should scroll in horizontal direction. This does not work on every platform. 
                // If Shift-Key is pressed and X is close to 0 we swap the Vector.
                if (e.KeyModifiers == KeyModifiers.Shift && MathUtilities.IsZero(delta.X))
                {
                    delta = new Vector(delta.Y, delta.X);
                }

                if (Extent.Height > Viewport.Height)
                {
                    double height = 50;
                    y += -delta.Y * height;
                    y = Math.Max(y, 0);
                    y = Math.Min(y, Extent.Height - Viewport.Height);
                }

                if (Extent.Width > Viewport.Width)
                {
                    double width = 50;
                    x += -delta.X * width;
                    x = Math.Max(x, 0);
                    x = Math.Min(x, Extent.Width - Viewport.Width);
                }

                Vector newOffset = SnapOffset(new Vector(x, y));

                var deltaVelocity = 600 * e.Delta;
                _scrollProxy.TryUpdatePositionWithAdditionalVelocity(new Vector3D(-deltaVelocity.X, -deltaVelocity.Y, 0));
            }
            e.Handled = true;
        }

        private void ScrollSnapPointsInfoSnapPointsChanged(object sender, RoutedEventArgs e)
        {
            UpdateSnapPoints();
        }

        private void EnsureAnchorElementSelection()
        {
            if (!_isAnchorElementDirty || _anchorCandidates is null)
            {
                return;
            }

            _anchorElement = null;
            _anchorElementBounds = default;
            _isAnchorElementDirty = false;

            var bestCandidate = default(Control);
            var bestCandidateDistance = double.MaxValue;

            // Find the anchor candidate that is scrolled closest to the top-left of this
            // ScrollContentPresenter.
            foreach (var element in _anchorCandidates)
            {
                if (element.IsVisible && GetViewportBounds(element, out var bounds))
                {
                    var distance = (Vector)bounds.Position;
                    var candidateDistance = Math.Abs(distance.Length);

                    if (candidateDistance < bestCandidateDistance)
                    {
                        bestCandidate = element;
                        bestCandidateDistance = candidateDistance;
                    }
                }
            }

            if (bestCandidate != null)
            {
                // We have a candidate, calculate its bounds relative to Child. Because these
                // bounds aren't relative to the ScrollContentPresenter itself, if they change
                // then we know it wasn't just due to scrolling.
                var unscrolledBounds = TranslateBounds(bestCandidate, Child!);
                _anchorElement = bestCandidate;
                _anchorElementBounds = unscrolledBounds;
            }
        }

        private bool GetViewportBounds(Control element, out Rect bounds)
        {
            if (TranslateBounds(element, Child!, out var childBounds))
            {
                // We want the bounds relative to the new Offset, regardless of whether the child
                // control has actually been arranged to this offset yet, so translate first to the
                // child control and then apply Offset rather than translating directly to this
                // control.
                var thisBounds = new Rect(Bounds.Size);
                bounds = new Rect(childBounds.Position - Offset, childBounds.Size);
                return bounds.Intersects(thisBounds);
            }

            bounds = default;
            return false;
        }

        private Rect TranslateBounds(Control control, Control to)
        {
            if (TranslateBounds(control, to, out var bounds))
            {
                return bounds;
            }

            throw new InvalidOperationException("The control's bounds could not be translated to the requested control.");
        }

        private bool TranslateBounds(Control control, Control to, out Rect bounds)
        {
            if (!control.IsVisible)
            {
                bounds = default;
                return false;
            }

            var p = control.TranslatePoint(default, to);
            bounds = p.HasValue ? new Rect(p.Value, control.Bounds.Size) : default;
            return p.HasValue;
        }

        private void UpdateSnapPoints()
        {
            var scrollable = GetScrollSnapPointsInfo(Child);

            if (scrollable is IScrollSnapPointsInfo scrollSnapPointsInfo)
            {
                _areVerticalSnapPointsRegular = scrollSnapPointsInfo.AreVerticalSnapPointsRegular;
                _areHorizontalSnapPointsRegular = scrollSnapPointsInfo.AreHorizontalSnapPointsRegular;

                if (!_areVerticalSnapPointsRegular)
                {
                    _verticalSnapPoints = scrollSnapPointsInfo.GetIrregularSnapPoints(Orientation.Vertical, VerticalSnapPointsAlignment);
                }
                else
                {
                    _verticalSnapPoints = new List<double>();
                    _verticalSnapPoint = scrollSnapPointsInfo.GetRegularSnapPoints(Orientation.Vertical, VerticalSnapPointsAlignment, out _verticalSnapPointOffset);

                }

                if (!_areHorizontalSnapPointsRegular)
                {
                    _horizontalSnapPoints = scrollSnapPointsInfo.GetIrregularSnapPoints(Orientation.Horizontal, HorizontalSnapPointsAlignment);
                }
                else
                {
                    _horizontalSnapPoints = new List<double>();
                    _horizontalSnapPoint = scrollSnapPointsInfo.GetRegularSnapPoints(Orientation.Vertical, VerticalSnapPointsAlignment, out _horizontalSnapPointOffset);
                }
            }
            else
            {
                _horizontalSnapPoints = new List<double>();
                _verticalSnapPoints = new List<double>();
            }
        }

        private Vector SnapOffset(Vector offset)
        {
            var scrollable = GetScrollSnapPointsInfo(Child);

            if (scrollable is null)
                return offset;

            var diff = GetAlignedDiff();

            if (VerticalSnapPointsType != SnapPointsType.None)
            {
                offset = new Vector(offset.X, offset.Y + diff.Y);
                double nearestSnapPoint = offset.Y;

                if (_areVerticalSnapPointsRegular)
                {
                    var minSnapPoint = (int)(offset.Y / _verticalSnapPoint) * _verticalSnapPoint + _verticalSnapPointOffset;
                    var maxSnapPoint = minSnapPoint + _verticalSnapPoint;
                    var midPoint = (minSnapPoint + maxSnapPoint) / 2;

                    nearestSnapPoint = offset.Y < midPoint ? minSnapPoint : maxSnapPoint;
                }
                else if (_verticalSnapPoints != null && _verticalSnapPoints.Count > 0)
                {
                    var higherSnapPoint = FindNearestSnapPoint(_verticalSnapPoints, offset.Y, out var lowerSnapPoint);
                    var midPoint = (lowerSnapPoint + higherSnapPoint) / 2;

                    nearestSnapPoint = offset.Y < midPoint ? lowerSnapPoint : higherSnapPoint;
                }

                offset = new Vector(offset.X, nearestSnapPoint - diff.Y);
            }

            if (HorizontalSnapPointsType != SnapPointsType.None)
            {
                offset = new Vector(offset.X + diff.X, offset.Y);
                double nearestSnapPoint = offset.X;

                if (_areHorizontalSnapPointsRegular)
                {
                    var minSnapPoint = (int)(offset.X / _horizontalSnapPoint) * _horizontalSnapPoint + _horizontalSnapPointOffset;
                    var maxSnapPoint = minSnapPoint + _horizontalSnapPoint;
                    var midPoint = (minSnapPoint + maxSnapPoint) / 2;

                    nearestSnapPoint = offset.X < midPoint ? minSnapPoint : maxSnapPoint;
                }
                else if (_horizontalSnapPoints != null && _horizontalSnapPoints.Count > 0)
                {
                    var higherSnapPoint = FindNearestSnapPoint(_horizontalSnapPoints, offset.X, out var lowerSnapPoint);
                    var midPoint = (lowerSnapPoint + higherSnapPoint) / 2;

                    nearestSnapPoint = offset.X < midPoint ? lowerSnapPoint : higherSnapPoint;
                }

                offset = new Vector(nearestSnapPoint - diff.X, offset.Y);

            }

            Vector GetAlignedDiff()
            {
                var vector = offset;

                switch (VerticalSnapPointsAlignment)
                {
                    case SnapPointsAlignment.Center:
                        vector += new Vector(0, Viewport.Height / 2);
                        break;
                    case SnapPointsAlignment.Far:
                        vector += new Vector(0, Viewport.Height);
                        break;
                }

                switch (HorizontalSnapPointsAlignment)
                {
                    case SnapPointsAlignment.Center:
                        vector += new Vector(Viewport.Width / 2, 0);
                        break;
                    case SnapPointsAlignment.Far:
                        vector += new Vector(Viewport.Width, 0);
                        break;
                }

                return vector - offset;
            }

            return offset;
        }

        private static double FindNearestSnapPoint(IReadOnlyList<double> snapPoints, double value, out double lowerSnapPoint)
        {
            var point = snapPoints.BinarySearch(value, Comparer<double>.Default);

            if (point < 0)
            {
                point = ~point;

                lowerSnapPoint = snapPoints[Math.Max(0, point - 1)];
            }
            else
            {
                lowerSnapPoint = snapPoints[point];

                point += 1;
            }
            return snapPoints[Math.Min(point, snapPoints.Count - 1)];
        }

        private IScrollSnapPointsInfo GetScrollSnapPointsInfo(object content)
        {
            var scrollable = content;

            if (Child is ItemsControl itemsControl)
                scrollable = itemsControl.Presenter?.Panel;

            if (Child is ItemsPresenter itemsPresenter)
                scrollable = itemsPresenter.Panel;

            var snapPointsInfo = scrollable as IScrollSnapPointsInfo;

            if (snapPointsInfo != _scrollSnapPointsInfo)
            {
                if (_scrollSnapPointsInfo != null)
                {
                    _scrollSnapPointsInfo.VerticalSnapPointsChanged -= ScrollSnapPointsInfoSnapPointsChanged;
                    _scrollSnapPointsInfo.HorizontalSnapPointsChanged -= ScrollSnapPointsInfoSnapPointsChanged;
                }

                _scrollSnapPointsInfo = snapPointsInfo;

                if (_scrollSnapPointsInfo != null)
                {
                    _scrollSnapPointsInfo.VerticalSnapPointsChanged += ScrollSnapPointsInfoSnapPointsChanged;
                    _scrollSnapPointsInfo.HorizontalSnapPointsChanged += ScrollSnapPointsInfoSnapPointsChanged;
                }
            }

            return snapPointsInfo;
        }

        #region Static

        internal static Vector CoerceOffset(AvaloniaObject sender, Vector value)
        {
            var extent = sender.GetValue(ExtentProperty);
            var viewport = sender.GetValue(ViewportProperty);

            var maxX = Math.Max(extent.Width - viewport.Width, 0);
            var maxY = Math.Max(extent.Height - viewport.Height, 0);
            return new Vector(Clamp(value.X, 0, maxX), Clamp(value.Y, 0, maxY));
        }

        private static double Clamp(double value, double min, double max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        #endregion

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            try
            {
                _compositionUpdate = true;
                SetCurrentValue(OffsetProperty, new Vector(args.Position.X, args.Position.Y));
            }
            finally
            {
                _compositionUpdate = false;
            }
        }

        private void RaiseScrollInvalidated()
        {
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
        {
            ScrollInvalidated?.Invoke(this, e);
        }

        public event EventHandler ScrollInvalidated;
    }
}