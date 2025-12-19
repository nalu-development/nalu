using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Messaging;
using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class TenPage : ContentPage, IRecipient<TenPageScrollToItemMessage>, IDisposable
{
    private readonly IMessenger _messenger;

    public TenPage(TenPageModel viewModel, IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register(this);
        BindingContext = viewModel;
        InitializeComponent();
    }

    public void Receive(TenPageScrollToItemMessage message)
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

