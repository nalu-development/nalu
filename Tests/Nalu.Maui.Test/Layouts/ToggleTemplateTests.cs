namespace Nalu.Maui.Test.Layouts;

public class ToggleTemplateTests
{
    [Theory(DisplayName = "ToggleTemplate renders expected template")]
    [InlineData(true, typeof(Label))]
    [InlineData(false, typeof(Button))]
    [InlineData(null, null)]
    public void ToggleTemplateRendersExpectedTemplate(bool? value, Type? expectedContentType)
    {
        // Arrange
        var toggleTemplate = new ToggleTemplate();
        var trueTemplate = new DataTemplate(() => new Label());
        var falseTemplate = new DataTemplate(() => new Button());

        // Act
        toggleTemplate.WhenTrue = trueTemplate;
        toggleTemplate.WhenFalse = falseTemplate;
        toggleTemplate.Value = value;

        // Assert
        if (expectedContentType != null)
        {
            toggleTemplate.PresentedContent.Should().BeOfType(expectedContentType);
        }
        else
        {
            toggleTemplate.PresentedContent.Should().BeNull();
        }
    }

    [Theory(DisplayName = "ToggleTemplate renders expected template when not provided")]
    [InlineData(true)]
    [InlineData(false)]
    public void ToggleTemplateRendersExpectedTemplateWhenNotProvided(bool value)
    {
        // Arrange
        var toggleTemplate = new ToggleTemplate();
        var trueTemplate = value ? new DataTemplate(() => new Label()) : null;
        var falseTemplate = value ? null : new DataTemplate(() => new Label());

        // Act
        toggleTemplate.WhenTrue = trueTemplate;
        toggleTemplate.WhenFalse = falseTemplate;
        toggleTemplate.Value = !value;

        // Assert
        toggleTemplate.PresentedContent.Should().BeNull();
    }

    [Fact(DisplayName = "ToggleTemplate changes content when value changes")]
    public void ToggleTemplateChangesContentWhenValueChanges()
    {
        // Arrange
        var toggleTemplate = new ToggleTemplate();
        var trueTemplate = new DataTemplate(() => new Label { Text = "True" });
        var falseTemplate = new DataTemplate(() => new Label { Text = "False" });
        toggleTemplate.WhenTrue = trueTemplate;
        toggleTemplate.WhenFalse = falseTemplate;

        // Act & Assert
        toggleTemplate.Value = true;
        toggleTemplate.PresentedContent.Should().BeOfType<Label>().Which.Text.Should().Be("True");

        toggleTemplate.Value = false;
        toggleTemplate.PresentedContent.Should().BeOfType<Label>().Which.Text.Should().Be("False");

        toggleTemplate.Value = null;
        toggleTemplate.PresentedContent.Should().BeNull();
    }

    [Fact(DisplayName = "ToggleTemplate clears when BindingContext is set to null")]
    public void ToggleTemplateClearsWhenBindingContextIsSetToNull()
    {
        // Arrange
        var toggleTemplate = new ToggleTemplate();
        var trueTemplate = new DataTemplate(() => new Label());
        var falseTemplate = new DataTemplate(() => new Button());
        toggleTemplate.WhenTrue = trueTemplate;
        toggleTemplate.WhenFalse = falseTemplate;
        
        var bindingContext = new { TheValue = true };
        toggleTemplate.SetBinding(ToggleTemplate.ValueProperty, new Binding("TheValue"));
        toggleTemplate.BindingContext = bindingContext;
        // Ensure initial state
        toggleTemplate.PresentedContent.Should().BeOfType<Label>();

        // Act
        toggleTemplate.BindingContext = null;

        // Assert
        toggleTemplate.PresentedContent.Should().BeNull();
    }
}
