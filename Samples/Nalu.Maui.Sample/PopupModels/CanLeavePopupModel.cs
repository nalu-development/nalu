namespace Nalu.Maui.Sample.PopupModels;

using CommunityToolkit.Mvvm.ComponentModel;

public class CanLeavePopupModel : ObservableObject
{
    public string Text { get; set; } = "Do you want to leave the page?";
}
