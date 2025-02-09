using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nalu.Maui.Weather.Views;

using Resources;

public partial class AirQualityCard
{
    public AirQualityCard()
    {
        InitializeComponent();
    }

    private void ToggleExpander(object? sender, EventArgs e)
    {
        Expander.IsExpanded = !Expander.IsExpanded;
        ExpanderButton.Text = Expander.IsExpanded ? Texts.ShowLess : Texts.ShowMore;
    }
}

