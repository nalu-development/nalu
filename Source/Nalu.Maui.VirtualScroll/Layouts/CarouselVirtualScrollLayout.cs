namespace Nalu;

/// <summary>
/// A carousel layout for virtual scroll that arranges items to fill the available space and applies paging snapping behavior.
/// </summary>
public class CarouselVirtualScrollLayout : VirtualScrollLayout
{
    private static readonly BindableProperty _carouselVirtualScrollLayoutControllerProperty =
        BindableProperty.CreateAttached(
            "_carouselVirtualScrollLayoutController",
            typeof(CarouselVirtualScrollLayoutController),
            typeof(CarouselVirtualScrollLayout),
            null,
            defaultValueCreator: bindable =>
            {
                var virtualScroll = bindable as VirtualScroll ?? throw new InvalidOperationException("Carousel attached properties can only be set on VirtualScroll.");
                return new CarouselVirtualScrollLayoutController(virtualScroll);
            }
        );
    
    private static CarouselVirtualScrollLayoutController GetCarouselVirtualScrollLayoutController(BindableObject bindable)
        => (CarouselVirtualScrollLayoutController)bindable.GetValue(_carouselVirtualScrollLayoutControllerProperty);

    /// <summary>
    /// Bindable property for the current visible item in the carousel.
    /// </summary>
    public static readonly BindableProperty CurrentRangeProperty =
        BindableProperty.CreateAttached(
            "CurrentRange",
            typeof(VirtualScrollRange),
            typeof(CarouselVirtualScrollLayout),
            null,
            defaultValueCreator: bindable =>
            {
                GetCarouselVirtualScrollLayoutController(bindable);
                return new VirtualScrollRange(0, 0, 0, 0);
            },
            propertyChanged: (bindable, _, newValue) =>
            {
                var controller = GetCarouselVirtualScrollLayoutController(bindable);
                controller.UpdateCurrentRange((VirtualScrollRange)newValue);
            },
            defaultBindingMode: BindingMode.TwoWay
        );

    /// <summary>
    /// Gets the current visible item in the carousel.
    /// </summary>
    public static VirtualScrollRange GetCurrentRange(BindableObject bindable) => (VirtualScrollRange) bindable.GetValue(CurrentRangeProperty);

    /// <summary>
    /// Sets the current visible item in the carousel.
    /// </summary>
    public static void SetCurrentRange(BindableObject bindable, VirtualScrollRange value)
        => bindable.SetValue(CurrentRangeProperty, value);

    /// <summary>
    /// Initializes a new instance of the <see cref="CarouselVirtualScrollLayout" /> class.
    /// </summary>
    /// <param name="orientation">The orientation of the layout.</param>
    protected CarouselVirtualScrollLayout(ItemsLayoutOrientation orientation)
        : base(orientation)
    {
    }

    private class CarouselVirtualScrollLayoutController
    {
        private readonly VirtualScroll _virtualScroll;
        private bool _updatingIndex;
        private VirtualScrollRange? _deferredRange;

        public CarouselVirtualScrollLayoutController(VirtualScroll virtualScroll)
        {
            _virtualScroll = virtualScroll;
            virtualScroll.OnScrollEnded += VirtualScroll_OnScrollEnded;
        }

        private void VirtualScroll_OnScrollEnded(object? sender, VirtualScrollScrolledEventArgs e)
        {
            if (_virtualScroll.ItemsLayout is not CarouselVirtualScrollLayout)
            {
                return;
            }

            var itemsRange = _virtualScroll.GetVisibleItemsRange();

            _updatingIndex = true;
            _virtualScroll.SetValue(CurrentRangeProperty, itemsRange ?? CurrentRangeProperty.DefaultValue);
            _updatingIndex = false;
        }

        public void UpdateCurrentRange(VirtualScrollRange range)
        {
            if (_updatingIndex)
            {
                return;
            }

            if (!_virtualScroll.IsLoaded)
            {
                _deferredRange = range;
                _virtualScroll.Loaded -= OnVirtualScrollLoaded;
                _virtualScroll.Loaded += OnVirtualScrollLoaded;
                return;
            }

            _virtualScroll.ScrollTo(range.StartSectionIndex, range.EndItemIndex);
        }

#pragma warning disable VSTHRD100
        private async void OnVirtualScrollLoaded(object? sender, EventArgs e)
#pragma warning restore VSTHRD100
        {
            _virtualScroll.Loaded -= OnVirtualScrollLoaded;
            
            if (_deferredRange is { } range)
            {
                await Task.Yield();
                _virtualScroll.ScrollTo(range.StartSectionIndex, range.EndItemIndex, animated: false);
                _deferredRange = null;
            }
        }
    }
}
