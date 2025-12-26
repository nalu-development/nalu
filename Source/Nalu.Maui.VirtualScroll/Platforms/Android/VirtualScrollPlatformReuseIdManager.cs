using System.Runtime.InteropServices;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Controls.Internals;

namespace Nalu;

internal sealed class VirtualScrollPlatformReuseIdManager
{
    private static readonly DataTemplate _defaultTemplate = new (() => new ContentView());
    private readonly RecyclerView _recyclerView;
    private readonly Dictionary<string, int> _registeredIds = [];
    private readonly Dictionary<int, DataTemplate> _registeredTemplates = [];
    private int _idCounter;

    public int DefaultReuseId => 0;

    public VirtualScrollPlatformReuseIdManager(RecyclerView recyclerView)
    {
        _recyclerView = recyclerView;
        _registeredIds.Add("DEFAULT", _idCounter++);
        _registeredTemplates.Add(DefaultReuseId, _defaultTemplate);
    }
    
    public DataTemplate GetTemplateById(int reuseId) => _registeredTemplates[reuseId];

    public int GetReuseId(DataTemplate template, string prefix)
    {
        IDataTemplateController dataTemplateController = template;
        var templateId = dataTemplateController.IdString;
        var reuseId = $"{prefix}_{templateId}";
        ref var storedReuseId = ref CollectionsMarshal.GetValueRefOrAddDefault(_registeredIds, reuseId, out var exists);
        if (!exists)
        {
            storedReuseId = _idCounter++;
            _registeredTemplates.Add(storedReuseId, template);
            _recyclerView.GetRecycledViewPool().SetMaxRecycledViews(storedReuseId, 40);
        }

        return storedReuseId;
    }
}
