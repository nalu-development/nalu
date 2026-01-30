using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

/// <summary>
/// Base class for all items in the flattened friends list.
/// </summary>
public abstract class FriendListItemBase : ObservableObject
{
    /// <summary>
    /// Reference to the parent group for operations.
    /// </summary>
    public FriendGroup Group { get; }

    protected FriendListItemBase(FriendGroup group)
    {
        Group = group;
    }
}

/// <summary>
/// Header item for a friend group - displays the group name in bold.
/// </summary>
public partial class FriendGroupHeader : FriendListItemBase
{
    [ObservableProperty]
    public partial string GroupName { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    public FriendGroupHeader(FriendGroup group, string groupName) : base(group)
    {
        GroupName = groupName;
    }

    [RelayCommand]
    private void StartEditing()
    {
        IsEditing = true;
    }

    [RelayCommand]
    private void SaveGroupName()
    {
        IsEditing = false;
    }
}

/// <summary>
/// Individual friend item with checkbox, editable name, and remove button.
/// </summary>
public partial class FriendItem : FriendListItemBase
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    public FriendItem(FriendGroup group, string name) : base(group)
    {
        Name = name;
    }

    [RelayCommand]
    private void StartEditing()
    {
        IsEditing = true;
    }

    [RelayCommand]
    private void SaveName()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private void Remove()
    {
        Group.RemoveFriend(this);
    }
}

/// <summary>
/// Footer item that shows "Add entry" button for each group.
/// </summary>
public partial class AddFriendButton : FriendListItemBase
{
    public AddFriendButton(FriendGroup group) : base(group)
    {
    }

    [RelayCommand]
    private void AddFriend()
    {
        Group.AddNewFriend();
    }
}

/// <summary>
/// Logical grouping of friends (not directly displayed, used for management).
/// </summary>
public class FriendGroup
{
    private readonly NinePageModel _pageModel;
    private int _friendCounter;

    public FriendGroupHeader Header { get; }
    public List<FriendItem> Friends { get; } = [];
    public AddFriendButton AddButton { get; }

    public FriendGroup(NinePageModel pageModel, string groupName, IEnumerable<string> friendNames)
    {
        _pageModel = pageModel;
        Header = new FriendGroupHeader(this, groupName);
        AddButton = new AddFriendButton(this);

        foreach (var name in friendNames)
        {
            Friends.Add(new FriendItem(this, name));
            _friendCounter++;
        }
    }

    public void AddNewFriend()
    {
        var newFriend = new FriendItem(this, $"New Friend {++_friendCounter}");
        Friends.Add(newFriend);
        _pageModel.InsertFriendIntoFlatList(this, newFriend);
    }

    public void RemoveFriend(FriendItem friend)
    {
        Friends.Remove(friend);
        _pageModel.RemoveFriendFromFlatList(friend);
    }
}

public sealed class FriendListAdapter : VirtualScrollObservableCollectionAdapter<FriendListItemBase>
{
    public FriendListAdapter(ObservableCollection<FriendListItemBase> collection) : base(collection)
    {
    }

    public override void OnDragInitiating(VirtualScrollDragInfo dragInfo)
    {
        if (dragInfo.Item is FriendGroupHeader)
        {
            // Remove all FriendItems and AddFriendButtons from the collection temporarily
            // to allow reordering groups. We iterate backwards to avoid index shifting issues.
            for (var i = Collection.Count - 1; i >= 0; i--)
            {
                if (Collection[i] is FriendItem or AddFriendButton)
                {
                    Collection.RemoveAt(i);
                }
            }
        }
    }

    public override void OnDragEnded(VirtualScrollDragInfo dragInfo)
    {
        if (dragInfo.Item is FriendGroupHeader)
        {
            // Re-add all FriendItems and AddFriendButtons back to the collection
            // Structure: Header → Friends → AddButton for each group
            for (var i = 0; i < Collection.Count; i++)
            {
                if (Collection[i] is FriendGroupHeader header)
                {
                    var insertIndex = i + 1;
                    
                    // Insert all friends after the header
                    foreach (var friend in header.Group.Friends)
                    {
                        Collection.Insert(insertIndex++, friend);
                        i++;
                    }
                    
                    // Insert the add button after the friends
                    Collection.Insert(insertIndex, header.Group.AddButton);
                    i++;
                }
            }
        }
    }

    public override bool CanDragItem(VirtualScrollDragInfo dragInfo) => dragInfo.Item is FriendItem or FriendGroupHeader;

    public override bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo)
    {
        var item = Collection[dragDropInfo.DestinationItemIndex];

        return dragDropInfo.Item is FriendItem ? item is FriendItem : item is FriendGroupHeader;
    }
}

public partial class NinePageModel : ObservableObject
{
    private static int _instanceCount;
    private int _groupCounter;

    public string Message { get; } = "Friends Groups - Using DataTemplateSelector";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    /// <summary>
    /// The flattened collection containing headers, friends, and add buttons.
    /// </summary>
    public ObservableCollection<FriendListItemBase> Items { get; } = [];

    /// <summary>
    /// The adapter for the VirtualScroll.
    /// </summary>
    public IReorderableVirtualScrollAdapter Adapter { get; }

    /// <summary>
    /// All friend groups for management.
    /// </summary>
    private readonly List<FriendGroup> _groups = [];

    public NinePageModel()
    {
        // Create initial groups
        AddGroup("Family", ["Mom", "Dad", "Sister", "Brother"]);
        AddGroup("Work", ["Alice", "Bob", "Charlie"]);
        AddGroup("School", ["David", "Emma", "Frank", "Grace"]);
        AddGroup("Sports", ["Hannah", "Ian"]);
        AddGroup("Fishing", ["Jeremia", "Jeremia"]);
        AddGroup("Gaming", ["Kevin", "Liam", "Mia"]);
        AddGroup("Climbing", ["Joe", "Mason"]);
        AddGroup("Travel", ["Noah", "Olivia", "Pam"]);
        AddGroup("Sleeping", ["James", "Johanna"]);
        AddGroup("Bird Watching", ["Quinn", "Rachel"]);
        AddGroup("Cooking", ["Sophia", "Thomas", "Uma"]);
        AddGroup("Hiking", ["Victor", "Wendy", "Xander", "Yara", "Zoe"]);
        AddGroup("Photography", ["Aaron", "Bella"]);
        AddGroup("Music", ["Carter", "Diana", "Ethan"]);

        Adapter = new FriendListAdapter(Items);
    }

    private void AddGroup(string groupName, IEnumerable<string> friendNames)
    {
        var group = new FriendGroup(this, groupName, friendNames);
        _groups.Add(group);
        _groupCounter++;

        // Add to flat list: Header, Friends, AddButton
        Items.Add(group.Header);
        foreach (var friend in group.Friends)
        {
            Items.Add(friend);
        }
        Items.Add(group.AddButton);
    }

    /// <summary>
    /// Called by FriendGroup when a new friend is added.
    /// </summary>
    internal void InsertFriendIntoFlatList(FriendGroup group, FriendItem friend)
    {
        // Find the AddButton for this group and insert before it
        var addButtonIndex = Items.IndexOf(group.AddButton);
        if (addButtonIndex >= 0)
        {
            Items.Insert(addButtonIndex, friend);
        }
    }

    /// <summary>
    /// Called by FriendGroup when a friend is removed.
    /// </summary>
    internal void RemoveFriendFromFlatList(FriendItem friend)
    {
        Items.Remove(friend);
    }

    [RelayCommand]
    private void AddNewGroup()
    {
        AddGroup($"Group {++_groupCounter}", [$"Friend 1"]);
    }
}
