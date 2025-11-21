using Nalu.Maui.Sample.PopupModels;

namespace Nalu.Maui.Sample.Popups;

public partial class CanLeavePopup
{
    public CanLeavePopup(CanLeavePopupModel canLeavePopupModel)
    {
        BindingContext = canLeavePopupModel;
        InitializeComponent();
    }

    private void YesOnClicked(object? sender, EventArgs e) => CloseAsync(true);

    private void NoOnClicked(object? sender, EventArgs e) => CloseAsync(false);
}
