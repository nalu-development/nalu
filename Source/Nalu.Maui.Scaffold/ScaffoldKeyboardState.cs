using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// The live soft-keyboard state of a <see cref="Scaffold"/>: one observable object per scaffold
/// (<see cref="Scaffold.KeyboardState"/>), updated from the same platform geometry the keyboard
/// modes react to (iOS <c>keyboardLayoutGuide</c>, Android IME window insets) — per animation
/// frame while the keyboard moves. Bind to it through <see cref="KeyboardBindingExtension"/>
/// (<c>IsVisible="{nalu:KeyboardBinding IsVisible, Converter={StaticResource Not}}"</c>) or
/// <see cref="KeyboardBindings"/> to collapse, pad or re-arrange content while the keyboard is up,
/// whatever the surface's <see cref="ScaffoldKeyboardMode"/>.
/// </summary>
public sealed class ScaffoldKeyboardState : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets a value indicating whether the soft keyboard currently overlaps the window
    /// (<see cref="Height"/> &gt; 0). On iOS a connected hardware keyboard still shows the input
    /// accessory bar, which counts as visible.
    /// </summary>
    public bool IsVisible
    {
        get;
        private set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the keyboard's overlap with the window's bottom edge, in device-independent units:
    /// 0 while hidden, the running value while it animates, the resting height when shown
    /// (iOS: accessory bar included). This is the amount the keyboard modes resize/pan by.
    /// </summary>
    public double Height
    {
        get;
        private set => SetField(ref field, value);
    }

    /// <summary>Updates the state from the platform (called by the presenters).</summary>
    internal void Update(double height)
    {
        height = Math.Max(0, height);
        Height = height;
        IsVisible = height > 0;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
