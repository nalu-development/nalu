using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class ExpanderPageModel : ObservableObject
{
    private static int _instanceCount;
    private int _itemIdCounter;

    public string Message { get; } = "Expander VirtualScroll Demo";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public ObservableCollection<ExpanderItem> Items { get; }

    public ExpanderPageModel()
    {
        Items = new ObservableCollection<ExpanderItem>(
            Enumerable.Range(1, 20).Select(CreateItem)
        );
    }

    private ExpanderItem CreateItem(int index)
    {
        var description = GenerateRandomLongText();
        return new ExpanderItem($"Item {++_itemIdCounter}", description)
        {
            IsExpanded = index % 3 == 0 // Every third item starts expanded
        };
    }

    private string GenerateRandomLongText()
    {
        var random = Random.Shared;
        var sentences = new[]
        {
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
            "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
            "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.",
            "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore.",
            "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
            "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.",
            "Totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.",
            "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit.",
            "Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.",
            "Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur.",
            "Vel illum qui dolorem eum fugiat quo voluptas nulla pariatur.",
            "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti.",
            "Quos dolores et quas molestias excepturi sint occaecati cupiditate non provident.",
            "Similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga.",
            "Et harum quidem rerum facilis est et expedita distinctio nam libero tempore.",
            "Cum soluta nobis est eligendi optio cumque nihil impedit quo minus id quod maxime placeat facere possimus.",
            "Omnis voluptas assumenda est, omnis dolor repellendus temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus.",
            "Saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae itaque earum rerum hic tenetur a sapiente delectus.",
            "Ut aut reiciendis voluptatibus maiores alias consequatur aut perferendis doloribus asperiores repellat.",
            "This is a demonstration of expandable content with varying text lengths to showcase the ExpanderViewBox functionality."
        };

        // Generate between 3 and 15 sentences randomly
        var sentenceCount = random.Next(3, 16);
        var selectedSentences = sentences.OrderBy(_ => random.Next()).Take(sentenceCount);
        
        return string.Join(" ", selectedSentences);
    }

    [RelayCommand]
    private void AddItem()
    {
        var randomIndex = Items.Count > 0 ? Random.Shared.Next(Items.Count + 1) : 0;
        Items.Insert(randomIndex, CreateItem(Items.Count + 1));
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (Items.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Items.Count);
            Items.RemoveAt(randomIndex);
        }
    }
}

/// <summary>
/// Represents an expandable item in the VirtualScroll.
/// </summary>
public partial class ExpanderItem : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isExpanded;

    public ExpanderItem(string name, string description)
    {
        _name = name;
        _description = description;
        _isExpanded = false;
    }

    [RelayCommand]
    private void Toggle()
    {
        IsExpanded = !IsExpanded;
    }
}

