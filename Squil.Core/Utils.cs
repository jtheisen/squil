using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

public class NoElementException : InvalidOperationException
{
    public NoElementException(String message, Exception inner)
        : base(message, inner)
    {
    }
}

public class MultipleElementsException : InvalidOperationException
{
    public MultipleElementsException(String message, Exception inner)
        : base(message, inner)
    {
    }
}

public class Empties<T>
{
    public static readonly T[] Array = new T[0];

    public static readonly IEnumerable<T> Enumerable = Array;
}

public class Empties<K, T>
{
    public static readonly Dictionary<K, T> Dictionary = new Dictionary<K, T>();

    public static readonly IMap<K, T> Map = new DefaultMap<K, T>();

    public static readonly ILookup<K, T> Lookup = Empties<(K, T)>.Array.ToLookup(p => p.Item1, p => p.Item2);
}

public static class RazorHelpers
{
    public static String ClassNames(params String[] classes) => String.Join(' ', classes);
}

public static partial class Extensions
{
    static readonly XmlWriterSettings xmlWriterSettings = new XmlWriterSettings { Indent = true };

    public static String Format(this XmlDocument document)
    {
        using (var stringWriter = new StringWriter())
        using (var xmlTextWriter = XmlWriter.Create(stringWriter, xmlWriterSettings))
        {
            document.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();
            return stringWriter.GetStringBuilder().ToString();
        }
    }

    public static IEnumerable<T> ToSingleton<T>(this T value)
    {
        return new[] { value };
    }

    [DebuggerHidden]
    public static T Single<T>(this IEnumerable<T> source, String error)
    {
        try
        {
            return source.Single();
        }
        catch (InvalidOperationException ex)
        {
            var isNonEmpty = source.Select(x => true).FirstOrDefault();
            if (isNonEmpty) throw new MultipleElementsException(error, ex); else throw new NoElementException(error, ex);
        }
    }

    [DebuggerHidden]
    public static T SingleOrDefault<T>(this IEnumerable<T> source, String error)
    {
        try
        {
            return source.SingleOrDefault();
        }
        catch (InvalidOperationException ex)
        {
            throw new MultipleElementsException(error, ex);
        }
    }

    [DebuggerHidden]
    public static T Get<K, T>(this IDictionary<K, T> dict, K key, String error)
    {
        try
        {
            return dict[key];
        }
        catch (KeyNotFoundException ex)
        {
            throw new KeyNotFoundException(error, ex);
        }
    }

    [DebuggerHidden]
    public static T GetOrDefault<K, T>(this IDictionary<K, T> dict, K key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }
        else
        {
            return default;
        }
    }

    [DebuggerHidden]
    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
    {
        if (source == null) return;

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    [DebuggerHidden]
    public static T GetOrDefault<T>(this T[] source, Int32 index)
        => source.Length <= index ? default : source[index];

    [DebuggerHidden]
    public static void Apply<S>(this S source, Action<S> func)
        => func(source);

    [DebuggerHidden]
    public static T Apply<S, T>(this S source, Func<S, T> func)
        => func(source);

    [DebuggerHidden]
    public static Task<T> Ensure<T>(this Task<T> task)
        => task ?? Task.FromResult<T>(default);

    [DebuggerHidden]
    public static T Apply<T>(this T source, Boolean indeed, Func<T, T> func)
        => indeed ? func(source) : source;

    [DebuggerHidden]
    public static T Apply<S, T>(this S s, Func<S, T> fun, Action<S> onException)
    {
        var haveReachedEnd = false;
        try
        {
            var temp = fun(s);
            haveReachedEnd = true;
            return temp;
        }
        finally
        {
            if (!haveReachedEnd)
            {
                onException(s);
            }
        }
    }

    public static T If<T>(this T source, Boolean predicate) => predicate ? source : default;

    [DebuggerHidden]
    public static T Assert<T>(this T value, Predicate<T> predicate, String message = null)
    {
        if (!predicate(value))
        {
            throw new Exception(message);
        }

        return value;
    }

    [DebuggerHidden]
    public static T AssertWeakly<T>(this T value, Predicate<T> predicate, String message = null)
    {
        if (!predicate(value))
        {
            //LogManager.GetLogger("Assertions").Error(message);

            Debugger.Break();
        }

        return value;
    }

    [DebuggerHidden]
    public static T AssertNotNull<T>(this T value, String message)
        where T : class
    {
        if (value == null)
        {
            throw new Exception(message);
        }

        return value;
    }

    public static string GetDescription(this Enum value)
    {
        Type type = value.GetType();
        string name = Enum.GetName(type, value);
        if (name != null)
        {
            var field = type.GetField(name);
            if (field != null)
            {
                var attr = Attribute.GetCustomAttribute(
                    field,
                    typeof(DescriptionAttribute)) as DescriptionAttribute
                ;

                if (attr != null) return attr.Description;
            }
        }
        return null;
    }

    public static Uri WithChangedQuery(this Uri uri, Action<NameValueCollection> change)
    {
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);
        change(query);
        builder.Query = query.ToString();
        return builder.Uri;
    }

    public static void Time(Action func, out TimeSpan elapsed)
    {
        var watch = new Stopwatch();

        watch.Start();

        try
        {
            func();
        }
        finally
        {
            watch.Stop();

            elapsed = watch.Elapsed;
        }
    }

    public static S Time<S>(Func<S> func, out TimeSpan elapsed)
    {
        var watch = new Stopwatch();

        watch.Start();

        try
        {
            return func();
        }
        finally
        {
            watch.Stop();

            elapsed = watch.Elapsed;
        }
    }
}
