namespace Nalu;

internal sealed class VirtualScrollCellManager<T> : IDisposable
    where T : class
{
    private readonly Func<T, IView?> _viewGetter;
    private readonly List<T> _cells = new(32);

    public VirtualScrollCellManager(Func<T, IView?> viewGetter)
    {
        _viewGetter = viewGetter;
    }

    public void TrackCell(T cell) => _cells.Add(cell);

    public void Dispose()
    {
        foreach (var cell in _cells)
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
        
        _cells.Clear();
    }
}
