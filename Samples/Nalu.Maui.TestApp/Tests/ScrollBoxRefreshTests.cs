using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Scroll Box Refresh Tests")]
public class ScrollBoxRefreshTests : ContentPage
{
    private readonly ScrollBox _scrollBox;
    private readonly Label _refreshCountLabel;
    private readonly Label _isRefreshingLabel;
    private Action? _pendingCompletion;
    private int _refreshCount;

    public ScrollBoxRefreshTests()
    {
        _refreshCountLabel = new Label { AutomationId = "RefreshCountLabel", FontSize = 12, Text = "Refreshes: 0" };
        _isRefreshingLabel = new Label { AutomationId = "IsRefreshingLabel", FontSize = 12, Text = "False" };

        var stack = new VerticalStackLayout();

        for (var i = 1; i <= 30; i++)
        {
            stack.Add(new Label
                {
                    Text = $"R{i}",
                    AutomationId = $"RItem{i}",
                    FontSize = 16,
                    HeightRequest = 44,
                    Margin = new Thickness(16, 0)
                }
            );
        }

        _scrollBox = new ScrollBox
        {
            AutomationId = "RefreshScrollBox",
            IsRefreshEnabled = true,
            RefreshAccentColor = Colors.OrangeRed,
            Content = stack
        };

        _scrollBox.OnRefresh += (_, args) =>
        {
            _refreshCount++;
            _refreshCountLabel.Text = $"Refreshes: {_refreshCount}";

            // Completion is test-driven: the harness completes it via CompleteRefreshButton.
            _pendingCompletion = args.Complete;
        };

        _scrollBox.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScrollBox.IsRefreshing))
            {
                _isRefreshingLabel.Text = _scrollBox.IsRefreshing.ToString();
            }
        };

        // The user pull gesture cannot be synthesized (no touch physics in agent gestures):
        // this drives the SAME controller pipeline a platform pull goes through.
        var pullButton = new Button { Text = "Simulate pull", AutomationId = "SimulatePullButton", FontSize = 11 };
        pullButton.Clicked += (_, _) => ((IScrollBoxController) _scrollBox).Refresh(() => { });

        var showButton = new Button { Text = "Show spinner", AutomationId = "ShowRefreshButton", FontSize = 11 };
        showButton.Clicked += (_, _) => _scrollBox.IsRefreshing = true;

        var completeButton = new Button { Text = "Complete", AutomationId = "CompleteRefreshButton", FontSize = 11 };

        completeButton.Clicked += (_, _) =>
        {
            if (_pendingCompletion is { } completion)
            {
                _pendingCompletion = null;
                completion();
            }
            else
            {
                _scrollBox.IsRefreshing = false;
            }
        };

        var controls = new HorizontalWrapLayout
                       {
                           pullButton,
                           showButton,
                           completeButton,
                           _refreshCountLabel,
                           _isRefreshingLabel
                       };
        controls.HorizontalSpacing = 8;
        controls.VerticalSpacing = 4;
        controls.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(120), new RowDefinition(GridLength.Star)]
                   };
        grid.Add(controls);
        grid.Add(_scrollBox, 0, 1);

        Content = grid;
    }
}
