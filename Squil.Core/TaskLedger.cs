using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TaskLedgering;

public class TimedScope : IDisposable
{
    private readonly Stopwatch watch;
    private readonly TaskLedger report;
    private readonly String name;

    public TimedScope(TaskLedger report, String name, Stopwatch watch)
    {
        this.report = report;
        this.name = name;
        this.watch = watch?.IsRunning == false ? watch : new Stopwatch();
        watch.Reset();
        watch.Start();
    }

    public void Dispose()
    {
        watch.Stop();
        report.ReportTime(name, watch.Elapsed);
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

    public LedgerEntry(String name, TimeSpan time)
    {
        this.name = name;
        this.time = time;
    }
}

public static class LedgerControl
{
    static AsyncLocal<TaskLedger> current = new();

    public static TaskLedger GetCurrentLedger() => current.Value;

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

    public void ReportTime(String name, TimeSpan time) => entries.Add(new LedgerEntry(name, time));

    public IDisposable GroupingScope(String name)
    {
        groups.Push(name);

        return new ActionScope(() => groups.Pop());
    }

    public IDisposable TimedScope(String name) => new TimedScope(this, name, watch);
}
