namespace Demo;

using System.Text;
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

    [Token("*")]
    Asterisk,

    [Match(typeof(TokenKindEx), nameof(TokenKindEx.StringLiteral))]
    StringLiteral,

    End,
}

static class TokenKindEx
{

    // matches a string literal, which can contain escaped quotes (e.g., "Hello \"World\"")
    // The string literal must start and end with a double quote, 
    // and can contain any characters in between, including escaped quotes (\"), 
    // but cannot contain unescaped double quotes.
    // the implementation is essentially implementing the pattern /"[^"/]*(["/][^"/]*)*"/
    // the grouping is not supported by our regexes, so we have to implement it manually    
    // the implementation is using the nested loop that corresponds to the grouping in the regex: 
    // the outer loop is looking for the closing quote, and the inner loop is looking for escaped quotes
    public static bool StringLiteral(ReadOnlySpan<char> src, ref int i)
    {
        if (i >= src.Length || src[i] != '"')
        {
            return false;
        }
        i++; // consume the opening quote

        while (i < src.Length)
        {
            if (src[i] == '"')
            {
                i++; // consume the closing quote
                return true;
            }
            else if (src[i] == '\\')
            {
                i += 2; // consume the escape character and the escaped character
            }
            else
            {
                i++; // consume a regular character
            }
        }

        return false; // reached the end of input without finding a closing quote
    }

    // Unescape a string literal, removing the surrounding quotes and interpreting escape sequences
    public static string UnescapeStringLiteral(ReadOnlySpan<char> s)
    {
        if (s.Length < 2 || s[0] != '"' || s[^1] != '"')
        {
            return "";
        }
        var len = s.Length - 2 - s.Count('\\'); // the length of the unescaped string
        return string.Create(len, s, (span, s) =>
        {
            int j = 0;
            for (int i = 1; i < s.Length - 1; i++)
            {
                if (s[i] == '\\')
                {
                    i++; // skip the escape character
                    if (i >= s.Length - 1)
                    {
                        throw new FormatException("Invalid escape sequence at end of string.");
                    }
                    span[j++] = s[i]; // add the escaped character
                }
                else
                {
                    span[j++] = s[i]; // add a regular character
                }
            }
        });
    }
}
