using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// A DataTemplateSelector that selects a <see cref="DataTemplate"/> based on the <see cref="DataTemplate"/> provided as the item.
/// It also ensures that the <see cref="BindableObject.BindingContext"/> of the created <see cref="View"/> is set to the <see cref="BindableObject.BindingContext"/> of the container.
/// </summary>
public partial class TemplateSourceSelector : DataTemplateSelector
{
    private readonly ConditionalWeakTable<DataTemplate, DataTemplate> _dataTemplates = new();
    
    /// <inheritdoc />
    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        var dataTemplate = item as DataTemplate ?? throw new ArgumentException("Items source must be an enumerable of DataTemplate", nameof(item));
        
        if (!_dataTemplates.TryGetValue(dataTemplate, out var cachedTemplate))
        {
            cachedTemplate = new DataTemplate(() =>
            {
                var content = dataTemplate.CreateContent();
                if (content is not View view)
                {
                    throw new InvalidOperationException("DataTemplate must create a View");
                }

                view.SetBinding(BindableObject.BindingContextProperty, static (BindableObject container) => container.BindingContext, source: container);
                
                return view;
            });
            
            _dataTemplates.Add(dataTemplate, cachedTemplate);
        }
        
        return cachedTemplate;
    }

    
}
