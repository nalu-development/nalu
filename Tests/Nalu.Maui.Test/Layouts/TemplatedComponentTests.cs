namespace Nalu.Maui.Test.Layouts;

public class TemplatedComponentTests
{
    [Fact(DisplayName = "ContentTemplate property should set and get content template")]
    public void ContentTemplateProperty_Should_SetAndGetContentTemplate()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplate = new DataTemplate(() => new Label { Text = "Test" });

        // Act
        templatedComponent.ContentTemplate = dataTemplate;
        var result = templatedComponent.ContentTemplate;

        // Assert
        result.Should().Be(dataTemplate);
    }

    [Fact(DisplayName = "ContentTemplate property should update content and content template")]
    public void ContentTemplatePropertyShouldUpdateContentAndContentTemplate()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var oldDataTemplate = new DataTemplate(() => new Label { Text = "Old" });
        var newDataTemplate = new DataTemplate(() => new Label { Text = "New" });

        // Act & Assert
        templatedComponent.ContentTemplate = oldDataTemplate;
        templatedComponent.ContentTemplate.Should().Be(oldDataTemplate);
        templatedComponent.PresentedContent.Should().BeOfType<Label>().Which.Text.Should().Be("Old");

        templatedComponent.ContentTemplate = newDataTemplate;
        templatedComponent.ContentTemplate.Should().Be(newDataTemplate);
        templatedComponent.PresentedContent.Should().BeOfType<Label>().Which.Text.Should().Be("New");
    }

    [Fact(DisplayName = "ContentTemplate accepts a DataTemplateSelector based on BindingContext")]
    public void ContentTemplateAcceptsADataTemplateSelectorBasedOnBindingContext()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplateSelector = new TestTemplateSelector();
        var bindingContext = "hello";
        templatedComponent.BindingContext = bindingContext;

        // Act
        templatedComponent.ContentTemplate = dataTemplateSelector;

        // Assert
        templatedComponent.ContentTemplate.Should().Be(dataTemplateSelector);
        templatedComponent.PresentedContent.Should().BeOfType<Label>();
        var label = (Label)templatedComponent.PresentedContent!;
        label.Text.Should().Be(bindingContext);
        label.BindingContext.Should().Be(bindingContext);
    }

    [Fact(DisplayName = "ContentTemplate accepts a DataTemplateSelector based on ContentBindingContext")]
    public void ContentTemplateAcceptsADataTemplateSelectorBasedOnContentBindingContext()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplateSelector = new TestTemplateSelector();
        var bindingContext = "hello";
        var contentBindingContext = "world";
        templatedComponent.BindingContext = bindingContext;
        templatedComponent.ContentBindingContext = contentBindingContext;

        // Act
        templatedComponent.ContentTemplate = dataTemplateSelector;

        // Assert
        templatedComponent.ContentTemplate.Should().Be(dataTemplateSelector);
        templatedComponent.PresentedContent.Should().BeOfType<Label>();
        var label = (Label)templatedComponent.PresentedContent!;
        label.Text.Should().Be(contentBindingContext);
        label.BindingContext.Should().Be(contentBindingContext);
    }

    [Fact(DisplayName = "BindingContext set through ContentBindingContext should be cleared from content on removal")]
    public void BindingContextSetThroughContentBindingContextShouldBeClearedFromContentOnRemoval()
    {
        // Arrange
        var component = new TemplatedComponent();
        var newBindingContext = new object();
        component.ContentBindingContext = newBindingContext;
        component.ContentTemplate = new DataTemplate(() => new Label());
        var view = (Label)component.PresentedContent!;
        view.BindingContext.Should().Be(newBindingContext);

        // Act
        component.ContentTemplate = null;

        // Assert
        view.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "BindingContext set through BindingContext should be cleared from content on removal")]
    public void BindingContextSetThroughBindingContextShouldBeClearedFromContentOnRemoval()
    {
        // Arrange
        var component = new TemplatedComponent();
        var newBindingContext = new object();
        component.BindingContext = newBindingContext;
        component.ContentTemplate = new DataTemplate(() => new Label());
        var view = (Label)component.PresentedContent!;
        view.BindingContext.Should().Be(newBindingContext);

        // Act
        component.ContentTemplate = null;

        // Assert
        view.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "ContentTemplate clears content when DataTemplateSelector is used on an empty ContentBindingContext")]
    public void ContentTemplateClearsContentWhenDataTemplateSelectorIsUsedOnAnEmptyContentBindingContext()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplateSelector = new TestTemplateSelector();
        var contentBindingContext = "world";
        templatedComponent.ContentBindingContext = contentBindingContext;
        templatedComponent.ContentTemplate = dataTemplateSelector;
        var label = (Label)templatedComponent.PresentedContent!;
        label.BindingContext.Should().Be(contentBindingContext);

        // Act
        templatedComponent.ContentBindingContext = null;

        // Assert
        templatedComponent.PresentedContent.Should().BeNull();
        label.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "ContentTemplate clears content when DataTemplateSelector is used on an empty BindingContext")]
    public void ContentTemplateClearsContentWhenDataTemplateSelectorIsUsedOnAnEmptyBindingContext()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplateSelector = new TestTemplateSelector();
        var bindingContext = "world";
        templatedComponent.BindingContext = bindingContext;
        templatedComponent.ContentTemplate = dataTemplateSelector;
        var label = (Label)templatedComponent.PresentedContent!;
        label.BindingContext.Should().Be(bindingContext);

        // Act
        templatedComponent.BindingContext = null;

        // Assert
        templatedComponent.PresentedContent.Should().BeNull();
        label.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "ContentTemplate changes binding context when DataTemplateSelector returns the same template")]
    public void ContentTemplateChangesBindingContextWhenDataTemplateSelectorReturnsTheSameTemplate()
    {
        // Arrange
        var templatedComponent = new TemplatedComponent();
        var dataTemplateSelector = new StaticRefTemplateSelector();
        var bindingContext = "world";
        templatedComponent.BindingContext = bindingContext;
        templatedComponent.ContentTemplate = dataTemplateSelector;
        var label = (Label)templatedComponent.PresentedContent!;
        label.BindingContext.Should().Be(bindingContext);

        // Act
        var newBindingContext = "hello";
        templatedComponent.BindingContext = newBindingContext;

        // Assert
        templatedComponent.PresentedContent.Should().Be(label);
        label.BindingContext.Should().Be(newBindingContext);
    }

    [Fact(DisplayName = "ContentTemplate can project content")]
    public Task ContentTemplateCanProjectContent()
        => DispatcherTest.RunWithDispatcherStub(() =>
        {
            // Arrange
            var templatedComponent = new TemplatedComponent();
            var dataTemplate = new DataTemplate(() => new ProjectContainer());
            var bindingContext = "world";
            var projectedContent = new Label { Text = "hello" };
            templatedComponent.BindingContext = bindingContext;
            templatedComponent.ContentTemplate = dataTemplate;

            // Act
            templatedComponent.ProjectedContent = projectedContent;

            // Assert
            var container = templatedComponent.PresentedContent as ProjectContainer;
            container.Should().NotBeNull();
            container!.Content.Should().Be(projectedContent);
        });

    private class TestTemplateSelector : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object? item, BindableObject container)
            => new(() => new Label { Text = (string?)item! });
    }

    private class StaticRefTemplateSelector : DataTemplateSelector
    {
        private static readonly DataTemplate _template = new(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, new Binding("."));
            return label;
        });

        protected override DataTemplate OnSelectTemplate(object? item, BindableObject container)
            => _template;
    }
}
