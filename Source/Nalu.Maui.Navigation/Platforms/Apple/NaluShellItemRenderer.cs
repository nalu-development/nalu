using System.Collections.Specialized;
using System.ComponentModel;
using CoreGraphics;
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
    private UIView? _tabBarScrim;
    private View? _crossPlatformTabBar;
    private View? _crossPlatformTabBarScrim;

    // ReSharper disable once MemberCanBePrivate.Global
    protected IShellItemController ShellItemController => ShellItem;
    
    private readonly NaluShellSectionWrapperController _sectionWrapperController = new();
    private CGRect _lastBounds;

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

        var container = View!;
        AddChildViewController(_sectionWrapperController);
        var wrapperView = _sectionWrapperController.View!;
        container.AddSubview(wrapperView);
        wrapperView.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints([
            wrapperView.TopAnchor.ConstraintEqualTo(container.TopAnchor),
            wrapperView.BottomAnchor.ConstraintEqualTo(container.BottomAnchor),
            wrapperView.LeftAnchor.ConstraintEqualTo(container.LeftAnchor),
            wrapperView.RightAnchor.ConstraintEqualTo(container.RightAnchor)
        ]);
        _sectionWrapperController.DidMoveToParentViewController(this);

        UpdateTabBarView();
        UpdateTabBarScrimView();
        
        GoTo(ShellItem.CurrentItem);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
    
        if (_crossPlatformTabBar is not null && _tabBar is { Hidden: false })
        {
            var container = View!;
            var containerBounds = container.Bounds;

            if (_tabBar.NeedsMeasure || containerBounds != _lastBounds)
            {
                _lastBounds = containerBounds;

                var size = _tabBar.SizeThatFits(_lastBounds.Size);
#if NET10_0_OR_GREATER
                var safeAreaInsets = container.SafeAreaInsets;
                var heightWithoutInsets = size.Height - safeAreaInsets.Bottom;
                var height = size.Height;
#else
                var safeAreaInsets = container.SafeAreaInsets;
                var heightWithoutInsets = size.Height;
                var height = size.Height + safeAreaInsets.Bottom;
#endif

                var frame = new CGRect(
                    0,
                    containerBounds.Height - height,
                    containerBounds.Width,
                    height
                );

                _tabBar.Frame = frame;
                _sectionWrapperController.AdditionalSafeAreaInsets = new UIEdgeInsets(0, 0, heightWithoutInsets, 0);
            }
        }
        else
        {
            _sectionWrapperController.AdditionalSafeAreaInsets = UIEdgeInsets.Zero;
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
        switch (e.PropertyName)
        {
            case nameof(ShellItem.CurrentItem):
                GoTo(ShellItem.CurrentItem);
                break;
            case "TabBarView":
                UpdateTabBarView();
                break;
            case "TabBarScrimView":
                UpdateTabBarScrimView();
                break;
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
            _tabBarScrim?.RemoveFromSuperview();
            _tabBarScrim = null;
            _crossPlatformTabBarScrim?.DisconnectHandlers();
            _crossPlatformTabBarScrim = null;
            _tabBar?.RemoveFromSuperview();
            _tabBar = null;
            _crossPlatformTabBar?.DisconnectHandlers();
            _crossPlatformTabBar = null;
        }
    }

    private void UpdateTabBarView()
    {
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        _tabBar?.RemoveFromSuperview();
        _tabBar = null;
        _crossPlatformTabBar?.DisconnectHandlers();
        _crossPlatformTabBar = null;

        if (tabBarView == null)
        {
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

    private void UpdateTabBarScrimView()
    {
        var tabBarScrimView = NaluShell.GetTabBarScrimView(ShellItem);

        _tabBarScrim?.RemoveFromSuperview();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        if (tabBarScrimView != null)
        {
            var mauiContext = shellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarScrimView.ToPlatform(mauiContext);
            _tabBarScrim = platformView;
            _crossPlatformTabBar = tabBarScrimView;
            var container = _sectionWrapperController.View!;
            container.AddSubview(platformView);
            platformView.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints([
                platformView.TopAnchor.ConstraintEqualTo(container.TopAnchor),
                platformView.BottomAnchor.ConstraintEqualTo(container.BottomAnchor),
                platformView.LeftAnchor.ConstraintEqualTo(container.LeftAnchor),
                platformView.RightAnchor.ConstraintEqualTo(container.RightAnchor)
            ]);
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
