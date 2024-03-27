namespace Nalu;

using Cassowary;

internal class VariableHolder(Variable variable, ISceneElement element, Action<ISceneViewConstraint>? setter = null)
{
    private double _value;

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            if (element is ISceneViewConstraint view)
            {
                setter?.Invoke(view);
            }
        }
    }

    public Variable Variable => variable;
    public ISceneElement Element => element;
}
