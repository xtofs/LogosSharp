using Logos;

var tokenizer = new TokenKindLogos.Tokenizer("let x = 42 + y");

while (tokenizer.NextToken() is var token && token.Kind != TokenKind.End)
{
    Console.WriteLine($"{token.Kind}: '{token.Value.ToString()}'");
}

[Logos(Skip = "[ \\t\\r\\n]+")]
public enum TokenKind
{
    [Token("let")] 
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
