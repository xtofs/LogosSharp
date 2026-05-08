namespace MiniLangSample;

using Logos;

[Logos(Skip = @"[ \t\r\n]+")]
public enum MiniTokenKind
{
    [Token("function", true)]
    Function,

    [Token("return", true)]
    Return,

    [Token("if", true)]
    If,

    [Token("else", true)]
    Else,

    [Token("while", true)]
    While,

    [Token("for", true)]
    For,

    [Token("var", true)]
    Var,

    [Token("true", true)]
    True,

    [Token("false", true)]
    False,

    [Token("null", true)]
    Null,

    [Token("==")]
    EqualsEquals,

    [Token("!=")]
    BangEquals,

    [Token("<=")]
    LessEquals,

    [Token(">=")]
    GreaterEquals,

    [Token("&&")]
    AndAnd,

    [Token("||")]
    OrOr,

    [Token("=>")]
    Arrow,

    [Token("=")]
    Equals,

    [Token("+")]
    Plus,

    [Token("-")]
    Minus,

    [Token("*")]
    Asterisk,

    [Token("/")]
    Slash,

    [Token("%")]
    Percent,

    [Token("!")]
    Bang,

    [Token("<")]
    Less,

    [Token(">")]
    Greater,

    [Token("(")]
    LeftParen,

    [Token(")")]
    RightParen,

    [Token("{")]
    LeftBrace,

    [Token("}")]
    RightBrace,

    [Token("[")]
    LeftBracket,

    [Token("]")]
    RightBracket,

    [Token(",")]
    Comma,

    [Token(".")]
    Dot,

    [Token(";")]
    Semicolon,

    [Token(":")]
    Colon,

    [Regex("[0-9]+")]
    Number,

    [Regex("[a-zA-Z_][a-zA-Z0-9_]*")]
    Identifier,

    [Match(typeof(MiniTokenKindEx), nameof(MiniTokenKindEx.StringLiteral), StartsWith = "\"")]
    StringLiteral,

    End,
}

internal static class MiniTokenKindEx
{
    public static bool StringLiteral(ReadOnlySpan<char> src, ref int i)
    {
        if (i >= src.Length || src[i] != '"')
        {
            return false;
        }

        i++;
        while (i < src.Length)
        {
            if (src[i] == '"')
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
