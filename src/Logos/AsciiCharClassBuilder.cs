namespace Logos;

internal sealed class AsciiCharClassBuilder
{
    private readonly Dictionary<AsciiCharSet, AsciiCharClassDefinition> _definitions = new();

    public IReadOnlyList<AsciiCharClassDefinition> Definitions => _definitions.Values.OrderBy(x => x.BitIndex).ToArray();

    public static AsciiCharClassBuilder Create(IEnumerable<RegexPattern> patterns)
    {
        var builder = new AsciiCharClassBuilder();

        foreach (var pattern in patterns)
        {
            foreach (var repetition in pattern.Sequence)
            {
                if (repetition.Atom is CharClass charClass)
                {
                    builder.Register(charClass);
                }
            }
        }

        return builder;
    }

    public bool TryGetDefinition(CharClass charClass, out AsciiCharClassDefinition? definition)
    {
        if (!AsciiCharSet.TryCreate(charClass, out var set))
        {
            definition = null;
            return false;
        }

        return _definitions.TryGetValue(set, out definition);
    }

    private void Register(CharClass charClass)
    {
        if (!AsciiCharSet.TryCreate(charClass, out var set))
        {
            return;
        }

        var bitIndex = _definitions.Count;
        if (bitIndex >= 64)
        {
            throw new InvalidOperationException("At most 64 distinct ASCII character classes can be optimized per enum.");
        }

        var name = WellKnownSets.TryGetValue(set, out var wellKnownName)
            ? wellKnownName
            : $"Class{bitIndex + 1}";

        if (!_definitions.ContainsKey(set))
        {
            _definitions.Add(set, new AsciiCharClassDefinition(set, name, bitIndex));
        }
    }

    private static readonly Dictionary<AsciiCharSet, string> WellKnownSets = new()
    {
        { AsciiCharSet.FromPattern(" \t\r\n"), "Whitespace" },
        { AsciiCharSet.FromPattern("\r\n"), "Linebreak" },
        { AsciiCharSet.FromPattern("a-zA-Z"), "Alphabetic" },
        { AsciiCharSet.FromPattern("a-zA-Z_"), "IdentifierStart" },
        { AsciiCharSet.FromPattern("a-zA-Z0-9_"), "IdentifierPart" },
        { AsciiCharSet.FromPattern("a-zA-Z0-9"), "Alphanumeric" },
        { AsciiCharSet.FromPattern("0-9"), "Digit" },
        { AsciiCharSet.FromPattern("0-9a-fA-F"), "HexDigit" },
        { AsciiCharSet.FromPattern("0-7"), "OctalDigit" },
        { AsciiCharSet.FromPattern("0-9.eE+-"), "NumericLiteral" },
        { AsciiCharSet.FromPattern("01"), "BinaryDigit" },
        { AsciiCharSet.FromPattern("()[]{}"), "Bracket" },
        { AsciiCharSet.FromPattern("+*/\\%-"), "ArithmeticOperator" },
        { AsciiCharSet.FromPattern("<>=!&|^~"), "LogicalOperator" },
        { AsciiCharSet.FromPattern(".,;:?!"), "Punctuation" },
        { AsciiCharSet.FromPattern("\"'`"), "Quote" },
        { AsciiCharSet.FromPattern("\\/"), "Slash" },
        { AsciiCharSet.FromPattern("@#$"), "Sigil" },
    };
}

internal sealed class AsciiCharClassDefinition(AsciiCharSet set, string name, int bitIndex)
{
    public AsciiCharSet Set { get; } = set;

    public string Name { get; } = name;

    public int BitIndex { get; } = bitIndex;

    public string ConstantName => $"{Name}Class";
}

internal readonly struct AsciiCharSet : IEquatable<AsciiCharSet>
{
    private readonly ulong _lower;
    private readonly ulong _upper;

    public ulong Lower => _lower;
    public ulong Upper => _upper;

    private AsciiCharSet(ulong lower, ulong upper)
    {
        _lower = lower;
        _upper = upper;
    }

    /// <summary>
    /// Parses a pattern like "a-zA-Z0-9" into an AsciiCharSet. 
    /// Ranges are specified with a hyphen, and individual characters are also allowed.
    /// For example, "a-zA-Z0-9" includes all lowercase letters, uppercase letters, and digits.
    /// Similar to regex character class patterns, but only supports ASCII characters and does not support negation or intersection.
    /// </summary>
    /// <note>
    /// Similar to regex character class patterns, the dash '-' 
    /// needs to be in a specific position to be treated as a range operator.
    /// For example, "a-z" is a valid range, but "a-zA-Z-" would treat the 
    /// last '-' as a literal character.
    /// </note>
    internal static AsciiCharSet FromPattern(string pattern)
    {
        var lower = 0UL;
        var upper = 0UL;
        for (var index = 0; index < pattern.Length; index++)
        {
            if (index + 2 < pattern.Length && pattern[index + 1] == '-')
            {
                for (var current = pattern[index]; current <= pattern[index + 2]; current++)
                {
                    Add(ref lower, ref upper, current);
                }

                index += 2;
                continue;
            }

            Add(ref lower, ref upper, pattern[index]);
        }

        return new AsciiCharSet(lower, upper);
    }

    public static bool TryCreate(CharClass charClass, out AsciiCharSet set)
    {
        var lower = 0UL;
        var upper = 0UL;

        foreach (var range in charClass.Ranges)
        {
            if (range.End >= 128)
            {
                set = default;
                return false;
            }

            for (var current = range.Start; current <= range.End; current++)
            {
                Add(ref lower, ref upper, current);
            }
        }

        set = new AsciiCharSet(lower, upper);
        return true;
    }

    public bool Equals(AsciiCharSet other)
    {
        return _lower == other._lower && _upper == other._upper;
    }

    public override bool Equals(object? obj)
    {
        return obj is AsciiCharSet other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (_lower.GetHashCode() * 397) ^ _upper.GetHashCode();
        }
    }

    private static void Add(ref ulong lower, ref ulong upper, int value)
    {
        if (value < 64)
        {
            lower |= 1UL << value;
        }
        else
        {
            upper |= 1UL << (value - 64);
        }
    }
}