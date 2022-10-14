namespace Squil;

public class ModalModel<T>
{
    private readonly Action stateHasChanged;

    public ModalModel(Action stateHasChanged)
    {
        this.stateHasChanged = stateHasChanged;
    }

    T model;

    public T Model
    {
        get => model;
        set
        {
            model = value;

            Close = model != null ? () => Model = default : null;

            stateHasChanged();
        }
    }

    public void SetModel(T value) => Model = value;

    public Action Close { get; private set; }
}
