using NLog;
using System.ComponentModel;

public struct LogIds
{
    public Int32 instanceId;
    public Int32 runId;

    public override String ToString()
    {
        return $"{instanceId}{runId}";
    }
}

public class LoggingObject<D>
{
    static protected readonly Logger log = LogManager.GetLogger(typeof(D).Name);

    static Int32 totalInstanceCount;

    Int32 instanceCount = totalInstanceCount++;
    Int32 runCount;

    protected LogIds LogIds => new LogIds { instanceId = instanceCount % 10, runId = runCount++ % 10 };
}

public class ObservableObject<D> : LoggingObject<D>, INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs eventArgs = new PropertyChangedEventArgs(null);

    protected void NotifyChange() => PropertyChanged?.Invoke(this, eventArgs);

    public event PropertyChangedEventHandler PropertyChanged;

    public void RegisterListener(Action action) => PropertyChanged += (_, _) => action();
}
