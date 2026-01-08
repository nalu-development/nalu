using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class NinePage : ContentPage
{
    public NinePage(NinePageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

/// <summary>
/// Template selector for the flattened friend list items.
/// </summary>
public class FriendListTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GroupHeaderTemplate { get; set; }
    public DataTemplate? FriendItemTemplate { get; set; }
    public DataTemplate? AddFriendButtonTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            FriendGroupHeader => GroupHeaderTemplate!,
            FriendItem => FriendItemTemplate!,
            AddFriendButton => AddFriendButtonTemplate!,
            _ => throw new InvalidOperationException($"Unknown item type: {item.GetType()}")
        };
    }
}
