using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("View Box Tests")]
public class ViewBoxTestsPage : ContentPage
{
    private sealed class Animal(string name)
    {
        public string Name => name;
    }

    private sealed class ViewBoxTestsModel : INotifyPropertyChanged
    {
        private Animal _selectedAnimal = new("Dog");

        public Animal SelectedAnimal
        {
            get => _selectedAnimal;
            set
            {
                _selectedAnimal = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ViewBoxTestsPage()
    {
        var model = new ViewBoxTestsModel();
        BindingContext = model;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        // --- Content + content swap ---------------------------------------------------------
        var contentViewBox = new ViewBox
        {
            AutomationId = "TheViewBox",
            Content = new Label { Text = "Content A", AutomationId = "ViewBoxContentA" }
        };

        var swapContentButton = new Button { Text = "Swap content", AutomationId = "SwapContentButton" };
        swapContentButton.Clicked += (_, _) =>
            contentViewBox.Content = new Label { Text = "Content B", AutomationId = "ViewBoxContentB" };

        stack.Add(swapContentButton);
        stack.Add(contentViewBox);

        // --- ContentBindingContext ----------------------------------------------------------
        var animalLabel = new Label { AutomationId = "AnimalNameLabel" };
        animalLabel.SetBinding(Label.TextProperty, nameof(Animal.Name));

        var animalViewBox = new ViewBox { Content = animalLabel };
        animalViewBox.SetBinding(ViewBox.ContentBindingContextProperty, nameof(ViewBoxTestsModel.SelectedAnimal));

        var switchAnimalButton = new Button { Text = "Switch animal", AutomationId = "SwitchAnimalButton" };
        switchAnimalButton.Clicked += (_, _) => model.SelectedAnimal = new Animal("Cat");

        stack.Add(switchAnimalButton);
        stack.Add(animalViewBox);

        // --- IsClippedToBounds --------------------------------------------------------------
        // A 100x100 clipped ViewBox with 200x200 red content inside a white 220x220 host:
        // the pixel at host-relative (150,150) is red when unclipped, white when clipped.
        var clipBox = new ViewBox
        {
            AutomationId = "ClipBox",
            WidthRequest = 100,
            HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            IsClippedToBounds = true,
            Content = new BoxView
            {
                AutomationId = "ClipContent",
                WidthRequest = 200,
                HeightRequest = 200,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Color = Colors.Red
            }
        };

        var clipHost = new Grid
        {
            AutomationId = "ClipHost",
            WidthRequest = 220,
            HeightRequest = 220,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Colors.White
        };
        clipHost.Add(clipBox);

        var toggleClipButton = new Button { Text = "Toggle clipping", AutomationId = "ToggleClipButton" };
        toggleClipButton.Clicked += (_, _) => clipBox.IsClippedToBounds = !clipBox.IsClippedToBounds;

        stack.Add(toggleClipButton);
        stack.Add(clipHost);

        Content = new ScrollView { Content = stack };
    }
}
