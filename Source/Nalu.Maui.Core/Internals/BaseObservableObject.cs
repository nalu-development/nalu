namespace Nalu.Internals;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// A base class for objects that notify when a property changes.
/// </summary>
/// <remarks>
/// All credit goes to the <a href="https://learn.microsoft.com/it-it/dotnet/communitytoolkit/mvvm/observableobject">Community Toolkit</a> team.
/// </remarks>
public abstract class BaseObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Compares the current and new values for a given property. If the value has changed, updates the property with the new
    /// value, then raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <typeparam name="T">The type of the property that changed.</typeparam>
    /// <param name="field">The field storing the property's value.</param>
    /// <param name="newValue">The property's value after the change occurred.</param>
    /// <param name="propertyName">(optional) The name of the property that changed.</param>
    /// <returns><see langword="true"/> if the property was changed, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// The <see cref="PropertyChanged"/> event is not raised
    /// if the current and new value for the target property are the same.
    /// </remarks>
    protected bool SetProperty<T>([NotNullIfNotNull(nameof(newValue))] ref T field, T newValue, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        field = newValue;

        OnPropertyChanged(propertyName);

        return true;
    }

    /// <summary>
    /// Compares the current and new values for a given property. If the value has changed, updates the property with the new
    /// value, then raises the <see cref="PropertyChanged"/> event.
    /// See additional notes about this overload in <see cref="SetProperty{T}(ref T,T,string)"/>.
    /// </summary>
    /// <typeparam name="T">The type of the property that changed.</typeparam>
    /// <param name="field">The field storing the property's value.</param>
    /// <param name="newValue">The property's value after the change occurred.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> instance to use to compare the input values.</param>
    /// <param name="propertyName">(optional) The name of the property that changed.</param>
    /// <returns><see langword="true"/> if the property was changed, <see langword="false"/> otherwise.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="comparer"/> is <see langword="null"/>.</exception>
    protected bool SetProperty<T>([NotNullIfNotNull(nameof(newValue))] ref T field, T newValue, IEqualityComparer<T> comparer, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (comparer.Equals(field, newValue))
        {
            return false;
        }

        field = newValue;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        return true;
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    protected virtual void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
