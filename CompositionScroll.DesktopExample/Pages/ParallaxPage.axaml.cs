using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using CompositionScroll.Interactions;

namespace CompositionScroll.DesktopExample.Pages
{
    public partial class ParallaxPage : UserControl
    {
        private ScrollPropertiesSource _scrollSource;

        public ParallaxPage()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            ScrollViewer.PropertyChanged += ScrollViewer_PropertyChanged;
            _scrollSource = (ScrollViewer.Presenter as CompositionScrollContentPresenter).GetScrollPropertiesSource();
            UpdateParallax();
        }

        private void ScrollViewer_PropertyChanged(object sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.ExtentProperty ||
                e.Property == ScrollViewer.ViewportProperty)
            {
                UpdateParallax();
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            UpdateParallax();
        }

        private void UpdateParallax()
        {
            if (_scrollSource == null)
                return;

            var scrollableHeight = ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height;
            if (scrollableHeight <= 0)
            {
                Image.Height = double.NaN;
                return;
            }

            Image.Height = this.Bounds.Height + (scrollableHeight / 2);
            var imageVisual = ElementComposition.GetElementVisual(Image);
            
            var animation = imageVisual.Compositor.CreateExpressionAnimation();
            animation.Expression = "-Source.Position / 2";
            animation.Target = "Offset";
            animation.SetReferenceParameter("Source", _scrollSource);

            var impl = imageVisual.Compositor.CreateImplicitAnimationCollection();
            impl["Offset"] = animation;

            imageVisual.ImplicitAnimations = impl;
            imageVisual.Offset = new Avalonia.Vector3D(1, 0, 0);
        }
    }
}
