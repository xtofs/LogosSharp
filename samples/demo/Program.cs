using Logos;




using var file = File.CreateText("output.txt");
Compiler.Compile<TokenKind>(file);


[Logos(Skip = "[ \\t\\r\\n]+")]
enum TokenKind
{
    [Token("let")] Let,
    [Regex("[a-zA-Z_][a-zA-Z0-9_]*")] Identifier,

    [Regex("[0-9]+")] Number,
    [Token("=")] Equals,
    [Token("+")] Plus,
    [Regex("""[^"]*""")] StringLiteral,

    End
    // Whitespace,    
    // End
}
