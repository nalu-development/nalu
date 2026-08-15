using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class KeyboardScrollFormPageModel : ObservableObject;

[UsedImplicitly]
public class KeyboardVirtualFormPageModel : ObservableObject;

/// <summary>Shared chrome of the keyboard-content harness pages: page keyboard-mode switches + hide button + probes.</summary>
file static class KeyboardContentChrome
{
    public static View Build(Page page, string marker, Func<Task> hideKeyboardAsync, Action? scrollToEnd = null)
    {
        var modeLabel = new Label { AutomationId = $"{marker}Mode", Text = "page:Resize", FontSize = 11, VerticalOptions = LayoutOptions.Center };
        var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center, Padding = new Thickness(16, 4) };

        foreach (var mode in new[] { ScaffoldKeyboardMode.Resize, ScaffoldKeyboardMode.Pan, ScaffoldKeyboardMode.None })
        {
            var button = new Button { Text = mode.ToString(), AutomationId = $"{marker}Mode{mode}Button", FontSize = 11, Padding = new Thickness(8, 4) };
            button.Clicked += (_, _) =>
            {
                Scaffold.SetKeyboardMode(page, mode);
                modeLabel.Text = $"page:{mode}";
            };
            row.Add(button);
        }

        var hide = new Button { Text = "Hide", AutomationId = $"{marker}HideButton", FontSize = 11, Padding = new Thickness(8, 4) };
        hide.Clicked += async (_, _) => await hideKeyboardAsync();
        row.Add(hide);

        // "Types" a newline into the focused input the way the keyboard does (native insert at the
        // caret), so caret-following behavior can be exercised without a hardware keyboard.
        var addLine = new Button { Text = "AddLine", AutomationId = $"{marker}AddLineButton", FontSize = 11, Padding = new Thickness(8, 4) };
        addLine.Clicked += (_, _) =>
        {
            var focused = page.GetVisualTreeDescendants().OfType<InputView>().FirstOrDefault(v => v.IsFocused);
            switch (focused?.Handler?.PlatformView)
            {
#if IOS
                case UIKit.UITextView textView:
                    textView.InsertText("\n");
                    break;
                case UIKit.UITextField textField:
                    textField.InsertText("x");
                    break;
#elif ANDROID
                case Android.Widget.EditText editText when editText.EditableText is { } editable:
                    editable.Insert(Math.Max(0, editText.SelectionEnd), new Java.Lang.String((editText.InputType & Android.Text.InputTypes.TextFlagMultiLine) != 0 ? "\n" : "x"));
                    break;
#endif
            }
        };
        row.Add(addLine);

        if (scrollToEnd is not null)
        {
            var toEnd = new Button { Text = "ToEnd", AutomationId = $"{marker}ToEndButton", FontSize = 11, Padding = new Thickness(8, 4) };
            toEnd.Clicked += (_, _) => scrollToEnd();
            row.Add(toEnd);
        }

        row.Add(modeLabel);
        row.Add(SoftKeyboardProbe.CreateLabel($"{marker}KeyboardProbe"));
        row.Add(SoftKeyboardProbe.CreateHeightLabel($"{marker}KeyboardHeight"));

        var exit = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed, Padding = new Thickness(8, 4) };
        exit.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        row.Add(exit);

        return row;
    }

    public static async Task HideAsync(params InputView[] inputs)
    {
        foreach (var input in inputs)
        {
            input.Unfocus();
            await input.HideSoftInputAsync(CancellationToken.None);
        }
    }
}

/// <summary>
/// A page whose content is a ScrollView form: filler, a single-line entry and a MULTILINE,
/// auto-sizing editor near the bottom, more filler below. Focusing the editor and typing lines
/// (the editor grows) exercises caret visibility under the page's keyboard policy.
/// </summary>
[UsedImplicitly]
public class KeyboardScrollFormPage : ContentPage
{
    public KeyboardScrollFormPage(KeyboardScrollFormPageModel model)
    {
        BindingContext = model;
        Title = "ScrollForm";

        var entry = new Entry { AutomationId = "KbScrollEntry", Placeholder = "entry near the bottom", FontSize = 14 };
        var editor = new Editor { AutomationId = "KbScrollEditor", Placeholder = "multiline editor (auto-size)", FontSize = 14, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60 };

        var stack = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 16) };
        stack.Add(new Label { Text = "Scroll form", AutomationId = "KeyboardScrollFormPage", FontSize = 22, FontAttributes = FontAttributes.Bold });

        for (var i = 0; i < 24; i++)
        {
            stack.Add(new Label { Text = $"Filler {i}", FontSize = 12 });
        }

        stack.Add(entry);
        stack.Add(editor);

        for (var i = 0; i < 3; i++)
        {
            stack.Add(new Label { Text = $"Trailer {i}", FontSize = 12 });
        }

        var scrollView = new ScrollView { AutomationId = "KbScrollScroll", Content = stack };
        var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        grid.Add(KeyboardContentChrome.Build(this, "KbScroll", () => KeyboardContentChrome.HideAsync(entry, editor), () => _ = scrollView.ScrollToAsync(0, Math.Max(0, scrollView.ContentSize.Height - scrollView.Height), false)));
        grid.Add(scrollView, 0, 1);

        Content = grid;
    }
}

public sealed class KeyboardVirtualItem(int index, bool isEditor)
{
    public string Name { get; } = isEditor ? "Editor" : $"Row {index}";
    public string InputId { get; } = isEditor ? "KbVirtualEditor" : $"KbVirtualEntry{index}";
    public bool IsEditor { get; } = isEditor;
}

/// <summary>Same shapes inside a VirtualScroll: 30 rows with an entry each, then a MULTILINE auto-sizing editor row.</summary>
[UsedImplicitly]
public class KeyboardVirtualFormPage : ContentPage
{
    private sealed class ItemSelector : DataTemplateSelector
    {
        private readonly DataTemplate _entry = new(() =>
        {
            var label = new Label { WidthRequest = 72, VerticalOptions = LayoutOptions.Center, FontSize = 12 };
            label.SetBinding(Label.TextProperty, nameof(KeyboardVirtualItem.Name));
            var entry = new Entry { FontSize = 14, Placeholder = "type here" };
            entry.SetBinding(AutomationIdProperty, nameof(KeyboardVirtualItem.InputId));

            var cell = new Grid { Padding = new Thickness(16, 4), ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
            cell.Add(label);
            cell.Add(entry, 1);

            return cell;
        });

        private readonly DataTemplate _editor = new(() =>
        {
            var editor = new Editor { FontSize = 14, Placeholder = "multiline editor (auto-size)", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60 };
            editor.SetBinding(AutomationIdProperty, nameof(KeyboardVirtualItem.InputId));

            return new Grid { Padding = new Thickness(16, 4), Children = { editor } };
        });

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => item is KeyboardVirtualItem { IsEditor: true } ? _editor : _entry;
    }

    public KeyboardVirtualFormPage(KeyboardVirtualFormPageModel model)
    {
        BindingContext = model;
        Title = "VirtualForm";

        var items = new ObservableCollection<KeyboardVirtualItem>(Enumerable.Range(1, 30).Select(i => new KeyboardVirtualItem(i, false)));
        items.Add(new KeyboardVirtualItem(31, true));

        var virtualScroll = new VirtualScroll
        {
            AutomationId = "KbVirtualScroll",
            ItemsSource = items,
            ItemTemplate = new ItemSelector()
        };

        Func<Task> hide = async () =>
        {
            // Whatever cell input holds the focus.
            foreach (var input in virtualScroll.GetVisualTreeDescendants().OfType<InputView>().Where(v => v.IsFocused).ToArray())
            {
                await KeyboardContentChrome.HideAsync(input);
            }
        };

        var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        grid.Add(KeyboardContentChrome.Build(this, "KbVirtual", hide, () => virtualScroll.ScrollTo(0, items.Count - 1, ScrollToPosition.End, animated: false)));
        grid.Add(new Label { Text = "Virtual form", AutomationId = "KeyboardVirtualFormPage", FontSize = 22, FontAttributes = FontAttributes.Bold, Margin = new Thickness(16, 4) }, 0, 1);
        grid.Add(virtualScroll, 0, 2);

        Content = grid;
    }
}

/// <summary>Keyboard-vs-content harness: text inputs (single and multiline) inside a ScrollView and inside a VirtualScroll, under each page keyboard mode.</summary>
[UsedImplicitly]
[TestPage("Scaffold Keyboard Content Tests")]
public class KeyboardContentScaffold : Scaffold
{
    public KeyboardContentScaffold()
    {
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "ScrollForm", PageType = typeof(KeyboardScrollFormPage) },
                    new ScaffoldRoot { Title = "VirtualForm", PageType = typeof(KeyboardVirtualFormPage) }
                }
            }
        );
    }
}
