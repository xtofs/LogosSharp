readonly ref struct Token
{
    public Token(TokenKind kind, ReadOnlySpan<char> value)
    {
        Kind = kind;
        Value = value;
    }

    public TokenKind Kind { get; }

    public ReadOnlySpan<char> Value { get; }
}
