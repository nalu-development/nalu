using System.Runtime.CompilerServices;

namespace Nalu.Maui.Test;

public class BindingTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyBindings")]
    private static extern void ReapplyBindings(BindableObject bindable);
    
    [Fact]
    public void TestReapplyBindingBehavior()
    {
        var label = new Label();
        var viewModel = new Label { Text = "Initial Value" };
        var binding = new Binding("Text", BindingMode.OneTime, source: viewModel);
        
        label.SetBinding(Label.TextProperty, binding);
        
        Assert.Equal("Initial Value", label.Text);
        
        viewModel.Text = "Updated Value";
        Assert.Equal("Initial Value", label.Text);
        
        ReapplyBindings(label);
        Assert.Equal("Updated Value", label.Text);
    }
}
