namespace Logos;

internal sealed class RegexParser
{
    private readonly string _src;
    
    private int _pos;

    public RegexParser(string src)
    {
        _src = src ?? throw new ArgumentNullException(nameof(src));
    }

    public RegexPattern Parse()
    {
        var repetitions = new List<Repetition>();

        while (!End)
        {
            repetitions.Add(ParseRepetition());
        }

        return new RegexPattern(repetitions.ToArray());
    }

    private Repetition ParseRepetition()
    {
        var atom = ParseAtom();
        var min = 1;
        int? max = 1;

        if (!End)
        {
            var c = Peek();
            if (c == '*')
            {
                Consume();
                min = 0;
                max = null;
            }
            else if (c == '+')
            {
                Consume();
                min = 1;
                max = null;
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
        {
            throw Error("Unexpected end of pattern; expected atom.");
        }

        var c = Peek();

        if (c == '[')
        {
            return ParseCharClass();
        }

        if (c == '\\')
        {
            Consume();
            return new SingleChar(ParseEscape());
        }

        if (IsMeta(c))
        {
            throw Error($"Unexpected meta character '{c}' where atom was expected.");
        }

        Consume();
        return new SingleChar(c);
    }

    private CharClass ParseCharClass()
    {
        Consume();

        var negated = false;
        if (!End && Peek() == '^')
        {
            negated = true;
            Consume();
        }

        var ranges = new List<CharRange>();

        while (!End && Peek() != ']')
        {
            var start = ParseClassChar();
            if (!End && Peek() == '-' && LookaheadNotClosingBracket())
            {
                Consume();
                var end = ParseClassChar();
                if (end < start)
                {
                    throw Error($"Invalid range '{start}-{end}' in character class.");
                }

                ranges.Add(new CharRange(start, end));
            }
            else
            {
                ranges.Add(new CharRange(start, start));
            }
        }

        if (End || Peek() != ']')
        {
            throw Error("Unterminated character class.");
        }

        Consume();
        return new CharClass(ranges.ToArray(), negated);
    }

    private char ParseClassChar()
    {
        if (End)
        {
            throw Error("Unexpected end of pattern inside character class.");
        }

        if (Peek() == '\\')
        {
            Consume();
            return ParseEscape();
        }

        return Consume();
    }

    private char ParseEscape()
    {
        if (End)
        {
            throw Error("Incomplete escape sequence at end of pattern.");
        }

        var c = Consume();
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
            _ => c,
        };
    }

    private char ParseHexEscape(int digits)
    {
        if (_pos + digits > _src.Length)
        {
            throw Error("Incomplete hex escape.");
        }

        var value = 0;
        for (var index = 0; index < digits; index++)
        {
            var c = _src[_pos + index];
            var hexValue = HexValue(c);
            if (hexValue < 0)
            {
                throw Error($"Invalid hex digit '{c}' in escape.");
            }

            value = (value << 4) | hexValue;
        }

        _pos += digits;
        return (char)value;
    }

    private char ParseUnicodeEscape()
    {
        if (End || Peek() != '{')
        {
            throw Error("Expected '{' after \\u.");
        }

        Consume();
        var value = 0;
        var digits = 0;

        while (!End && Peek() != '}')
        {
            var c = Consume();
            var hexValue = HexValue(c);
            if (hexValue < 0)
            {
                throw Error($"Invalid hex digit '{c}' in unicode escape.");
            }

            value = (value << 4) | hexValue;
            digits++;
            if (digits > 6)
            {
                throw Error("Unicode escape too long.");
            }
        }

        if (End || Peek() != '}')
        {
            throw Error("Unterminated unicode escape.");
        }

        Consume();

        if (value > 0x10FFFF)
        {
            throw Error("Unicode code point out of range.");
        }

        return (char)value;
    }

    private static int HexValue(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'f')
        {
            return 10 + c - 'a';
        }

        if (c >= 'A' && c <= 'F')
        {
            return 10 + c - 'A';
        }

        return -1;
    }

    private bool LookaheadNotClosingBracket()
    {
        return _pos + 1 < _src.Length && _src[_pos + 1] != ']';
    }

    private static bool IsMeta(char c)
    {
        return c == '*' || c == '+' || c == '?' || c == '[' || c == ']' || c == '|';
    }

    private bool End => _pos >= _src.Length;

    private char Peek() => _src[_pos];

    private char Consume() => _src[_pos++];

    private FormatException Error(string message)
    {
        return new FormatException($"{message} at position {_pos} in pattern '{_src}'.");
    }
}