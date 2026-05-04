
using System.Collections;
using System.Runtime.CompilerServices;

namespace Logos;

[CollectionBuilder(typeof(ReadOnlyListBuilder), nameof(ReadOnlyListBuilder.Create))]
public class ReadOnlyList<T> : IEnumerable<T>
{
    private readonly T[] _items;

    public ReadOnlyList(ReadOnlySpan<T> items)
    {
        _items = items.ToArray();
    }

    public ReadOnlyList(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public ReadOnlyList(params T[] items)
    {
        _items = items; // TODO: is this safe? should we copy the array?
    }

    // add support for collection expressions
    public static ReadOnlyList<T> Create(ReadOnlySpan<T> items) => new ReadOnlyList<T>(items.ToArray());

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public override string ToString() => $"[{string.Join(", ", _items)}]";

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _items.GetEnumerator();
    }
}



internal class ReadOnlyListBuilder
{
    public static ReadOnlyList<T> Create<T>(ReadOnlySpan<T> items)
       => new ReadOnlyList<T>(items);
}

