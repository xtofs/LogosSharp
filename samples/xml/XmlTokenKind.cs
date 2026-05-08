namespace XmlSample;

using Logos;

[Logos(Skip = @"[ \t\r\n]+")]
public enum XmlTokenKind
{
    [Token("<?")]
    ProcessingInstructionStart,

    [Token("?>")]
    ProcessingInstructionEnd,

    [Token("</")]
    CloseTagStart,

    [Token("/>")]
    EmptyElementEnd,

    [Token("<")]
    OpenTagStart,

    [Token(">")]
    TagEnd,

    [Token("=")]
    Equals,

    [Regex("[a-zA-Z_:][a-zA-Z0-9_:\\-\\.]*")]
    Name,

    [Regex("[0-9]+")]
    Number,

    [Match(typeof(XmlTokenKindEx), nameof(XmlTokenKindEx.DoubleQuotedString), StartsWith = "\"")]
    DoubleQuotedString,

    [Match(typeof(XmlTokenKindEx), nameof(XmlTokenKindEx.SingleQuotedString), StartsWith = "'")]
    SingleQuotedString,

    End,
}

internal static class XmlTokenKindEx
{
    public static bool DoubleQuotedString(ReadOnlySpan<char> src, ref int i)
    {
        return Quoted(src, ref i, '"');
    }

    public static bool SingleQuotedString(ReadOnlySpan<char> src, ref int i)
    {
        return Quoted(src, ref i, '\'');
    }

    private static bool Quoted(ReadOnlySpan<char> src, ref int i, char quote)
    {
        if (i >= src.Length || src[i] != quote)
        {
            return false;
        }

        i++;
        while (i < src.Length)
        {
            if (src[i] == quote)
            {
                i++;
                return true;
            }

            if (src[i] == '\\')
            {
                i += 2;
            }
            else
            {
                i++;
            }
        }

        return false;
    }
}
