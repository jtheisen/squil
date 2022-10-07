using System.Collections;
using System.Diagnostics.CodeAnalysis;

public class AssocList<K, V> : IReadOnlyDictionary<K, V>
{
    private readonly List<KeyValuePair<K, V>> pairs;
    private readonly Dictionary<K, V> dict;
    private readonly String name;

    public AssocList()
    {
        this.pairs = new List<KeyValuePair<K, V>>();
        this.dict = new Dictionary<K, V>();
    }

    public AssocList(IEnumerable<KeyValuePair<K, V>> pairs, String name)
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

    public void Prepend(K key, V value)
    {
        if (dict.ContainsKey(key)) throw new Exception($"An {name ?? "item"} already exists under '{key}'");
        pairs.Insert(0, KeyValuePair.Create(key, value));
        dict[key] = value;
    }

    public void Append(K key, V value)
    {
        if (dict.ContainsKey(key)) throw new Exception($"An {name ?? "item"} already exists under '{key}'");
        pairs.Add(KeyValuePair.Create(key, value));
        dict[key] = value;
    }

    public void Remove(K key)
    {
        if (!dict.Remove(key)) throw new Exception($"No {name ?? "item"} found to remove under '{key}'");

        var i = pairs.FindIndex(p => p.Key.Equals(key));

        pairs.RemoveAt(i);
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
}