using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for the full ScrollTo combination matrix: items, section headers (itemIndex -1)
/// and the global header/footer sentinels, each with Start/Center/End/MakeVisible.
/// Fixed element heights keep the geometry assertions deterministic:
/// global header/footer 40, section headers 30, items 50, section footers 24.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll ScrollTo Tests")]
public class VirtualScrollScrollToTests : ContentPage
{
    private readonly VirtualScroll _virtualScroll;
    private readonly Entry _targetEntry;
    private readonly Entry _positionEntry;

    public VirtualScrollScrollToTests()
    {
        // 5 sections × 5 items.
        var sections = new ObservableCollection<VirtualScrollSection>(
            Enumerable.Range(0, 5).Select(s => new VirtualScrollSection($"{s}", Enumerable.Range(0, 5).Select(i => new VirtualScrollListItem($"S{s}I{i}"))))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "STScroll",
                             ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(sections, s => s.Items),

                             HeaderTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "GHeader",
                                     Text = "Global header",
                                     HeightRequest = 40,
                                     BackgroundColor = Colors.Gold,
                                     HorizontalTextAlignment = TextAlignment.Center,
                                     VerticalTextAlignment = TextAlignment.Center
                                 }
                             ),

                             FooterTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "GFooter",
                                     Text = "Global footer",
                                     HeightRequest = 40,
                                     BackgroundColor = Colors.Silver,
                                     HorizontalTextAlignment = TextAlignment.Center,
                                     VerticalTextAlignment = TextAlignment.Center
                                 }
                             ),

                             SectionHeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     HeightRequest = 30,
                                                     FontAttributes = FontAttributes.Bold,
                                                     BackgroundColor = Colors.LightGray,
                                                     Padding = new Thickness(10, 0),
                                                     VerticalTextAlignment = TextAlignment.Center
                                                 };
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "Section {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "SH{0}"));

                                     return label;
                                 }
                             ),

                             SectionFooterTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     HeightRequest = 24,
                                                     FontAttributes = FontAttributes.Italic,
                                                     BackgroundColor = Colors.WhiteSmoke,
                                                     Padding = new Thickness(10, 0),
                                                     VerticalTextAlignment = TextAlignment.Center
                                                 };
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "End {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "SF{0}"));

                                     return label;
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     HeightRequest = 50,
                                                     FontSize = 14,
                                                     Padding = new Thickness(16, 0),
                                                     VerticalTextAlignment = TextAlignment.Center
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        _targetEntry = new Entry { Placeholder = "gh | gf | s:i | s:h", AutomationId = "TargetEntry", MinimumWidthRequest = 90 };
        _positionEntry = new Entry { Placeholder = "Position", AutomationId = "PositionEntry", MinimumWidthRequest = 90 };

        var scrollToButton = new Button { Text = "ScrollTo", AutomationId = "ScrollToButton", FontSize = 12 };
        scrollToButton.Clicked += (_, _) => DoScrollTo(animated: false);
        var scrollToAnimatedButton = new Button { Text = "Animated", AutomationId = "ScrollToAnimatedButton", FontSize = 12 };
        scrollToAnimatedButton.Clicked += (_, _) => DoScrollTo(animated: true);

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _targetEntry,
                                 _positionEntry,
                                 scrollToButton,
                                 scrollToAnimatedButton
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    /// <summary>
    /// Target syntax: "gh" (global header), "gf" (global footer),
    /// "s:i" (item i of section s), "s:h" (header of section s).
    /// </summary>
    private void DoScrollTo(bool animated)
    {
        var target = _targetEntry.Text?.Trim().ToLowerInvariant();
        var position = Enum.TryParse<ScrollToPosition>(_positionEntry.Text?.Trim(), true, out var p) ? p : ScrollToPosition.MakeVisible;

        switch (target)
        {
            case null or "":
                return;

            case "gh":
                _virtualScroll.ScrollTo(VirtualScrollRange.GlobalHeaderSectionIndex, 0, position, animated);

                break;

            case "gf":
                _virtualScroll.ScrollTo(VirtualScrollRange.GlobalFooterSectionIndex, 0, position, animated);

                break;

            default:
            {
                var parts = target.Split(':');

                if (parts.Length == 2 && int.TryParse(parts[0], out var section))
                {
                    var item = parts[1] == "h" ? -1 : int.TryParse(parts[1], out var i) ? i : 0;
                    _virtualScroll.ScrollTo(section, item, position, animated);
                }

                break;
            }
        }
    }
}
