namespace Nalu.Maui.Sample.Popups;

using PopupModels;

public partial class CanLeavePopup
{
    public CanLeavePopup(CanLeavePopupModel canLeavePopupModel)
    {
        BindingContext = canLeavePopupModel;
        InitializeComponent();
    }

    private void YesOnClicked(object? sender, EventArgs e) => Close(true);

    private void NoOnClicked(object? sender, EventArgs e) => Close(false);
}

