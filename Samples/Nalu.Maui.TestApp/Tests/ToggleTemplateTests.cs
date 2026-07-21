using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Toggle Template Tests")]
public class ToggleTemplateTestsPage : ContentPage
{
    public ToggleTemplateTestsPage()
    {
        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        // --- WhenTrue / WhenFalse -----------------------------------------------------------
        var toggleTemplate = new ToggleTemplate
        {
            Value = false,
            WhenTrue = new DataTemplate(() => new Label { Text = "TRUE", AutomationId = "WhenTrueLabel" }),
            WhenFalse = new DataTemplate(() => new Label { Text = "FALSE", AutomationId = "WhenFalseLabel" })
        };

        var toggleValueButton = new Button { Text = "Toggle value", AutomationId = "ToggleValueButton" };
        toggleValueButton.Clicked += (_, _) => toggleTemplate.Value = !(toggleTemplate.Value ?? false);

        stack.Add(toggleValueButton);
        stack.Add(toggleTemplate);

        // --- WhenTrue only (no template -> no content) --------------------------------------
        var onlyTrueTemplate = new ToggleTemplate
        {
            Value = false,
            WhenTrue = new DataTemplate(() => new Label { Text = "EXPENSIVE", AutomationId = "OnlyTrueLabel" })
        };

        var toggleOnlyTrueButton = new Button { Text = "Toggle only-true", AutomationId = "ToggleOnlyTrueButton" };
        toggleOnlyTrueButton.Clicked += (_, _) => onlyTrueTemplate.Value = !(onlyTrueTemplate.Value ?? false);

        stack.Add(toggleOnlyTrueButton);
        stack.Add(onlyTrueTemplate);

        Content = new ScrollView { Content = stack };
    }
}
