namespace Demo;

using Logos;

[Logos(Skip = @"[ \t\r\n]+")]
public enum TokenKind
{
    [Token("let", true)]
    Let,

    [Regex("[a-zA-Z_][a-zA-Z0-9_]*")]
    Identifier,

    [Regex("[0-9]+")]
    Number,

    [Token("=")]
    Equals,

    [Token("+")]
    Plus,

    [Regex("""[^"]*""")]
    StringLiteral,

    End,
}
