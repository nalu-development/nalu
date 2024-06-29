namespace Nalu;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Nalu.Internals;

#pragma warning disable CA1725

/// <summary>
/// Provides a navigation command that can be used in XAML by providing a <see cref="Navigation"/> object as parameter.
/// </summary>
[SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "This is a command, not an async delegate")]
public class NavigateCommand : IMarkupExtension<ICommand>
{
    /// <inheritdoc cref="IMarkupExtension{T}.ProvideValue"/>
    public ICommand ProvideValue(IServiceProvider xamlProvider)
    {
        var provideValueTarget = xamlProvider.GetRequiredService<IProvideValueTarget>();

        return new NavigateCommandCommand(provideValueTarget);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "This is a command, not an async delegate")]
    private sealed class NavigateCommandCommand(IProvideValueTarget provideValueTarget) : ICommand
    {
        private int _canExecute = 1;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute == 1;

        public async void Execute(object? parameter)
        {
            if (parameter is not Navigation navigation)
            {
                throw new ArgumentException("The parameter must be of type Navigation", nameof(parameter));
            }

            var element = (IElement)provideValueTarget.TargetObject;
            while (element.Handler is null && element.Parent is not null)
            {
                element = element.Parent;
            }

            if (element.Handler is null)
            {
                throw new InvalidOperationException("Navigation commands can only be used when the element is part of the visual tree");
            }

            if (Interlocked.CompareExchange(ref _canExecute, 0, 1) == 0)
            {
                return;
            }

            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

            var navigationService = element.Handler.GetRequiredService<INavigationService>();
            try
            {
                await navigationService.GoToAsync(navigation).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Console.WriteLine(ex);
                }
            }

            Interlocked.Exchange(ref _canExecute, 1);
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
