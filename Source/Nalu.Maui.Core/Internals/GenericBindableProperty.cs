using System.Diagnostics.CodeAnalysis;

namespace Nalu.Internals;

internal static class GenericBindableProperty<[DynamicallyAccessedMembers(DeclaringTypeMembers)]TBindable>
    where TBindable : BindableObject
{
    // ReSharper disable once InconsistentNaming
    private const DynamicallyAccessedMemberTypes DeclaringTypeMembers = DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods;
    // ReSharper disable once InconsistentNaming
    private const DynamicallyAccessedMemberTypes ReturnTypeMembers = DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;
    
    public delegate void PropertyChangeDelegate<in TValue>(TValue oldValue, TValue newValue);

    // defaultValueCreator produces the default value PER INSTANCE. Mandatory for defaults that
    // must not be shared between bindables: MAUI re-parents Shadow (and Brush) values on
    // assignment, so one static instance handed to two owners breaks the first.
    public static BindableProperty Create<[DynamicallyAccessedMembers(ReturnTypeMembers)]TValue>(
        string propertyName,
        TValue defaultValue = default!,
        BindingMode defaultBindingMode = BindingMode.OneWay,
        Func<TBindable, PropertyChangeDelegate<TValue>>? propertyChanged = null,
        Func<TBindable, PropertyChangeDelegate<TValue>>? propertyChanging = null,
        Func<TBindable, TValue>? defaultValueCreator = null
    )
        => BindableProperty.Create(
            propertyName,
            typeof(TValue),
            typeof(TBindable),
            defaultValue,
            defaultBindingMode,
            propertyChanged: propertyChanged is not null ? (bindable, value, newValue) => propertyChanged((TBindable) bindable)((TValue) value, (TValue) newValue) : null,
            propertyChanging: propertyChanging is not null ? (bindable, value, newValue) => propertyChanging((TBindable) bindable)((TValue) value, (TValue) newValue) : null,
            defaultValueCreator: defaultValueCreator is not null ? bindable => defaultValueCreator((TBindable) bindable) : null
        );
}
