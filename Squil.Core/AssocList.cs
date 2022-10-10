using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

public interface IAssocList<K, V> : IReadOnlyDictionary<K, V>
{
    void Prepend(K key, V value);

    void Append(K key, V value);

    void Remove(K key);
}

public class MappingAssocList<K, NV, OV> : IAssocList<K, NV>
{
    private readonly IAssocList<K, OV> nested;
    private readonly Func<OV, NV> convert;
    private readonly Func<NV, OV> convertBack;

    public MappingAssocList(IAssocList<K, OV> nested, Func<OV, NV> convert, Func<NV, OV> convertBack)
    {
        this.nested = nested;
        this.convert = convert;
        this.convertBack = convertBack;
    }

    public NV this[K key] => convert(nested[key]);

    public IEnumerable<K> Keys => nested.Keys;

    public IEnumerable<NV> Values => nested.Values.Select(convert);

    public Int32 Count => nested.Count;

    public void Append(K key, NV value)
    {
        nested.Append(key, convertBack(value));
    }

    public Boolean ContainsKey(K key)
    {
        return nested.ContainsKey(key);
    }

    public IEnumerator<KeyValuePair<K, NV>> GetEnumerator()
    {
        return (from p in nested select KeyValuePair.Create(p.Key, convert(p.Value))).GetEnumerator();
    }

    public void Prepend(K key, NV value)
    {
        nested.Prepend(key, convertBack(value));
    }

    public void Remove(K key)
    {
        nested.Remove(key);
    }

    public Boolean TryGetValue(K key, [MaybeNullWhen(false)] out NV value)
    {
        var haveValue = nested.TryGetValue(key, out var v);

        value = haveValue ? convert(v) : default;

        return haveValue;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class AssocList<K, V> : ObservableObject, IAssocList<K, V>
{
    private readonly List<KeyValuePair<K, V>> pairs;
    private readonly Dictionary<K, V> dict;
    private readonly String name;

    public AssocList(String name = null)
    {
        this.pairs = new List<KeyValuePair<K, V>>();
        this.dict = new Dictionary<K, V>();

        this.name = name;
    }

    public AssocList(IEnumerable<KeyValuePair<K, V>> pairs, String name = null)
    {
        this.pairs = pairs.ToList();
        this.dict = new Dictionary<K, V>(this.pairs);

        this.name = name;
    }

    public V this[K key] => dict.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"No {name ?? "item"} found under '{key}'");

    public IEnumerable<K> Keys => from p in pairs select p.Key;
    public IEnumerable<V> Values => from p in pairs select p.Value;

    public Int32 Count => pairs.Count;

    public Boolean ContainsKey(K key) => dict.ContainsKey(key);

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => (pairs as IEnumerable<KeyValuePair<K, V>>).GetEnumerator();

    public Boolean TryGetValue(K key, [MaybeNullWhen(false)] out V value) => dict.TryGetValue(key, out value);

    public void Clear()
    {
        pairs.Clear();
        dict.Clear();
        NotifyChange();
    }

    public void Prepend(K key, V value)
    {
        if (dict.ContainsKey(key)) throw new Exception($"An {name ?? "item"} already exists under '{key}'");
        pairs.Insert(0, KeyValuePair.Create(key, value));
        dict[key] = value;
        NotifyChange();
    }

    public void Append(K key, V value)
    {
        if (dict.ContainsKey(key)) throw new Exception($"An {name ?? "item"} already exists under '{key}'");
        pairs.Add(KeyValuePair.Create(key, value));
        dict[key] = value;
        NotifyChange();
    }

    public void UpdateOrAppend(K key, V value)
    {
        if (dict.ContainsKey(key))
        {
            Update(key, value);
        }
        else
        {
            Append(key, value);
        }
    }

    public void Update(K key, V value)
    {
        if (!dict.ContainsKey(key)) throw new Exception($"No {name ?? "item"} found to update under '{key}'");

        var i = pairs.FindIndex(p => p.Key.Equals(key));

        if (i < 0) throw new Exception();

        pairs[i] = KeyValuePair.Create(key, value);
        dict[key] = value;
        NotifyChange();
    }

    public void Remove(K key)
    {
        if (!dict.Remove(key)) throw new Exception($"No {name ?? "item"} found to remove under '{key}'");

        var i = pairs.FindIndex(p => p.Key.Equals(key));

        if (i < 0) throw new Exception();

        pairs.RemoveAt(i);

        NotifyChange();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class AssocListExtensions
{
    public static AssocList<K, V> ToAssocList<K, V>(this IEnumerable<KeyValuePair<K, V>> pairs, String name = null)
        => new AssocList<K, V>(pairs, name);

    public static AssocList<K, V> ToAssocList<K, V>(this IEnumerable<V> items, Func<V, K> selectKey, String name = null)
        => new AssocList<K, V>(from i in items select KeyValuePair.Create(selectKey(i), i), name);

    public static AssocList<K, V> ToAssocList<K, V, T>(this IEnumerable<T> items, Func<T, K> selectKey, Func<T, V> selectValue, String name = null)
        => new AssocList<K, V>(from i in items select KeyValuePair.Create(selectKey(i), selectValue(i)), name);

    public static IAssocList<K, NV> Map<K, NV, OV>(this IAssocList<K, OV> source, Func<OV, NV> convert, Func<NV, OV> convertBack)
        => new MappingAssocList<K, NV, OV>(source, convert, convertBack);
}