using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;

static class CharClassBuilder
{

    // [Obsolete("Use CreateClasses and CreateCharToClassFlagsLookup instead")]
    // public static void FromCharClassPatterns(string[] patterns, out int[] charToClassesLookup, out IDictionary<string, (string, int)> classIndex)
    // {
    //     charToClassesLookup = new int[127];
    //     var unique = 0;
    //     classIndex = new OrderedDictionary<string, (string, int)>();
    //     var index = 0;
    //     foreach (var cls in patterns)
    //     {
    //         var set = ParseCharClass(cls);
    //         var name = WellKnownSets.TryGetValue(set, out var nm) ? nm : $"Class{++unique}";
    //         var flag = 1 << (index++);

    //         classIndex[name] = (set, flag);

    //         foreach (var ch in set)
    //         {
    //             charToClassesLookup[ch] |= flag;
    //         }
    //     }
    // }

    public static IReadOnlyDictionary<UInt128, (string Name, int Value)> CreateClasses(string[] patterns)
    {
        // charToClassesLookup = new int[127];
        var unique = 0;
        var classIndex = new Dictionary<UInt128, (string, int)>();
        var index = 0;
        foreach (var cls in patterns)
        {
            var set = ParseCharClass(cls);
            var name = WellKnownSets.TryGetValue(set, out var nm) ? nm : $"Class{++unique}";
            var flag = 1 << (index++);

            classIndex[set] = (name, flag);

        }
        return classIndex.ToFrozenDictionary();
    }

    public static int[] CreateLookup(string[] patterns, IReadOnlyDictionary<UInt128, (string, int)> classIndex)
    {
        var charToClassesLookup = new int[127];

        foreach (var cls in patterns)
        {
            var set = ParseCharClass(cls);

            var (name, flag) = classIndex[set];

            foreach (var ch in set.EnumerateBits())
            {
                charToClassesLookup[ch] |= flag;
            }
        }



        return charToClassesLookup;
    }

    private static UInt128 ParseCharClass(string cls)
    {
        var chars = cls.StartsWith('[') && cls.EndsWith(']') ? cls[1..^1] : cls;
        var set = UInt128.Zero;
        for (int i = 0; i < chars.Length; i++)
        {
            if (i + 2 < chars.Length && chars[i + 1] == '-')
            {
                for (char c = chars[i]; c <= chars[i + 2]; c++)
                {
                    set |= UInt128.One << c;
                }
                i += 2;
            }
            else
            {
                set |= UInt128.One << chars[i];
            }
        }
        return set;
    }

    private static readonly Dictionary<UInt128, string> WellKnownSets = new()
    {
        { ParseCharClass(" \t\r\n"), "Whitespace" },
        { ParseCharClass("\r\n"), "Linebreak" },
        { ParseCharClass("a-zA-Z"), "Alphabetic" },
        { ParseCharClass("a-zA-Z_"), "IdentifierStart" },
        { ParseCharClass("a-zA-Z0-9_"), "IdentifierPart" },
        { ParseCharClass("a-zA-Z0-9"), "Alphanumeric" },
        { ParseCharClass("0-9"), "Digit" },
        { ParseCharClass("0-9a-fA-F"), "HexDigit" },
        { ParseCharClass("0-7"), "OctalDigit" },
        { ParseCharClass("0-9.eE+-"), "NumericLiteral" },
        { ParseCharClass("01"), "BinaryDigit" },
        { ParseCharClass("()[]{}"), "Bracket" },
        { ParseCharClass("+*/\\%-"), "ArithmeticOperator" },
        { ParseCharClass("<>=!&|^~"), "LogicalOperator" },
        { ParseCharClass(".,;:?!"), "Punctuation" },
        { ParseCharClass("\"'`"), "Quote" },
        { ParseCharClass("\\/"), "Slash" },
        { ParseCharClass("@#$"), "Sigil" },
    };


    internal static object CharsFromSet(UInt128 set)
    {
        var len = set.PopCount();

        return string.Create(len, set, (span, value) =>
        {
            span.Fill(' ');
            var idx = 0;

            while (value != UInt128.Zero)
            {
                // Find the index of the least significant set bit
                var b = value.TrailingZeroCount();

                // fix up control characters to their unicode code points in the control pictures block, so they can be displayed as characters instead of whitespace
                if (b < 32) b += 0x2400;

                // add the corresponding character to the span
                span[idx++] = (char)b;

                value &= value - 1; // Clear the least significant bit
            }
        });
    }
}
