using DynamicData;
using Nalu.Maui.DailyHelper.Models;
using System.Reactive.Linq;
using System.Text.Json;

namespace Nalu.Maui.DailyHelper.Services;

/// <summary>
/// Centralized to-do state: a <see cref="SourceCache{TObject,TKey}"/> every page (and the tab
/// badge) subscribes to. Items are immutable records — all mutations are cache updates, and
/// persistence is just another subscriber (throttled JSON snapshot).
/// </summary>
public sealed class TodoStore : IDisposable
{
    private static readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "todos.json");
    private readonly SourceCache<TodoItem, Guid> _todos = new(t => t.Id);
    private readonly IDisposable _persistence;

    public TodoStore()
    {
        var (items, seeded) = Load();
        _todos.AddOrUpdate(items);

        // Skip AFTER ToCollection: ToCollection builds its state from the changesets it
        // observes, so skipping the initial changeset before it would lose the loaded items.
        _persistence = _todos.Connect()
                             .ToCollection()
                             .Skip(1)
                             .Throttle(TimeSpan.FromMilliseconds(500))
                             .Subscribe(Save);

        if (seeded)
        {
            // The Skip(1) above deliberately ignores the initial changeset — but a FRESH seed
            // must become durable right away: stable task ids are what id-carrying snapshots
            // (e.g. navigation restore's TaskEditorIntent) key on across launches. Without
            // this, every cold start re-seeded with new GUIDs until the first user mutation.
            Save(items);
        }
    }

    /// <summary>To-do changesets: filter, group and sort them in the page models.</summary>
    public IObservable<IChangeSet<TodoItem, Guid>> Connect() => _todos.Connect();

    public TodoItem? Get(Guid id) => _todos.Lookup(id) is { HasValue: true } lookup ? lookup.Value : null;

    public void Save(TodoItem item) => _todos.AddOrUpdate(item);

    public void Delete(Guid id) => _todos.RemoveKey(id);

    public void Toggle(Guid id)
    {
        if (Get(id) is { } item)
        {
            _todos.AddOrUpdate(item with { IsDone = !item.IsDone });
        }
    }

    public TodoItem CreateDraft() => new(Guid.NewGuid(), string.Empty, null, DateOnly.FromDateTime(DateTime.Today), false, NextSortOrder());

    private int NextSortOrder() => _todos.Items.Count == 0 ? 0 : _todos.Items.Max(t => t.SortOrder) + 1;

    /// <summary>
    /// Handles a drag-and-drop move: reindexes the target bucket around the dropped item and
    /// adapts the item to its new bucket (dropping on <see cref="TodoBucket.Done"/> completes it,
    /// dragging it out revives it, moving between Today/Upcoming rewrites the due date).
    /// </summary>
    public void Move(TodoItem item, TodoBucket bucket, int index)
    {
        var moved = AdaptToBucket(item, bucket);

        _todos.Edit(cache =>
            {
                var siblings = cache.Items
                                    .Where(t => t.Id != item.Id && t.Bucket == bucket)
                                    .OrderBy(t => t.SortOrder)
                                    .ToList();

                siblings.Insert(Math.Clamp(index, 0, siblings.Count), moved);

                for (var i = 0; i < siblings.Count; i++)
                {
                    if (siblings[i].SortOrder != i || siblings[i].Id == item.Id)
                    {
                        cache.AddOrUpdate(siblings[i] with { SortOrder = i });
                    }
                }
            }
        );
    }

    private static TodoItem AdaptToBucket(TodoItem item, TodoBucket bucket)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return bucket switch
        {
            TodoBucket.Done => item with { IsDone = true },
            TodoBucket.Today => item with { IsDone = false, DueDate = item.DueDate is { } due && due <= today ? due : today },
            _ => item with { IsDone = false, DueDate = item.DueDate is { } due && due > today ? due : today.AddDays(1) }
        };
    }

    private static (IReadOnlyList<TodoItem> Items, bool Seeded) Load()
    {
        try
        {
            if (File.Exists(_filePath)
                && JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(_filePath)) is { Count: > 0 } items)
            {
                return (items, false);
            }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // Corrupted or unreadable snapshot: fall back to the seed data.
        }

        return (Seed(), true);
    }

    private static void Save(IReadOnlyCollection<TodoItem> items)
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(items.OrderBy(t => t.SortOrder).ToList()));
        }
        catch (IOException)
        {
            // Best-effort persistence: losing a snapshot only means re-seeding on next start.
        }
    }

    private static List<TodoItem> Seed()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return
        [
            new TodoItem(Guid.NewGuid(), "Morning run", "5k along the river", today, false, 0) { Duration = TimeSpan.FromMinutes(45) },
            new TodoItem(Guid.NewGuid(), "Water the plants", null, today, false, 1) { Duration = TimeSpan.FromMinutes(15) },
            new TodoItem(Guid.NewGuid(), "Grocery shopping", "Basil, mozzarella, tomatoes", today, false, 2) { Duration = TimeSpan.FromHours(1) },
            new TodoItem(Guid.NewGuid(), "Call the plumber", "Kitchen sink drips", today.AddDays(1), false, 3),
            new TodoItem(Guid.NewGuid(), "Book dentist appointment", null, today.AddDays(2), false, 4),
            new TodoItem(Guid.NewGuid(), "Plan weekend hike", "Check the forecast first!", today.AddDays(3), false, 5),
            new TodoItem(Guid.NewGuid(), "Renew gym membership", null, null, true, 6)
        ];
    }

    public void Dispose()
    {
        _persistence.Dispose();
        _todos.Dispose();
    }
}
