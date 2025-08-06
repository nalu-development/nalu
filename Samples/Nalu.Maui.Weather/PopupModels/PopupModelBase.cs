using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Weather.PopupModels;

public abstract class PopupModelBase<TIntent, TResult>(INavigationService navigationService) : ObservableObject, IEnteringAware<TIntent>
    where TIntent : AwaitableIntent<TResult>
{
    protected TIntent PopupIntent { get; private set; } = null!;

    public virtual ValueTask OnEnteringAsync(TIntent intent)
    {
        PopupIntent = intent;
        return ValueTask.CompletedTask;
    }

    protected async Task CloseAsync()
    {
        await navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    protected Task CloseAsync(TResult result)
    {
        PopupIntent.SetResult(result);
        return CloseAsync();
    }

    protected Task CloseFaultyAsync(Exception exception)
    {
        PopupIntent.SetException(exception);
        return CloseAsync();
    }
}
