using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Xaml;

namespace Nalu.Maui.Test.Internals;

public class VirtualScrollAdapterBindingTests
{
    public VirtualScrollAdapterBindingTests() => DispatcherProvider.SetCurrent(new DispatcherProviderStub());

    // DragHandler is deliberately declared BEFORE ItemsSource to prove the
    // {RelativeSource Self} binding does not depend on attribute order:
    // it must pick up the Adapter through the property-changed notification
    // raised when ItemsSource coerces a new adapter.
    private const string _xaml =
        """
        <nalu:VirtualScroll
            xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
            xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
            xmlns:nalu="clr-namespace:Nalu;assembly=Nalu.Maui.VirtualScroll"
            DragHandler="{Binding Adapter, Source={RelativeSource Self}}"
            ItemsSource="{Binding Items}" />
        """;

    [Fact]
    public void SelfBoundDragHandler_ShouldResolveToCoercedAdapter()
    {
        var virtualScroll = new VirtualScroll().LoadFromXaml(_xaml);

        virtualScroll.BindingContext = new TestViewModel();

        virtualScroll.Adapter.Should().NotBeNull().And.BeAssignableTo<IReorderableVirtualScrollAdapter>();
        virtualScroll.DragHandler.Should().BeSameAs(virtualScroll.Adapter);
    }

    [Fact]
    public void SelfBoundDragHandler_ShouldFollowAdapterWhenItemsSourceCollectionIsReplaced()
    {
        var virtualScroll = new VirtualScroll().LoadFromXaml(_xaml);
        var viewModel = new TestViewModel();
        virtualScroll.BindingContext = viewModel;
        var initialAdapter = virtualScroll.Adapter;

        viewModel.Items = new ObservableCollection<string> { "X", "Y" };

        virtualScroll.Adapter.Should().NotBeSameAs(initialAdapter);
        virtualScroll.DragHandler.Should().BeSameAs(virtualScroll.Adapter);
    }

    private sealed class TestViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<string> _items = ["A", "B", "C"];

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<string> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
