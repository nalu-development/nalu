using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PopupModels;

public class CanLeavePopupModel : ObservableObject
{
    public string Text { get; set; } = "Do you want to leave the page?";
}
