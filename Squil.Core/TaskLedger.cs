using System.IO;
using System.Threading;

namespace TaskLedgering;

public interface IReportResult
{
    String ToReportString();
}

public class LedgerScope : IDisposable
{
    Int32 nestingLevel;
    private readonly Stopwatch watch;
    private readonly TaskLedger report;
    internal readonly String name;
    private Object result;

    TimeSpan? time;
    internal List<LedgerScope> children;

    public String Name => name;
    public TimeSpan? Time => time;
    public Object Result => result;
    public IEnumerable<LedgerScope> Children => children;
    public Boolean IsLeaf => children.Count == 0;

    internal LedgerScope(TaskLedger report, String name, Int32 nestingLevel, Stopwatch watch)
    {
        this.report = report;
        this.name = name;
        this.nestingLevel = nestingLevel;
        this.watch = watch?.IsRunning == false ? watch : new Stopwatch();
        this.children = new List<LedgerScope>();
        this.watch.Reset();
        this.watch.Start();
    }

    public T SetResult<T>(T result)
    {
        this.result = result;

        return result;
    }

    public IEnumerable<LedgerScope> GetDescendants() => children.SelectMany(c => c.GetDescendants());
    public IEnumerable<LedgerScope> GetDescendantLeaves() => GetDescendants().Where(c => c.IsLeaf);

    public void Dispose()
    {
        watch.Stop();

        time = watch.Elapsed;

        report.CloseScope(this);
    }

    public void Write(TextWriter writer, String indentation)
    {
        writer.Write(indentation);

        if (name is not null)
        {
            writer.Write(name);
        }
        else
        {
            writer.Write("root");
        }

        if (time is not null)
        {
            writer.Write($" in {time}");
        }

        if (result is not null)
        {
            writer.Write($" with {result.GetType().Name}");
        }

        writer.WriteLine();

        var nestedIndentation = indentation + "  ";

        foreach (var child in children)
        {
            child.Write(writer, nestedIndentation);
        }
    }

    public override String ToString()
    {
        var writer = new StringWriter();
        Write(writer, "");
        return writer.ToString();
    }
}

class ActionScope : IDisposable
{
    private readonly Action action;

    public ActionScope(Action action) => this.action = action;

    public void Dispose() => action();
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

    LedgerScope root;

    Stack<LedgerScope> stack = new Stack<LedgerScope>();

    public LedgerScope Root => root;

    public IEnumerable<LedgerScope> GetAllScopes() => Root.GetDescendants();
    public IEnumerable<LedgerScope> GetAllLeafScopes() => Root.GetDescendantLeaves();

    public TaskLedger(Action onDispose)
    {
        root = new LedgerScope(this, "", 0, null);

        stack.Push(root);

        this.onDispose = onDispose;
    }

    public void Dispose() => onDispose?.Invoke();

    internal void CloseScope(LedgerScope scope)
    {
        var top = stack.Pop();

        if (!Object.ReferenceEquals(top, scope)) throw new Exception();
    }

    public LedgerScope OpenScope(String name)
    {
        var scope = new LedgerScope(this, name, stack.Count, watch);

        stack.Peek().children.Add(scope);

        stack.Push(scope);

        return scope;
    }
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