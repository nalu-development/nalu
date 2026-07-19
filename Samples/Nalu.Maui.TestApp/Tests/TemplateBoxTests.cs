using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Template Box Tests")]
public class TemplateBoxTestsPage : ContentPage
{
    private sealed class Person(string name)
    {
        public string Name => name;
    }

    private sealed class SelectorItem(bool isEven, string name)
    {
        public bool IsEven => isEven;
        public string Name => name;
    }

    private sealed class TemplateBoxTestsModel : INotifyPropertyChanged
    {
        private Person _currentPerson = new("Alice");
        private SelectorItem _selectorItem = new(true, "Even item");

        public Person CurrentPerson
        {
            get => _currentPerson;
            set
            {
                _currentPerson = value;
                OnPropertyChanged();
            }
        }

        public SelectorItem SelectorItem
        {
            get => _selectorItem;
            set
            {
                _selectorItem = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class EvenOddTemplateSelector : DataTemplateSelector
    {
        public required DataTemplate EvenTemplate { get; init; }
        public required DataTemplate OddTemplate { get; init; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => ((SelectorItem) item).IsEven ? EvenTemplate : OddTemplate;
    }

    public TemplateBoxTestsPage()
    {
        var model = new TemplateBoxTestsModel();
        BindingContext = model;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        // --- ContentTemplate + ContentBindingContext + template swap ------------------------
        var templateA = new DataTemplate(() =>
            {
                var label = new Label { AutomationId = "TemplateALabel" };
                label.SetBinding(Label.TextProperty, nameof(Person.Name));

                return label;
            }
        );

        var templateB = new DataTemplate(() =>
            {
                var label = new Label { AutomationId = "TemplateBLabel" };
                label.SetBinding(Label.TextProperty, new Binding(nameof(Person.Name), stringFormat: "B:{0}"));

                return label;
            }
        );

        var templateBox = new TemplateBox { ContentTemplate = templateA };
        templateBox.SetBinding(TemplateBox.ContentBindingContextProperty, nameof(TemplateBoxTestsModel.CurrentPerson));

        var switchPersonButton = new Button { Text = "Switch person", AutomationId = "SwitchPersonButton" };
        switchPersonButton.Clicked += (_, _) =>
            model.CurrentPerson = model.CurrentPerson.Name == "Alice" ? new Person("Bob") : new Person("Alice");

        var swapTemplateButton = new Button { Text = "Swap template", AutomationId = "SwapTemplateButton" };
        swapTemplateButton.Clicked += (_, _) => templateBox.ContentTemplate = templateB;

        stack.Add(switchPersonButton);
        stack.Add(swapTemplateButton);
        stack.Add(templateBox);

        // --- DataTemplateSelector -----------------------------------------------------------
        var selector = new EvenOddTemplateSelector
        {
            EvenTemplate = new DataTemplate(() =>
                {
                    var label = new Label { AutomationId = "EvenTemplateLabel" };
                    label.SetBinding(Label.TextProperty, nameof(SelectorItem.Name));

                    return label;
                }
            ),
            OddTemplate = new DataTemplate(() =>
                {
                    var label = new Label { AutomationId = "OddTemplateLabel" };
                    label.SetBinding(Label.TextProperty, nameof(SelectorItem.Name));

                    return label;
                }
            )
        };

        var selectorBox = new TemplateBox { ContentTemplate = selector };
        selectorBox.SetBinding(TemplateBox.ContentBindingContextProperty, nameof(TemplateBoxTestsModel.SelectorItem));

        var switchSelectorButton = new Button { Text = "Switch selector item", AutomationId = "SwitchSelectorModelButton" };
        switchSelectorButton.Clicked += (_, _) =>
            model.SelectorItem = model.SelectorItem.IsEven ? new SelectorItem(false, "Odd item") : new SelectorItem(true, "Even item");

        stack.Add(switchSelectorButton);
        stack.Add(selectorBox);

        // --- TemplateContentPresenter (content projection) ----------------------------------
        var projectionBox = new TemplateBox
        {
            TemplateContent = new Label { Text = "I'm here!", AutomationId = "ProjectedLabel" },
            ContentTemplate = new DataTemplate(() => new HorizontalStackLayout
                {
                    Children =
                    {
                        new Label { Text = "Projected => ", AutomationId = "ProjectionPrefixLabel" },
                        new TemplateContentPresenter()
                    }
                }
            )
        };

        stack.Add(projectionBox);

        Content = new ScrollView { Content = stack };
    }
}
