using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class GroupedDynamicDataPage : ContentPage
{
    public GroupedDynamicDataPage(GroupedDynamicDataPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void VirtualScroll_OnScrolled(object? sender, VirtualScrollScrolledEventArgs e)
    {
        var range = VirtualScroll.GetVisibleItemsRange();

        if (range.HasValue)
        {
            var r = range.Value;
            var startType = GetPositionType(r.StartSectionIndex, r.StartItemIndex);
            var endType = GetPositionType(r.EndSectionIndex, r.EndItemIndex);

            RangeInfoLabel.Text = $"Visible: {startType} → {endType}";
        }
        else
        {
            RangeInfoLabel.Text = "Visible Range: None";
        }
    }

    private static string GetPositionType(int sectionIndex, int itemIndex)
    {
        if (sectionIndex == VirtualScrollRange.GlobalHeaderSectionIndex)
            return "GlobalHeader";
        if (sectionIndex == VirtualScrollRange.GlobalFooterSectionIndex)
            return "GlobalFooter";
        if (itemIndex == VirtualScrollRange.SectionHeaderItemIndex)
            return $"SectionHeader[{sectionIndex}]";
        if (itemIndex == VirtualScrollRange.SectionFooterItemIndex)
            return $"SectionFooter[{sectionIndex}]";
        return $"Item[{sectionIndex},{itemIndex}]";
    }
}
