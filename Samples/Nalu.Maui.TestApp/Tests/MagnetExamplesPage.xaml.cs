using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Gallery of the documentation examples (conceptual_docs/layouts-magnet-examples.md): shows one example at a time
/// so the docs screenshots can be captured per-element (see MagnetExamplesShotsTests).
/// </summary>
[UsedImplicitly]
[TestPage("Magnet Examples")]
public partial class MagnetExamplesPage : ContentPage
{
    private static readonly string[] _titles =
    [
        "01 · List row",
        "02 · Login screen",
        "03 · Header bar",
        "04 · Form with barrier",
        "05 · Chain styles",
        "06 · Weighted columns",
        "07 · Media card (Ratio)",
        "08 · Guideline split",
        "09 · Visibility & gone margins",
        "10 · Packed chain + measured",
        "11 · Chain vertical centering",
        "12 · Gap modes under collapse"
    ];

    public MagnetExamplesPage()
    {
        InitializeComponent();
        ShowExample(1);
    }

    private void OnShowExample(object? sender, EventArgs e)
    {
        if (int.TryParse(ExampleSelector.Text, out var index))
        {
            ShowExample(index);
        }
    }

    private void ShowExample(int index)
    {
        var automationId = $"Example{index:00}";

        foreach (var child in ExamplesHost.Children)
        {
            if (child is Border border)
            {
                border.IsVisible = border.AutomationId == automationId;
            }
        }

        ExampleTitle.Text = index >= 1 && index <= _titles.Length ? _titles[index - 1] : "";
    }
}
