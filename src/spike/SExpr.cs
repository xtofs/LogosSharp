using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

abstract record SExpr;

abstract record Atom : SExpr;

sealed record Symbol(string Name) : Atom
{
    public override string ToString() => Name;
}

sealed record StringLit(string Value) : Atom
{
    public override string ToString() => $"\"{Value}\"";
}

sealed record IntLit(long Value) : Atom
{
    public override string ToString() => $"{Value}";
}

sealed record FloatLit(double Value) : Atom
{
    public override string ToString() => $"{Value}";
}
sealed record BoolLit(bool Value) : Atom
{
    public override string ToString() => $"{Value}";
}


/// <summary>
/// this is an S expression list with all infrastructure to 
/// make it work with collection expressions and C# patterns.
/// </summary>
/// <example>
/// SList call = [Sym("add"), Int(1), Int(2)];
/// // type has to be specified for type inference to work.
/// </example>
/// <example>
/// var s = expr switch {
///     SList and [Symbol("if"), var cond, var then, var els] => ...,
///     SList and [Symbol(var op), .. var opArgs] => ...,
///     IntLit i => ...,
///     ...
/// </example>
[CollectionBuilder(typeof(SList), nameof(Create))]
sealed record SList : SExpr, IReadOnlyList<SExpr>
{
    private readonly SExpr[] _items;

    public ImmutableArray<SExpr> Items => _items.ToImmutableArray();

    public SList(SExpr[] items) => _items = items.ToArray();

    public SList(IEnumerable<SExpr> items) => _items = items.ToArray();

    public int Count => _items.Length;

    public SExpr this[int index] => _items[index];
    public SExpr[] this[Range range] => _items[range];

    public IEnumerator<SExpr> GetEnumerator() => ((IEnumerable<SExpr>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public static SList Create(ReadOnlySpan<SExpr> items) => new(items.ToArray());

    public bool Equals(SList? other) =>
        other is not null && _items.AsSpan().SequenceEqual(other._items);
    public override int GetHashCode() =>
        _items.Aggregate(0, (h, e) => HashCode.Combine(h, e));

    public override string ToString() => $"({string.Join(" ", _items)})";
}


static class SExprExtensions
{
    // Factories
    public static SList List(params SExpr[] items) => new SList(items);

    public static Symbol Sym(string s) => new(s);

    public static StringLit Str(string s) => new(s);

    public static IntLit Int(long i) => new IntLit(i);

    public static FloatLit Float(double f) => new(f);

    public static BoolLit Bool(bool b) => new(b);

    // to match a Cons List (head . args) where head is a single SExprs and args is an array of SExprs.
    public static bool TryGetCons(this SExpr e, out SExpr head, out SExpr[] tail)
    {
        if (e is SList { Items: [SExpr s, .. var rest] })
        {
            head = s;
            tail = rest.ToArray();
            return true;
        }

        head = default!;
        tail = Array.Empty<SExpr>();
        return false;
    }
    public static bool TryGetCall(this SExpr e, out Symbol head, out SList args)
    {
        if (e.TryGetCons(out var h, out var a) && h is Symbol s)
        {
            head = s;
            args = new SList(a);
            return true;
        }
        head = default!;
        args = default!;
        return false;
    }
}
