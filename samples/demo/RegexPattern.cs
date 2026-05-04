namespace Logos;

// Feature              | Allowed | Notes
// ---------------------|---------|---------------------
// Character classes    | ✔       | Core building block
// Ranges               | ✔       | [a-z]
// Escapes              | ✔       | \n, \xNN, \u{NNNN}
// Concatenation        | ✔       | Only sequencing
// *, +, ?              | ✔       | Only on single atoms
//                      |         |
// Grouping (...)       | ✘       | Not supported
// Alternation `|`      | ✘       | Not supported
// Lookahead/lookbehind | ✘       | Not supported
// {m,n} repetition     | ✘       | Not supported

public sealed record RegexPattern(ReadOnlyList<Repetition> Sequence)
{
    public static RegexPattern Parse(string pattern)
    {
        RegexParser parser = new(pattern);
        return new RegexPattern(parser.Parse());
    }

    public override string ToString()
    {
        return string.Concat(Sequence.Select(r => r.Atom switch
        {
            CharClass c when c.Negated => $"[^{string.Concat(c.Ranges)}]{Suffix(r)}",
            CharClass c => $"[{string.Concat(c.Ranges)}]{Suffix(r)}",
            SingleChar c when char.IsLetterOrDigit(c.value) => $"{c.value}{Suffix(r)}",
            SingleChar c => $"\\{c.value}{Suffix(r)}",
            _ => throw new NotImplementedException()
        }));

        static string Suffix(Repetition r) => (r.Min, r.Max) switch
        {
            (0, 1) => "?",
            (0, null) => "*",
            (0, int.MaxValue) => "*", // defensivly treat MaxValue also as unbounded
            (1, null) => "+",
            (1, int.MaxValue) => "+", // defensivly treat MaxValue also as unbounded
            (1, 1) => "",
            _ => throw new NotImplementedException($"Unsupported repetition with min={r.Min} and max={r.Max}.")
        };
    }
}

// * → (min=0, max=∞)
// + → (min=1, max=∞)
// ? → (min=0, max=1)
// no suffix → (min=1, max=1)
public sealed record Repetition(Atom Atom, int Min, int? Max)
{
    public override string ToString()
    {
        var suffix = Suffix();
        return Atom switch
        {
            CharClass c => $"{c}{suffix}",
            SingleChar c when char.IsLetterOrDigit(c.value) => $"{c.value}{suffix}",
            SingleChar c => $"\\{c.value}{suffix}",
            _ => throw new NotImplementedException()
        };

        string Suffix() => (Min, Max) switch
        {
            (0, 1) => "?",
            (0, null) => "*",
            (1, null) => "+",
            (1, 1) => "",
            _ => throw new NotImplementedException()
        };
    }
}

public abstract record Atom { }

public sealed record SingleChar(char value) : Atom
{
    public override string ToString() => IsReservedChar(value) ? $"\\{value}" : $"{value}";

    private static bool IsReservedChar(char value)
    {
        return value switch
        {
            '\\' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '^' => true,
            _ => false
        };
    }
}


/// <summary>
/// he regex pattern's AST
///     RegexPattern: list of Repetitions
///       Repetition: Atom , min , max 
///         Atom: CharClass | CharRange | SingleChar
///           CharClass: list of CharRange, negated flag
///           CharRange: start char, end char
///           SingleChar: char value 

/// </summary>
/// <param name="Start"></param>
/// <param name="End"></param>
public sealed record CharRange(char Start, char End)
{
    public override string ToString() => Start == End ? $"{Start}" : $"{Start}-{End}";
}

public sealed record CharClass(ReadOnlyList<CharRange> Ranges, bool Negated = false) : Atom
{
    public override string ToString()
    {
        return $"{(Negated ? "^" : "")}[{string.Concat(Ranges)}]";
    }
}
