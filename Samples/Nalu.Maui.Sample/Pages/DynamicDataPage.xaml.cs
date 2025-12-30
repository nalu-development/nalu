using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Messaging;
using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class DynamicDataPage : ContentPage, IRecipient<DynamicDataPageScrollToItemMessage>, IDisposable
{
    private readonly IMessenger _messenger;

    public DynamicDataPage(DynamicDataPageModel viewModel, IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register(this);
        BindingContext = viewModel;
        InitializeComponent();
    }

    public void Receive(DynamicDataPageScrollToItemMessage message)
    {
        Dispatcher.Dispatch(() =>
            {
                Toast.Make($"Scrolling to item at index {message.ItemIndex}").Show();
                VirtualScroll.ScrollTo(0, message.ItemIndex);
            }
        );
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }
}

