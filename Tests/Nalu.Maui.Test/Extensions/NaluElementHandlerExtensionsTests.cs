using Nalu.Internals;

namespace Nalu.Maui.Test.Extensions;

/// <summary>
/// Contains test methods for <see cref="NaluElementHandlerExtensions" />.
/// </summary>
public class NaluElementHandlerExtensionsTests
{
    // ReSharper disable once MemberCanBePrivate.Global
    internal interface IService;

    [Fact(DisplayName = "GetServiceProvider should throw InvalidOperationException when MauiContext is null")]
    public void GetServiceProviderShouldThrowInvalidOperationExceptionWhenMauiContextIsNull()
    {
        // Arrange
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.ReturnsNull();

        // Act
        var exception = Record.Exception(handler.GetServiceProvider);

        // Assert
        exception.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
        exception!.Message.Should().Be("Unable to find the context. The MauiContext property should have been set by the host.");
    }

    [Fact(DisplayName = "GetServiceProvider should throw InvalidOperationException when MauiContext.Services is null")]
    public void GetServiceProviderShouldThrowInvalidOperationExceptionWhenMauiContextServicesIsNull()
    {
        // Arrange
        var context = Substitute.For<IMauiContext>();
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.Returns(context);
        context.Services.ReturnsNull();

        // Act
        var exception = Record.Exception(handler.GetServiceProvider);

        // Assert
        exception.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
        exception!.Message.Should().Be("Unable to find the service provider. The MauiContext property should have been set by the host.");
    }

    [Fact(DisplayName = "GetService should return service from MauiContext")]
    public void GetServiceShouldReturnServiceFromMauiContext()
    {
        // Arrange
        var context = Substitute.For<IMauiContext>();
        var services = Substitute.For<IServiceProvider>();
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.Returns(context);
        context.Services.Returns(services);
        var expected = Substitute.For<IService>();
        services.GetService(typeof(IService)).Returns(expected);

        // Act
        var actual = handler.GetService<IService>();

        // Assert
        actual.Should().NotBeNull().And.Be(expected);
    }

    [Fact(DisplayName = "GetService should return null when service is not found")]
    public void GetServiceShouldReturnNullWhenServiceIsNotFound()
    {
        // Arrange
        var context = Substitute.For<IMauiContext>();
        var services = Substitute.For<IServiceProvider>();
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.Returns(context);
        context.Services.Returns(services);
        services.GetService(typeof(IService)).ReturnsNull();

        // Act
        var actual = handler.GetService<IService>();

        // Assert
        actual.Should().BeNull();
    }

    [Fact(DisplayName = "GetRequiredService should return service from MauiContext")]
    public void GetRequiredServiceShouldReturnServiceFromMauiContext()
    {
        // Arrange
        var context = Substitute.For<IMauiContext>();
        var services = Substitute.For<IServiceProvider>();
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.Returns(context);
        context.Services.Returns(services);
        var expected = Substitute.For<IService>();
        services.GetService(typeof(IService)).Returns(expected);

        // Act
        var actual = handler.GetRequiredService<IService>();

        // Assert
        actual.Should().NotBeNull().And.Be(expected);
    }

    [Fact(DisplayName = "GetRequiredService should throw InvalidOperationException when service is not found")]
    public void GetRequiredServiceShouldThrowInvalidOperationExceptionWhenServiceIsNotFound()
    {
        // Arrange
        var context = Substitute.For<IMauiContext>();
        var services = Substitute.For<IServiceProvider>();
        var handler = Substitute.For<IElementHandler>();
        handler.MauiContext.Returns(context);
        context.Services.Returns(services);
        services.GetService(typeof(IService)).ReturnsNull();

        // Act
        var exception = Record.Exception(handler.GetRequiredService<IService>);

        // Assert
        exception.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
    }
}
