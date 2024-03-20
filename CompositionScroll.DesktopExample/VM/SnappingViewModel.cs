using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace CompositionScroll.DesktopExample.VM
{
    public class SnappingViewModel : ObservableObject
    {
        private bool _allowAutoHide;
        private bool _enableInertia;
        private ScrollBarVisibility _horizontalScrollVisibility;
        private ScrollBarVisibility _verticalScrollVisibility;
        private SnapPointsType _snapPointsType;
        private SnapPointsAlignment _snapPointsAlignment;
        private bool _areSnapPointsRegular;

        public SnappingViewModel()
        {
            AvailableVisibility = new List<ScrollBarVisibility>
            {
                ScrollBarVisibility.Auto,
                ScrollBarVisibility.Visible,
                ScrollBarVisibility.Hidden,
                ScrollBarVisibility.Disabled,
            };

            AvailableSnapPointsType = new List<SnapPointsType>()
            {
                SnapPointsType.None,
                SnapPointsType.Mandatory,
                SnapPointsType.MandatorySingle
            };

            AvailableSnapPointsAlignment = new List<SnapPointsAlignment>()
            {
                SnapPointsAlignment.Near,
                SnapPointsAlignment.Center,
                SnapPointsAlignment.Far,
            };

            HorizontalScrollVisibility = ScrollBarVisibility.Auto;
            VerticalScrollVisibility = ScrollBarVisibility.Auto;
            AllowAutoHide = true;
            EnableInertia = true;
        }

        public bool AllowAutoHide
        {
            get => _allowAutoHide;
            set => SetProperty(ref _allowAutoHide, value);
        }

        public bool EnableInertia
        {
            get => _enableInertia;
            set => SetProperty(ref _enableInertia, value);
        }

        public ScrollBarVisibility HorizontalScrollVisibility
        {
            get => _horizontalScrollVisibility;
            set => SetProperty(ref _horizontalScrollVisibility, value);
        }

        public ScrollBarVisibility VerticalScrollVisibility
        {
            get => _verticalScrollVisibility;
            set => SetProperty(ref _verticalScrollVisibility, value);
        }

        public List<ScrollBarVisibility> AvailableVisibility { get; }

        public bool AreSnapPointsRegular
        {
            get => _areSnapPointsRegular;
            set => SetProperty(ref _areSnapPointsRegular, value);
        }

        public SnapPointsType SnapPointsType
        {
            get => _snapPointsType;
            set => SetProperty(ref _snapPointsType, value);
        }

        public SnapPointsAlignment SnapPointsAlignment
        {
            get => _snapPointsAlignment;
            set => SetProperty(ref _snapPointsAlignment, value);
        }
        public List<SnapPointsType> AvailableSnapPointsType { get; }
        public List<SnapPointsAlignment> AvailableSnapPointsAlignment { get; }
    }
}