using System.Threading;

public static class StaticServiceStack<T>
{
    static AsyncLocal<Stack<T>> implementations = new AsyncLocal<Stack<T>>();

    public static IDisposable Install(T implementation)
    {
        lock (typeof(StaticServiceStack<T>))
        {
            if (implementations.Value == null)
            {
                implementations.Value = new Stack<T>();
            }

            implementations.Value.Push(implementation);
        }

        return new ActionDisposable(() =>
        {
            lock (typeof(StaticServiceStack<T>))
            {
                var top = implementations.Value.Pop();

                if (!top.Equals(implementation)) throw new Exception($"Unexpectedly found a different implementation on the stack on leaving install scope");
            }
        });
    }

    public static T Get() => implementations.Value is Stack<T> s && s.Count > 0 ? s.Peek() : throw new Exception($"No service of type {typeof(T).Name} was installed");
    public static T GetOptional() => implementations.Value is Stack<T> s && s.Count > 0 ? s.Peek() : default;
}

public class ActionDisposable : IDisposable
{
    private readonly Action action;

    public ActionDisposable(Action action) => this.action = action;

    public void Dispose() => action?.Invoke();
}

public static class StaticServiceStack
{
    public static IDisposable Install<T>(T implementation) => StaticServiceStack<T>.Install(implementation);

    public static T Get<T>() => StaticServiceStack<T>.Get();
    public static T GetOptional<T>() => StaticServiceStack<T>.GetOptional();
}

