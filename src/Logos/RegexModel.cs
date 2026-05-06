using System.Text;

namespace Logos;

internal sealed class RegexPattern(Repetition[] sequence)
{
    public Repetition[] Sequence { get; } = sequence ?? throw new ArgumentNullException(nameof(sequence));

    public static RegexPattern Parse(string pattern)
    {
        var parser = new RegexParser(pattern);
        return parser.Parse();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var repetition in Sequence)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(repetition.Atom switch
            {
                SingleChar c => $"'{c.Value}'",
                CharClass cc => $"[{(cc.Negated ? "^" : "")}{string.Join("", cc.Ranges.Select(r => r.Start == r.End ? r.Start.ToString() : $"{r.Start}-{r.End}"))}]",
                _ => throw new InvalidOperationException("Unknown atom type.")
            });

            if (repetition.Min == 0 && repetition.Max == 1)
            {
                builder.Append('?');
            }
            else if (repetition.Min == 0 && repetition.Max == null)
            {
                builder.Append('*');
            }
            else if (repetition.Min == 1 && repetition.Max == null)
            {
                builder.Append('+');
            }
            else if (repetition.Min != 1 || repetition.Max != 1)
            {
                builder.Append('{');
                builder.Append(repetition.Min);
                if (repetition.Max != null)
                {
                    builder.Append(',');
                    builder.Append(repetition.Max);
                }
                builder.Append('}');
            }
        }

        return builder.ToString();
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