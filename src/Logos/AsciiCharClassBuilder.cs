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

    public ulong[] BuildLookup()
    {
        var values = new ulong[128];
        foreach (var definition in Definitions)
        {
            foreach (var index in definition.Set.EnumerateCharacters())
            {
                values[index] |= definition.Flag;
            }
        }

        return values;
    }

    private void Register(CharClass charClass)
    {
        if (!AsciiCharSet.TryCreate(charClass, out var set))
        {
            return;
        }

        if (_definitions.ContainsKey(set))
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

        _definitions.Add(set, new AsciiCharClassDefinition(set, name, bitIndex));
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

    public ulong Flag => 1UL << BitIndex;

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

    public IEnumerable<int> EnumerateCharacters()
    {
        for (var index = 0; index < 64; index++)
        {
            if (((_lower >> index) & 1UL) != 0)
            {
                yield return index;
            }
        }

        for (var index = 0; index < 64; index++)
        {
            if (((_upper >> index) & 1UL) != 0)
            {
                yield return index + 64;
            }
        }
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