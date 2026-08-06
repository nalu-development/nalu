using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// The pill's content: lays out fixed-width item slots and computes which items fit.
/// Given the measure width constraint: <c>slots = floor(width / ItemWidth)</c>; when every
/// visible root fits, all are shown and "More" is hidden; otherwise <c>slots − 1</c> roots are
/// shown followed by the "More" item, and the remainder (declaration order) becomes the
/// overflow set. Desired width is <c>shown × ItemWidth</c> — the pill hugs its content.
/// </summary>
internal sealed class ScaffoldTabBarItemsLayout : Layout
{
    private readonly ScaffoldTabBarView _owner;
    private readonly List<ScaffoldTabBarItemView> _rootItems = [];
    private ScaffoldTabBarItemView? _moreItem;
    private List<ScaffoldRoot> _overflowRoots = [];

    public ScaffoldTabBarItemsLayout(ScaffoldTabBarView owner)
    {
        _owner = owner;
    }

    internal IReadOnlyList<ScaffoldRoot> OverflowRoots => _overflowRoots;

    internal IEnumerable<ScaffoldTabBarItemView> ItemViews
        => _moreItem is null ? _rootItems : [.. _rootItems, _moreItem];

    internal ScaffoldTabBarItemView? MoreItem => _moreItem;

    internal void Rebuild()
    {
        foreach (var item in _rootItems)
        {
            item.Unsubscribe();
            Remove(item);
        }

        _rootItems.Clear();

        if (_moreItem is null)
        {
            _moreItem = new ScaffoldTabBarItemView(_owner, root: null);
        }
        else
        {
            Remove(_moreItem);
        }

        if (_owner.TabBar is { } tabBar)
        {
            foreach (var root in tabBar.Roots)
            {
                var item = new ScaffoldTabBarItemView(_owner, root);
                _rootItems.Add(item);
                Add(item);
            }
        }

        // The More item stays LAST in child order: ArrangeChildren walks children in order and
        // hands the in-plan ones consecutive slots.
        Add(_moreItem);
        InvalidateMeasure();
    }

    /// <summary>Replaces the More item — its content (drawn ••• glyph vs. user icon) is fixed at construction.</summary>
    internal void RebuildMoreItem()
    {
        if (_moreItem is not null)
        {
            Remove(_moreItem);
        }

        _moreItem = new ScaffoldTabBarItemView(_owner, root: null);
        _moreItem.SetSelectedState(_overflowRoots.Any(static root => root.IsSelected));
        Add(_moreItem);
        InvalidateMeasure();
    }

    internal void OnRootVisibilityChanged() => InvalidateMeasure();

    internal void UpdateMoreState()
        => _moreItem?.SetSelectedState(_overflowRoots.Any(r => r.IsSelected));

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    private sealed class Manager(ScaffoldTabBarItemsLayout layout) : ILayoutManager
    {
        private List<ScaffoldTabBarItemView> _plan = [];

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var itemWidth = Math.Max(1, layout._owner.ItemWidth);
            var visibleItems = layout._rootItems.Where(item => item.Root!.IsVisible).ToList();

            int shownRootCount;
            var showMore = false;

            if (double.IsInfinity(widthConstraint) || visibleItems.Count * itemWidth <= widthConstraint)
            {
                shownRootCount = visibleItems.Count;
            }
            else
            {
                var slots = Math.Max(2, (int)(widthConstraint / itemWidth));
                shownRootCount = Math.Min(slots - 1, visibleItems.Count);
                showMore = true;
            }

            _plan = visibleItems.Take(shownRootCount).ToList();

            if (showMore && layout._moreItem is { } moreItem)
            {
                _plan.Add(moreItem);
            }

            var overflow = visibleItems.Skip(shownRootCount).Select(item => item.Root!).ToList();

            if (!overflow.SequenceEqual(layout._overflowRoots))
            {
                layout._overflowRoots = overflow;
                layout.UpdateMoreState();
                layout._owner.NotifyOverflowRootsChanged();
            }

            double height = 0;

            foreach (var item in _plan)
            {
                var size = ((IView)item).Measure(itemWidth, heightConstraint);
                height = Math.Max(height, size.Height);
            }

            return new Size(_plan.Count * itemWidth, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var itemWidth = Math.Max(1, layout._owner.ItemWidth);
            var x = bounds.X;

            foreach (var child in layout.Cast<IView>())
            {
                if (child is ScaffoldTabBarItemView item && _plan.Contains(item))
                {
                    item.Arrange(new Rect(x, bounds.Y, itemWidth, bounds.Height));
                    x += itemWidth;
                }
                else
                {
                    // Out-of-plan items park far offscreen (a zero-size arrange still renders
                    // the platform view at its stale frame on iOS); the pill's clip hides them.
                    child.Arrange(new Rect(-10000, -10000, itemWidth, bounds.Height));
                }
            }

            return bounds.Size;
        }
    }
}
