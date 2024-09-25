namespace Nalu.Maui.Test.Layouts;

public class ComponentTests
{
    [Fact(DisplayName = "Content property should set and get content")]
    public void ContentProperty_Should_SetAndGetContent()
    {
        // Arrange
        var component = new Component();
        var view = new Label { Text = "Test" };

        // Act
        component.Content = view;
        var result = component.Content;

        // Assert
        result.Should().Be(view);
    }

    [Fact(DisplayName = "On content property changed should update content")]
    public void OnContentPropertyChanged_Should_UpdateContent()
    {
        // Arrange
        var component = new Component();
        var oldView = new Label { Text = "Old" };
        var newView = new Label { Text = "New" };

        // Act
        component.Content = oldView;
        component.Content = newView;

        // Assert
        component.Content.Should().Be(newView);
    }

    [Fact(DisplayName = "ContentBindingContext property changes should set BindingContext on content")]
    public void ContentBindingContextPropertyChangesShouldSetBindingContextOnContent()
    {
        // Arrange
        var component = new Component();
        var view = new Label();
        component.Content = view;
        var newBindingContext = new object();

        // Act
        component.ContentBindingContext = newBindingContext;

        // Assert
        view.BindingContext.Should().Be(newBindingContext);
    }

    [Fact(DisplayName = "ContentBindingContext property should set BindingContext on content")]
    public void ContentBindingContextPropertyShouldSetBindingContextOnContent()
    {
        // Arrange
        var component = new Component();
        var view = new Label();
        var newBindingContext = new object();
        component.ContentBindingContext = newBindingContext;

        // Act
        component.Content = view;

        // Assert
        view.BindingContext.Should().Be(newBindingContext);
    }

    [Fact(DisplayName = "BindingContext should propagate to content")]
    public void BindingContextShouldPropagateToContent()
    {
        // Arrange
        var component = new Component();
        var view = new Label();
        var newBindingContext = new object();
        component.BindingContext = newBindingContext;

        // Act
        component.Content = view;

        // Assert
        view.BindingContext.Should().Be(newBindingContext);
    }

    [Fact(DisplayName = "BindingContext should be cleared from content on removal")]
    public void BindingContextShouldBeClearedFromContentOnRemoval()
    {
        // Arrange
        var component = new Component();
        var view = new Label();
        var newBindingContext = new object();
        component.BindingContext = newBindingContext;

        // Act
        component.Content = view;
        component.Content = null;

        // Assert
        view.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "BindingContext set through ContentBindingContext should be cleared from content on removal")]
    public void BindingContextSetThroughContentBindingContextShouldBeClearedFromContentOnRemoval()
    {
        // Arrange
        var component = new Component();
        var view = new Label();
        var newBindingContext = new object();
        component.ContentBindingContext = newBindingContext;

        // Act
        component.Content = view;
        component.Content = null;

        // Assert
        view.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "On padding property changed should invalidate measure")]
    public void OnPaddingPropertyChanged_Should_InvalidateMeasure()
    {
        // Arrange
        var component = new Component { IsPlatformEnabled = true };
        var newPadding = new Thickness(10);
        var measureInvalidated = false;
        component.MeasureInvalidated += (s, e) => measureInvalidated = true;

        // Act
        component.Padding = newPadding;

        // Assert
        component.Padding.Should().Be(newPadding);
        measureInvalidated.Should().BeTrue();
    }
}
