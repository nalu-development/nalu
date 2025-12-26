namespace Nalu;

internal sealed class VirtualScrollCellManager<T> : IDisposable
    where T : class
{
    private readonly Func<T, IView?> _viewGetter;
    private readonly List<WeakReference<T>> _cells = new(32);
    
    public int Count => _cells.Count;

    public VirtualScrollCellManager(Func<T, IView?> viewGetter)
    {
        _viewGetter = viewGetter;
    }

    public void TrackCell(T cell) => _cells.Add(new WeakReference<T>(cell));

    public void Dispose()
    {
        foreach (var weakCell in _cells)
        {
            if (weakCell.TryGetTarget(out var cell))
            {
                var view = _viewGetter(cell);
                if (view is null)
                {
                    continue;
                }

                view.DisconnectHandlers();

                if (view is Element { Parent: { } parent } element)
                {
                    parent.RemoveLogicalChild(element);
                }

                if (view is BindableObject bindableObject)
                {
                    bindableObject.BindingContext = null;
                }

                if (cell is IDisposable disposableCell)
                {
                    disposableCell.Dispose();
                }
            }
        }
        
        _cells.Clear();
    }
}
