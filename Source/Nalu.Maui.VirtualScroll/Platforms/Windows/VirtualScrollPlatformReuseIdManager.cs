using System.Runtime.InteropServices;
using Microsoft.Maui.Controls.Internals;
using Microsoft.UI.Xaml.Controls;

namespace Nalu;

/// <summary>
/// Manages reuse IDs for Windows ItemsRepeater element factory.
/// </summary>
internal sealed class VirtualScrollPlatformReuseIdManager
{
    private static readonly DataTemplate _defaultTemplate = new(() => new ContentView());
    private readonly Dictionary<string, string> _registeredIds = [];
    private readonly Dictionary<string, DataTemplate> _registeredTemplates = [];
    private int _idCounter;

    public string DefaultReuseId => "DEFAULT";

    public VirtualScrollPlatformReuseIdManager()
    {
        _registeredIds.Add("DEFAULT", DefaultReuseId);
        _registeredTemplates.Add(DefaultReuseId, _defaultTemplate);
    }

    public DataTemplate GetTemplateById(string reuseId) => _registeredTemplates[reuseId];

    public string GetReuseId(DataTemplate template, string prefix)
    {
        IDataTemplateController dataTemplateController = template;
        var templateId = dataTemplateController.IdString;
        var reuseIdKey = $"{prefix}_{templateId}";
        ref var storedReuseId = ref CollectionsMarshal.GetValueRefOrAddDefault(_registeredIds, reuseIdKey, out var exists);
        if (!exists)
        {
            storedReuseId = $"REUSE_{_idCounter++}";
            _registeredTemplates.Add(storedReuseId, template);
        }

        return storedReuseId;
    }
}
