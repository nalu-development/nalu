using System.Collections;

namespace Nalu;

/// <summary>
/// A paged view that snaps to one child at a time leveraging <see cref="ScrollView"/> and <see cref="StackLayout"/> under the hood.
/// </summary>
public class SlideViewBox : View, IList<IView>, IViewBox
{
    /// <summary>
    /// Bindable property for <see cref="Orientation" />.
    /// </summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(StackOrientation),
        typeof(SlideViewBox),
        StackOrientation.Horizontal,
        propertyChanged: OnOrientationPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="SelectedIndex" />.
    /// </summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SlideViewBox),
        0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnSelectedIndexPropertyChanged
    );

    private readonly SlideScrollView _scrollView;
    private readonly StackLayout _stackLayout;
    private readonly ViewBoxLayoutManager _layoutManager;
    private IList<IView> SlideHolders => _stackLayout;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlideViewBox" /> class.
    /// </summary>
    public SlideViewBox()
    {
        _stackLayout = new StackLayout { Orientation = StackOrientation.Horizontal };
        _layoutManager = new ViewBoxLayoutManager(this);
        _scrollView = new SlideScrollView
                      {
                          Content = _stackLayout,
                          Orientation = ScrollOrientation.Horizontal,
                      };

        _scrollView.DraggingEnded += OnDraggingEnded;
    }

    private void OnDraggingEnded(object? sender, EventArgs e)
    {
        
    }

    /// <summary>
    /// Gets or sets the slide orientation.
    /// </summary>
    public StackOrientation Orientation
    {
        get => (StackOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected slide index.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private void OnOrientationChanged()
    {
        var slideViewOrientation = Orientation;
        _stackLayout.Orientation = slideViewOrientation;
        _scrollView.Orientation = slideViewOrientation == StackOrientation.Horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
    }

    private void OnSelectedIndexChanged(int _)
    {
        // var horizontal = Orientation == StackOrientation.Horizontal;
        // var targetScrollOffset = targetIndex * (horizontal ? _scrollView.Width : _scrollView.Height);
        // var targetScrollX = horizontal ? targetScrollOffset : 0;
        // var targetScrollY = horizontal ? 0 : targetScrollOffset;
        // _ = _scrollView.ScrollToAsync(targetScrollX, targetScrollY, true);
    }

    private static void OnOrientationPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((SlideViewBox)bindable).OnOrientationChanged();

    private static void OnSelectedIndexPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((SlideViewBox)bindable).OnSelectedIndexChanged((int)newValue!);

    /// <inheritdoc />
    public IEnumerator<IView> GetEnumerator() => SlideHolders.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)SlideHolders).GetEnumerator();

    private IView GetWrappedView(IView view) => ((SlideHolder) view).Content!;

    /// <inheritdoc />
    public void Add(IView item) => SlideHolders.Add(
        new SlideHolder(_layoutManager)
        {
            Content = item
        }
    );

    /// <inheritdoc />
    public void Clear() => SlideHolders.Clear();

    /// <inheritdoc />
    public bool Contains(IView item) => SlideHolders.Contains(item);

    /// <inheritdoc />
    void ICollection<IView>.CopyTo(IView[] array, int arrayIndex) => SlideHolders.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(IView item) => SlideHolders.Remove(item);

    /// <inheritdoc />
    public int Count => SlideHolders.Count;

    /// <inheritdoc />
    bool ICollection<IView>.IsReadOnly => SlideHolders.IsReadOnly;

    /// <inheritdoc />
    public int IndexOf(IView item)
    {
        var length = SlideHolders.Count;
        for (var i = 0; i < length; i++)
        {
            if (GetWrappedView(SlideHolders[i]) == item)
            {
                return i;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public void Insert(int index, IView item) => SlideHolders.Insert(index, new SlideHolder(_layoutManager)
                                                                                  {
                                                                                      Content = item
                                                                                  });

    /// <inheritdoc />
    public void RemoveAt(int index) => SlideHolders.RemoveAt(index);

    /// <inheritdoc />
    public IView this[int index]
    {
        get => GetWrappedView(SlideHolders[index]);
        set => ((SlideHolder)SlideHolders[index]).Content = value;
    }

    Thickness IPadding.Padding => Thickness.Zero;

    Size IContentView.CrossPlatformMeasure(double widthConstraint, double heightConstraint) => _layoutManager.Measure(widthConstraint, heightConstraint);

    object? IContentView.Content => _scrollView;

    IView? IContentView.PresentedContent => _scrollView;

    Size IContentView.CrossPlatformArrange(Rect bounds) => _layoutManager.ArrangeChildren(bounds);

    bool IViewBox.ClipsToBounds => false;
}

