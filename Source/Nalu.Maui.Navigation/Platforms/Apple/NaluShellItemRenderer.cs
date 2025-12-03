using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRenderer(IShellContext shellContext) : UIViewController, IShellItemRenderer
{
    private readonly Dictionary<ShellSection, IShellSectionRenderer> _sectionRenderers = [];
    private Page? _displayedPage;
    private ShellSection? _selectedSection;
    private NaluTabBarContainerView? _tabBar;
    private View? _crossPlatformTabBar;

    // ReSharper disable once MemberCanBePrivate.Global
    protected IShellItemController ShellItemController => ShellItem;
    
    private readonly NaluShellSectionWrapperController _sectionWrapperController = new();

    UIViewController IShellItemRenderer.ViewController => this;

    public required ShellItem ShellItem
    {
        get;
        set
        {
            if (field is not null)
            {
                throw new InvalidOperationException($"{nameof(ShellItem)} can only be set once.");
            }

            ArgumentNullException.ThrowIfNull(value);

            field = value;
            OnShellItemSet();
        }
    }
    
    public override void LoadView()
    {
        base.LoadView();

        View!.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
        AddChildViewController(_sectionWrapperController);
        var wrapperView = _sectionWrapperController.View!;
        wrapperView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
        View!.AddSubview(wrapperView);
        _sectionWrapperController.DidMoveToParentViewController(this);

        UpdateTabBarView();
        
        GoTo(ShellItem.CurrentItem);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
    
        if (_crossPlatformTabBar is not null && _tabBar is { Hidden: false, NeedsMeasure: true })
        {
            var container = View!;
            var size = _tabBar.SizeThatFits(container.Frame.Size);
            var safeAreaInsets = container.SafeAreaInsets;
            var heightWithInsets = size.Height + safeAreaInsets.Bottom;
    
            var frame = new CoreGraphics.CGRect(
                0,
                container.Bounds.Height - heightWithInsets,
                container.Bounds.Width,
                heightWithInsets);
    
            _tabBar.Frame = frame;
            _sectionWrapperController.AdditionalSafeAreaInsets = new UIEdgeInsets(0, 0, heightWithInsets, 0);
        }
    }

    protected virtual void OnShellItemSet()
    {
        if (ShellItem.CurrentItem == null)
        {
            throw new InvalidOperationException($"Content not found for active {ShellItem}. Title: {ShellItem.Title}. Route: {ShellItem.Route}.");
        }

        ShellItemController.ItemsCollectionChanged += OnItemsCollectionChanged;
        ShellItem.PropertyChanged += OnShellItemPropertyChanged;
        
        OnItemsCollectionChanged(ShellItem, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, ShellItemController.GetItems()));
    }

    protected virtual void OnShellItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == ShellItem.CurrentItemProperty.PropertyName)
        {
            GoTo(ShellItem.CurrentItem);
        }
        else if (e.PropertyName == NaluShell.TabBarViewProperty.PropertyName)
        {
            UpdateTabBarView();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            var sectionsToRemove = _sectionRenderers.Keys.ToList();
            OnItemsCollectionChanged(ShellItem, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, sectionsToRemove));
            _sectionRenderers.Clear();
            
            ShellItem.PropertyChanged -= OnShellItemPropertyChanged;
            ShellItemController.ItemsCollectionChanged -= OnItemsCollectionChanged;
            ((IShellSectionController?)_selectedSection)?.RemoveDisplayedPageObserver(this);
            _selectedSection = null;
            _displayedPage = null;
            _tabBar?.RemoveFromSuperview();
            _tabBar = null;
            _crossPlatformTabBar = null;
        }
    }

    private void UpdateTabBarView()
    {
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        if (tabBarView == null)
        {
            _tabBar?.RemoveFromSuperview();
            _tabBar = null;
            _crossPlatformTabBar = null;
            AdditionalSafeAreaInsets = UIEdgeInsets.Zero;
        }
        else
        {
            var mauiContext = shellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarView.ToPlatform(mauiContext);
            var tabBarContainer = new NaluTabBarContainerView(platformView);
            _tabBar = tabBarContainer;
            _crossPlatformTabBar = tabBarView;
            UpdateTabBarHidden();
            View!.AddSubview(tabBarContainer);
        }
    }

    private void OnDisplayedPageChanged(Page page)
    {
        if (page != _displayedPage)
        {
            if (_displayedPage != null)
            {
                _displayedPage.PropertyChanged -= OnDisplayedPagePropertyChanged;
            }

            _displayedPage = page;

            if (_displayedPage != null)
            {
                _displayedPage.PropertyChanged += OnDisplayedPagePropertyChanged;
                UpdateTabBarHidden();
            }
        }
    }

    private void OnDisplayedPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Shell.TabBarIsVisibleProperty.PropertyName)
        {
            UpdateTabBarHidden();
        }
    }

    private void UpdateTabBarHidden()
    {
        if (_tabBar is not null)
        {
            var isTabBarVisible = _displayedPage?.GetValue(Shell.TabBarIsVisibleProperty) as bool? ?? true;
            _tabBar.Hidden = !isTabBarVisible;
        }
    }

    private void GoTo(ShellSection shellSection)
    {
        if (_sectionRenderers.TryGetValue(shellSection, out var renderer))
        {
            ((IShellSectionController?)_selectedSection)?.RemoveDisplayedPageObserver(this);
            _selectedSection = shellSection;
            ((IShellSectionController)_selectedSection)?.AddDisplayedPageObserver(this, OnDisplayedPageChanged);

            _sectionWrapperController.SelectedViewController = renderer.ViewController;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ShellSection removed in e.OldItems)
            {
                if (_sectionRenderers.Remove(removed, out var renderer))
                {
                    _sectionWrapperController.RemoveViewController(renderer.ViewController);
                    renderer.Dispose();
                }
            }
        }
        
        if (e.NewItems is not null)
        {
            foreach (ShellSection added in e.NewItems)
            {
                if (_sectionRenderers.ContainsKey(added))
                {
                    throw new InvalidOperationException($"Section renderer for {added} already exists.");
                }

                var renderer = shellContext.CreateShellSectionRenderer(added);
                renderer.ShellSection = added;
                _sectionRenderers[added] = renderer;
                _sectionWrapperController.AddViewController(renderer.ViewController);
            }
        }
    }
}
