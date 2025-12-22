using Microsoft.Maui.Controls.Shapes;
using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class SoftKeyboardManagerPage : ContentPage
{
	public SoftKeyboardManagerPage(SoftKeyboardManagerPageModel pageModel)
    {
        BindingContext = pageModel;

		var grid = new Grid();
        grid.AddRowDefinition(new RowDefinition(GridLength.Auto));
        grid.AddRowDefinition(new RowDefinition(GridLength.Star));
        grid.AddRowDefinition(new RowDefinition(GridLength.Auto));

		var itemTemplate = new DataTemplate(() =>
		{
			var border = new Border
			{
				StrokeShape = new RoundRectangle { CornerRadius = 16 },
				Margin = new Thickness(16),
				Background = Brush.White
			};
			var vsl = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(0, 16) };
			border.Content = vsl;

			vsl.SetBinding(BindableLayout.ItemsSourceProperty, new Binding("Inputs"));
			BindableLayout.SetItemTemplateSelector(vsl, new MyDataTemplateSelector());

			return border;
		});
		var cv = new VirtualScroll {
			AutomationId = "CV",
			Adapter = new List<Items>
			{
				new(15),
				new(3),
				new(15),
				new(3),
				new(15),
				new(3),
				new(15),
				new(3),
			},
			ItemTemplate = itemTemplate
		};

        var button = new Button
                     {
                         Text = "Mode: Resize",
                         Margin = new Thickness(16, 8)
                     };

        button.SetBinding(IsVisibleProperty, new Binding(nameof(SoftKeyboardState.IsHidden), source: SoftKeyboardManager.State));
        button.Clicked += (_, _) =>
        {
            var mode = SoftKeyboardManager.GetSoftKeyboardAdjustMode(this);

            if (mode == SoftKeyboardAdjustMode.Pan)
            {
                SoftKeyboardManager.SetSoftKeyboardAdjustMode(this, SoftKeyboardAdjustMode.Resize);
                button.Text = "Mode: Resize";
            }
            else if (mode is null or SoftKeyboardAdjustMode.Resize)
            {
                SoftKeyboardManager.SetSoftKeyboardAdjustMode(this, SoftKeyboardAdjustMode.None);
                button.Text = "Mode: None";
            }
            else
            {
                SoftKeyboardManager.SetSoftKeyboardAdjustMode(this, SoftKeyboardAdjustMode.Pan);
                button.Text = "Mode: Pan";
            }
        };
        
        var stickyEntry = new Entry
        {
            AutomationId = "StickyEntry",
            Placeholder = "Sticky Entry",
            Margin = new Thickness(16, 8),
            FontSize = 18,
            BackgroundColor = Colors.LightGoldenrodYellow
        };
        
        SoftKeyboardManager.SetSoftKeyboardAdjustMode(stickyEntry, SoftKeyboardAdjustMode.Pan);

        grid.Add(button, row: 0);
        grid.Add(cv, row: 1);
        grid.Add(stickyEntry, row: 2);

		Content = grid;
		Background = new SolidColorBrush(Color.Parse("#3C64BC"));
	}

	private partial class MyDataTemplateSelector : DataTemplateSelector
	{
		private readonly DataTemplate _entryTemplate = new(() =>
		{
			var entry = new Entry
			{
				FontSize = 18,
				HeightRequest = 40,
				BackgroundColor = Colors.LightGoldenrodYellow,
				Margin = new Thickness(16, 0)
			};

			entry.SetBinding(Entry.PlaceholderProperty, new Binding("Label"));
			entry.SetBinding(Element.AutomationIdProperty, new Binding("Label"));
			entry.SetBinding(Entry.TextProperty, new Binding("Content", BindingMode.TwoWay));
			return entry;
		});
		
		private readonly DataTemplate _editorTemplate = new(() =>
		{
			var entry = new AutoSizedEditor
			{
				FontSize = 18,
				BackgroundColor = Colors.LightGoldenrodYellow,
				Margin = new Thickness(16, 0)
			};

			entry.SetBinding(Editor.PlaceholderProperty, new Binding("Label"));
			entry.SetBinding(Element.AutomationIdProperty, new Binding("Label"));
			entry.SetBinding(Editor.TextProperty, new Binding("Content", BindingMode.TwoWay));

			return entry;
		});

		protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
		{
			var input = (Input)item;

			if (input.Number % 2 == 1)
			{
				return _editorTemplate;
			}
			
			return _entryTemplate;
		}
	}

	private class AutoSizedEditor : Editor
	{
		public AutoSizedEditor()
		{
			AutoSize = EditorAutoSizeOption.TextChanges;
		}

		protected override void OnHandlerChanged()
		{
			base.OnHandlerChanged();
#if IOS
			if (Handler?.PlatformView is UIKit.UITextView textView)
			{
				textView.ScrollEnabled = false;
			}
#endif
		}
	}

	private record Input(
		string Label,
		int Number,
		string Content
	);

	private class Items
	{
		private static int _idCounter;

		public List<Input> Inputs { get; }

		public Items(int count)
		{
			Inputs = [];
			for (var i = 0; i < count; i++)
			{
				Inputs.Add(new Input($"Input{++_idCounter}", _idCounter, string.Empty));
			}
		}
	}
}
