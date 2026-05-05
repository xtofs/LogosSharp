namespace Logos;

internal sealed class RegexPattern
{
    public RegexPattern(Repetition[] sequence)
    {
        Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
    }

    public Repetition[] Sequence { get; }

    public static RegexPattern Parse(string pattern)
    {
        var parser = new RegexParser(pattern);
        return parser.Parse();
    }
}

internal sealed class Repetition
{
    public Repetition(Atom atom, int min, int? max)
    {
        Atom = atom ?? throw new ArgumentNullException(nameof(atom));
        Min = min;
        Max = max;
    }

    public Atom Atom { get; }

    public int Min { get; }

    public int? Max { get; }
}

internal abstract class Atom
{
}

internal sealed class SingleChar : Atom
{
    public SingleChar(char value)
    {
        Value = value;
    }

    public char Value { get; }
}

internal sealed class CharClass : Atom
{
    public CharClass(CharRange[] ranges, bool negated = false)
    {
        Ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        Negated = negated;
    }

    public CharRange[] Ranges { get; }

    public bool Negated { get; }
}

internal readonly struct CharRange
{
    public CharRange(char start, char end)
    {
        Start = start;
        End = end;
    }

    public char Start { get; }

    public char End { get; }
}