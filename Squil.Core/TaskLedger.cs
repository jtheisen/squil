using System.Threading;

namespace TaskLedgering;

public interface IReportResult
{
    String ToReportString();
}

public class TimedScope : IDisposable
{
    private readonly Stopwatch watch;
    private readonly TaskLedger report;
    private readonly String name;
    private Object result;

    public TimedScope(TaskLedger report, String name, Stopwatch watch)
    {
        this.report = report;
        this.name = name;
        this.watch = watch?.IsRunning == false ? watch : new Stopwatch();
        watch.Reset();
        watch.Start();
    }

    public T SetResult<T>(T result)
    {
        this.result = result;

        return result;
    }

    public void Dispose()
    {
        watch.Stop();

        report.ReportTime(name, watch.Elapsed, result);
    }
}

class ActionScope : IDisposable
{
    private readonly Action action;

    public ActionScope(Action action) => this.action = action;

    public void Dispose() => action();
}

public struct LedgerEntry
{
    public String name;
    public TimeSpan time;
    public Object result;

    public LedgerEntry(String name, TimeSpan time, Object result)
    {
        this.name = name;
        this.time = time;
        this.result = result;
    }
}

public static class LedgerControl
{
    static AsyncLocal<TaskLedger> current = new();

    public static TaskLedger GetCurrentLedger() => current.Value ?? new TaskLedger(() => { });

    public static TaskLedger InstallTaskLedger()
    {
        return current.Value = new TaskLedger(() => current.Value = null);
    }
}

public class TaskLedger : IDisposable
{
    Action onDispose;

    Stopwatch watch = new Stopwatch();

    List<LedgerEntry> entries = new List<LedgerEntry>();

    Stack<String> groups = new Stack<String>();

    public TaskLedger(Action onDispose) => this.onDispose = onDispose;

    public void Dispose() => onDispose?.Invoke();

    public IEnumerable<LedgerEntry> GetEntries() => entries;

    public T GetLastEntry<T>()
    {
        return entries.Select(e => e.result).OfType<T>().LastOrDefault();
    }

    public void ReportTime(String name, TimeSpan time, Object result) => entries.Add(new LedgerEntry(name, time, result));

    public IDisposable GroupingScope(String name)
    {
        groups.Push(name);

        return new ActionScope(() => groups.Pop());
    }

    public TimedScope TimedScope(String name) => new TimedScope(this, name, watch);
}

public static class LedgerExtensions
{
    public static String GetReportString(this Object o)
    {
        if (o is String s)
        {
            return s;
        }
        else if (o is IReportResult r)
        {
            return r.ToReportString();
        }
        else
        {
            return null;
        }
    }
}