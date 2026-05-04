
var x = new Tokenizer("let x = 42 + y");

while (x.NextToken() is var token && token.Kind != TokenKind.End)
{
    Console.WriteLine($"Token: {token.Kind} '{token.Value.ToString()}'");
}


ref struct Tokenizer(ReadOnlySpan<char> src)
{
    private int _pos = 0;
    private readonly ReadOnlySpan<char> _src = src;

    public Token NextToken()
    {
        SkipWhitespace();

        if (_pos >= _src.Length)
            return new Token(TokenKind.End, default);

        int start = _pos;

        Console.WriteLine($"checking `{_src[_pos..]}` at position {_pos}");

        // Literal tokens
        if (Match.Let(_src, ref _pos))
            return new Token(TokenKind.Let, _src.Slice(start, _pos - start));

        if (Match.Equals(_src, ref _pos))
            return new Token(TokenKind.Equals, _src.Slice(start, _pos - start));

        if (Match.Plus(_src, ref _pos))
            return new Token(TokenKind.Plus, _src.Slice(start, _pos - start));

        // Regex tokens
        if (Match.Ident(_src, ref _pos))
            return new Token(TokenKind.Ident, _src.Slice(start, _pos - start));

        if (Match.Number(_src, ref _pos))
            return new Token(TokenKind.Number, _src.Slice(start, _pos - start));

        throw new Exception($"Unexpected character '{_src[_pos]}' at {_pos}");
    }

    private void SkipWhitespace()
    {
        while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos]))
        {
            _pos++;
        }
    }
}
