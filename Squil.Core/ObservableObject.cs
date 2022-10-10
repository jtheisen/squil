using System.ComponentModel;

public class ObservableObject : INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs eventArgs = new PropertyChangedEventArgs(null);

    protected void NotifyChange() => PropertyChanged?.Invoke(this, eventArgs);

    public event PropertyChangedEventHandler PropertyChanged;

    public void RegisterListener(Action action) => PropertyChanged += (_, _) => action();
}
