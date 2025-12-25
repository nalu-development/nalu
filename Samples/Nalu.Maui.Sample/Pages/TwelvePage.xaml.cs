using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using Nalu.Maui.Sample.PageModels;
using Nalu;

namespace Nalu.Maui.Sample.Pages;

public partial class TwelvePage : ContentPage
{
    private TwelvePageModel? _viewModel;
    private CollectionView? _collectionView;
    private VirtualScroll? _virtualScroll;

    public TwelvePage(TwelvePageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        
        // Subscribe to property changes
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // Loaded event to find controls after they're created
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        FindControls();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TwelvePageModel.ViewMode))
        {
            // Controls might be recreated when ViewMode changes, so find them again
            // Use a small delay to ensure the template has been applied
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                FindControls();
            });
        }
        else if (e.PropertyName == nameof(TwelvePageModel.ScrollTrigger))
        {
            // Ensure controls are found before scrolling
            if (_collectionView == null && _virtualScroll == null)
            {
                FindControls();
            }
            PerformScroll();
        }
    }

    private void FindControls()
    {
        _collectionView = FindControl<CollectionView>(this);
        _virtualScroll = FindControl<VirtualScroll>(this);
    }

    private static T? FindControl<T>(Element parent) where T : Element
    {
        if (parent is T control)
        {
            return control;
        }

        // Check IContentView (includes ViewBox, TemplateBox, etc.)
        if (parent is IContentView contentView && contentView.Content != null)
        {
            var found = FindControl<T>(contentView.Content as Element ?? throw new InvalidOperationException());
            if (found != null)
            {
                return found;
            }
        }

        // Check IViewContainer (includes Grid, StackLayout, etc.)
        if (parent is IViewContainer<View> container)
        {
            foreach (var child in container.Children)
            {
                if (child is Element childElement)
                {
                    var found = FindControl<T>(childElement);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }

        // Check Layout children
        if (parent is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is Element childElement)
                {
                    var found = FindControl<T>(childElement);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }

        return null;
    }

    private void PerformScroll()
    {
        if (_viewModel == null)
        {
            return;
        }

        var sectionIndex = _viewModel.CurrentSectionIndex;
        var itemIndex = _viewModel.CurrentItemIndex;

        Dispatcher.Dispatch(() =>
        {
            if (_viewModel.ViewMode == true && _collectionView != null)
            {
                // Scroll CollectionView
                if (sectionIndex < _viewModel.Groups.Count)
                {
                    var group = _viewModel.Groups[sectionIndex];
                    if (itemIndex < group.Items.Count)
                    {
                        var item = group.Items[itemIndex];
                        _collectionView.ScrollTo(item, group);
                    }
                }
            }
            else if (_viewModel.ViewMode == false && _virtualScroll != null)
            {
                // Scroll VirtualScroll
                _virtualScroll.ScrollTo(sectionIndex, itemIndex, ScrollToPosition.MakeVisible, animated: true);
            }
        });
    }
}

