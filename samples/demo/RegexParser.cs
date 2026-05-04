namespace Logos;

public sealed class RegexParser(string src)
{
    private readonly string _src = src ?? throw new ArgumentNullException(nameof(src));
    private int _pos;

    public RegexPattern Parse()
    {
        var repetitions = new List<Repetition>();

        while (!End)
        {
            var rep = ParseRepetition();
            repetitions.Add(rep);
        }

        return new RegexPattern(new ReadOnlyList<Repetition>(repetitions)); ;
    }

    private Repetition ParseRepetition()
    {
        var atom = ParseAtom();

        int min = 1;
        int? max = 1;

        if (!End)
        {
            char c = Peek();
            if (c == '*')
            {
                Consume();
                min = 0;
                max = null; // null means unbounded
            }
            else if (c == '+')
            {
                Consume();
                min = 1;
                max = null; // null means unbounded
            }
            else if (c == '?')
            {
                Consume();
                min = 0;
                max = 1;
            }
        }

        return new Repetition(atom, min, max);
    }

    private Atom ParseAtom()
    {
        if (End)
            throw Error("Unexpected end of pattern; expected atom.");

        char c = Peek();

        if (c == '[')
            return ParseCharClass();

        if (c == '\\')
        {
            Consume();
            char escaped = ParseEscape();
            return new SingleChar(escaped);
        }

        // Any non-meta character is a literal atom
        if (IsMeta(c))
            throw Error($"Unexpected meta character '{c}' where atom was expected.");

        Consume();
        return new SingleChar(c);
    }

    private CharClass ParseCharClass()
    {
        // assumes current char is '['
        Consume(); // '['

        bool negated = false;
        if (!End && Peek() == '^')
        {
            negated = true;
            Consume();
        }


        var ranges = new List<CharRange>();

        if (End)
            throw Error("Unterminated character class.");

        while (!End && Peek() != ']')
        {
            char start = ParseClassChar();

            if (!End && Peek() == '-' && LookaheadNotClosingBracket())
            {
                Consume(); // '-'
                char end = ParseClassChar();
                if (end < start)
                    throw Error($"Invalid range '{start}-{end}' in character class.");
                ranges.Add(new CharRange(start, end));
            }
            else
            {
                ranges.Add(new CharRange(start, start));
            }
        }

        if (End || Peek() != ']')
            throw Error("Unterminated character class.");

        Consume(); // ']'
        return new CharClass(new ReadOnlyList<CharRange>(ranges), negated); ;
    }

    private char ParseClassChar()
    {
        if (End)
            throw Error("Unexpected end of pattern inside character class.");

        char c = Peek();
        if (c == '\\')
        {
            Consume();
            return ParseEscape();
        }

        // ']' and '-' are allowed as literals if not used structurally
        Consume();
        return c;
    }

    private char ParseEscape()
    {
        if (End)
            throw Error("Incomplete escape sequence at end of pattern.");

        char c = Consume();

        return c switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '\\' => '\\',
            '"' => '"',
            '\'' => '\'',
            'x' => ParseHexEscape(2),
            'u' => ParseUnicodeEscape(),
            _ => c // treat unknown escapes as literal char
        };
    }

    private char ParseHexEscape(int digits)
    {
        if (_pos + digits > _src.Length)
            throw Error("Incomplete hex escape.");

        int value = 0;
        for (int i = 0; i < digits; i++)
        {
            char c = _src[_pos + i];
            int v = HexValue(c);
            if (v < 0)
                throw Error($"Invalid hex digit '{c}' in escape.");
            value = (value << 4) | v;
        }

        _pos += digits;
        return (char)value;
    }

    private char ParseUnicodeEscape()
    {
        // supports \u{NNNN}
        if (End || Peek() != '{')
            throw Error("Expected '{' after \\u.");

        Consume(); // '{'

        int value = 0;
        int digits = 0;

        while (!End && Peek() != '}')
        {
            char c = Consume();
            int v = HexValue(c);
            if (v < 0)
                throw Error($"Invalid hex digit '{c}' in unicode escape.");
            value = (value << 4) | v;
            digits++;
            if (digits > 6)
                throw Error("Unicode escape too long.");
        }

        if (End || Peek() != '}')
            throw Error("Unterminated unicode escape.");

        Consume(); // '}'

        if (value > 0x10FFFF)
            throw Error("Unicode code point out of range.");

        return (char)value; // for BMP only; adjust if you care about full range
    }

    private static int HexValue(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
        if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
        return -1;
    }

    private bool LookaheadNotClosingBracket()
    {
        // we are at '-', check that next is not ']' (so "a-]" is treated as 'a', '-', ']')
        if (_pos + 1 >= _src.Length) return false;
        return _src[_pos + 1] != ']';
    }

    private bool IsMeta(char c) => c switch
    {
        '*' or
        '+' or
        '?' or
        '[' or
        ']' or
        '|' => true,
        _ => false
    };

    private bool End => _pos >= _src.Length;
    private char Peek() => _src[_pos];
    private char Consume() => _src[_pos++];

    private Exception Error(string message) =>
        new FormatException($"{message} at position {_pos} in pattern '{_src}'.");
}

