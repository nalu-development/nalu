namespace Nalu.Maui.Test.NavigationTests;

public class NavigationSegmentAttributeTests
{
    [NavigationSegment("Hello")]
    private class WithAttribute;

    private class WithoutAttribute;

    private class WithGenericAttribute<T>;

    [Fact(DisplayName = "NavigationSegmentAttribute.GetSegmentName, when no attribute, returns type name")]
    public void NavigationSegmentAttributeGetSegmentNameWhenNoAttributeReturnsTypeName()
    {
        // Arrange
        var type = typeof(WithoutAttribute);

        // Act
        var result = NavigationSegmentAttribute.GetSegmentName(type);

        // Assert
        result.Should().Be(nameof(WithoutAttribute));
    }

    [Fact(DisplayName = "NavigationSegmentAttribute.GetSegmentName, when attribute, returns attribute value")]
    public void NavigationSegmentAttributeGetSegmentNameWhenAttributeReturnsAttributeValue()
    {
        // Arrange
        var type = typeof(WithAttribute);

        // Act
        var result = NavigationSegmentAttribute.GetSegmentName(type);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact(DisplayName = "NavigationSegmentAttribute.GetSegmentName, when generic type, returns type name")]
    public void NavigationSegmentAttributeGetSegmentNameWhenGenericTypeReturnsTypeName()
    {
        // Arrange
        var type = typeof(WithGenericAttribute<WithoutAttribute>);

        // Act
        var result = NavigationSegmentAttribute.GetSegmentName(type);
        var uri = new Uri($"//{result}");

        // Assert
        result.Should().Be("WithGenericAttribute-WithoutAttribute");
        uri.IsAbsoluteUri.Should().BeTrue();
    }
}
