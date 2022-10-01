using System.Collections;
using System.Collections.Specialized;

public interface IMap<K, V> : IEnumerable<KeyValuePair<K, V>>
{
    V this[K key] { get; set; }

    Object Backing { get; }
}

public static class Map
{
    public static IMap<K, T> ToMap<K, T>(this IEnumerable<(K key, T value)> pairs)
        => new Dictionary<K, T>(from p in pairs select new KeyValuePair<K, T>(p.key, p.value)).AsMap();

    public static IEnumerable<KeyValuePair<K, T>> ToPairs<K, T>(this IEnumerable<(K key, T value)> pairs)
        => from p in pairs select new KeyValuePair<K, T>(p.key, p.value);

    public static IMap<K, T> ToMap<K, T>(this IEnumerable<KeyValuePair<K, T>> pairs)
        => new Dictionary<K, T>(pairs).AsMap();

    public static Dictionary<K, T> ToDictionary<K, T>(this IEnumerable<KeyValuePair<K, T>> pairs)
        => new Dictionary<K, T>(pairs);

    public static IMap<K, T> AsMap<K, T>(this IDictionary<K, T> source, Boolean withDefaults = false)
        => new FromDictionaryMap<K, T>(source, withDefaults);

    public static IMap<String, String> AsMap(this NameValueCollection source)
        => new FromNvcMap(source);

    public static IMap<K, T> Convert<K, T, S>(this IMap<K, S> source, Func<S, T> convert, Func<T, S> convertBack)
        => new CastingMap<K, T, S>(source, convert, convertBack);


    public static NameValueCollection ToNameValueCollection(this IMap<String, String> map)
    {
        var nvc = new NameValueCollection();

        foreach (var kvp in map)
        {
            nvc.Add(kvp.Key, kvp.Value);
        }

        return nvc;
    }
}

class DefaultMap<K, T> : IMap<K, T>
{
    public T this[K key] { get => default; set => throw new NotImplementedException(); }

    public Object Backing => null;

    public IEnumerator<KeyValuePair<K, T>> GetEnumerator()
    {
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

class FromDictionaryMap<K, T> : IMap<K, T>
{
    private readonly IDictionary<K, T> backing;
    private readonly bool withDefaults;

    public FromDictionaryMap(IDictionary<K, T> backing, Boolean withDefaults = false)
    {
        this.backing = backing;
        this.withDefaults = withDefaults;
    }

    public T this[K key]
    {
        get => withDefaults ? GetOrDefault(key) : backing[key];
        set => backing[key] = value;
    }

    public Object Backing => backing;

    public IEnumerator<KeyValuePair<K, T>> GetEnumerator() => backing.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => backing.GetEnumerator();

    public T GetOrDefault(K key)
    {
        if (backing.TryGetValue(key, out var value))
        {
            return value;
        }
        else
        {
            return default;
        }
    }
}

class FromNvcMap : IMap<String, String>
{
    private readonly NameValueCollection backing;

    public FromNvcMap(NameValueCollection backing)
    {
        this.backing = backing;
    }

    public String this[String key]
    {
        get => backing[key];
        set => backing[key] = value;
    }

    public Object Backing => backing;

    IEnumerable<KeyValuePair<String, String>> GetEnumerable()
    {
        foreach (String key in backing)
        {
            yield return new KeyValuePair<String, String>(key, backing[key]);
        }
    }

    public IEnumerator<KeyValuePair<String, String>> GetEnumerator() => GetEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

class CastingMap<K, T, S> : IMap<K, T>
{
    private readonly IMap<K, S> source;
    private readonly Func<S, T> convert;
    private readonly Func<T, S> convertBack;

    public CastingMap(IMap<K, S> source, Func<S, T> convert, Func<T, S> convertBack)
    {
        this.source = source;
        this.convert = convert;
        this.convertBack = convertBack;
    }

    public T this[K key] { get => convert(source[key]); set => source[key] = convertBack(value); }

    public Object Backing => source.Backing;

    IEnumerable<KeyValuePair<K, T>> GetEnumerable()
    {
        return from p in source select new KeyValuePair<K, T>(p.Key, convert(p.Value));
    }

    public IEnumerator<KeyValuePair<K, T>> GetEnumerator() => GetEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

