using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Exposes the state of the soft keyboard.
/// </summary>
public class SoftKeyboardState : INotifyPropertyChanged
{
    private readonly WeakEventManager _weakEventManager = new();

    /// <summary>
    /// Gets the current soft keyboard adjustment mode.
    /// </summary>
    public SoftKeyboardAdjustMode AdjustMode
    {
        get;
        internal set => SetField(ref field, value);
    }
    
    /// <summary>
    /// Gets the soft keyboard height in device-independent units (DIPs).
    /// </summary>
    public double Height
    {
        get;
        internal set => SetField(ref field, value);
    }
    
    /// <summary>
    /// Gets a value indicating whether the soft keyboard is currently visible.
    /// </summary>
    public bool IsVisible
    {
        get;
        internal set
        {
            if (SetField(ref field, value))
            {
                OnPropertyChanged(nameof(IsHidden));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the soft keyboard is currently hidden.
    /// </summary>
    public bool IsHidden => !IsVisible;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _weakEventManager.AddEventHandler(value);
        remove => _weakEventManager.RemoveEventHandler(value);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        => _weakEventManager.HandleEvent(this, new PropertyChangedEventArgs(propertyName), nameof(PropertyChanged));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
