using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Expander Tests")]
public class ExpanderViewBoxTestsPage : ContentPage
{
    public ExpanderViewBoxTestsPage()
    {
        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        // --- Collapse / expand with tall content --------------------------------------------
        var expander = new ExpanderViewBox
        {
            AutomationId = "ExpanderBox",
            CollapsedHeight = 100,
            IsExpanded = false,
            BackgroundColor = Colors.Coral,
            Content = new VerticalStackLayout
            {
                AutomationId = "ExpanderContent",
                Children =
                {
                    new BoxView
                    {
                        WidthRequest = 120,
                        HeightRequest = 300,
                        HorizontalOptions = LayoutOptions.Start,
                        Color = Colors.SteelBlue
                    }
                }
            }
        };

        var canCollapseLabel = new Label { AutomationId = "CanCollapseLabel" };
        canCollapseLabel.SetBinding(
            Label.TextProperty,
            new Binding(nameof(ExpanderViewBox.CanCollapse), source: expander, stringFormat: "CanCollapse={0}")
        );

        var toggleExpandButton = new Button { Text = "Toggle expanded", AutomationId = "ToggleExpandButton" };
        toggleExpandButton.Clicked += (_, _) => expander.IsExpanded = !expander.IsExpanded;

        stack.Add(toggleExpandButton);
        stack.Add(canCollapseLabel);
        stack.Add(expander);

        // --- Content smaller than CollapsedHeight + runtime growth --------------------------
        var smallContent = new BoxView
        {
            AutomationId = "SmallExpanderContent",
            WidthRequest = 120,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Color = Colors.MediumSeaGreen
        };

        var smallExpander = new ExpanderViewBox
        {
            AutomationId = "SmallExpanderBox",
            CollapsedHeight = 100,
            IsExpanded = false,
            BackgroundColor = Colors.Khaki,
            Content = smallContent
        };

        var smallCanCollapseLabel = new Label { AutomationId = "SmallCanCollapseLabel" };
        smallCanCollapseLabel.SetBinding(
            Label.TextProperty,
            new Binding(nameof(ExpanderViewBox.CanCollapse), source: smallExpander, stringFormat: "CanCollapse={0}")
        );

        var growContentButton = new Button { Text = "Grow content", AutomationId = "GrowContentButton" };
        growContentButton.Clicked += (_, _) => smallContent.HeightRequest = 300;

        stack.Add(growContentButton);
        stack.Add(smallCanCollapseLabel);
        stack.Add(smallExpander);

        Content = new ScrollView { Content = stack };
    }
}
