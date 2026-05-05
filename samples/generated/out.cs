[Flags]
public enum CharacterClass
{
    Whitespace =         0x0001, // '␉␊␍ '
    Linebreak =          0x0002, // '␊␍'
    IdentifierStart =    0x0004, // 'ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz'
    IdentifierPart =     0x0008, // '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz'
    Digit =              0x0010, // '0123456789'
    Alphabetic =         0x0020, // 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'
    Alphanumeric =       0x0040, // '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'
    HexDigit =           0x0080, // '0123456789ABCDEFabcdef'
    OctalDigit =         0x0100, // '01234567'
    BinaryDigit =        0x0200, // '01'
    NumericLiteral =     0x0400, // '+-.0123456789Ee'
    Bracket =            0x0800, // '()[]{}'
    ArithmeticOperator = 0x1000, // '%*+-/\'
    LogicalOperator =    0x2000, // '!&<=>^|~'
    Punctuation =        0x4000, // '!,.:;?'
    Quote =              0x8000, // '"'`'
    Slash =              0x10000, // '/\'
    Sigil =              0x20000, // '#$@'
}

public static class CharClasses
{
    private static readonly CharacterClass[] Lookup = new CharacterClass[] {
        (CharacterClass)0x00000000, // '␀' 
        (CharacterClass)0x00000000, // '␁' 
        (CharacterClass)0x00000000, // '␂' 
        (CharacterClass)0x00000000, // '␃' 
        (CharacterClass)0x00000000, // '␄' 
        (CharacterClass)0x00000000, // '␅' 
        (CharacterClass)0x00000000, // '␆' 
        (CharacterClass)0x00000000, // '␇' 
        (CharacterClass)0x00000000, // '␈' 
        (CharacterClass)0x00000001, // '␉' (Whitespace)
        (CharacterClass)0x00000003, // '␊' (Whitespace, Linebreak)
        (CharacterClass)0x00000000, // '␋' 
        (CharacterClass)0x00000000, // '␌' 
        (CharacterClass)0x00000003, // '␍' (Whitespace, Linebreak)
        (CharacterClass)0x00000000, // '␎' 
        (CharacterClass)0x00000000, // '␏' 
        (CharacterClass)0x00000000, // '␐' 
        (CharacterClass)0x00000000, // '␑' 
        (CharacterClass)0x00000000, // '␒' 
        (CharacterClass)0x00000000, // '␓' 
        (CharacterClass)0x00000000, // '␔' 
        (CharacterClass)0x00000000, // '␕' 
        (CharacterClass)0x00000000, // '␖' 
        (CharacterClass)0x00000000, // '␗' 
        (CharacterClass)0x00000000, // '␘' 
        (CharacterClass)0x00000000, // '␙' 
        (CharacterClass)0x00000000, // '␚' 
        (CharacterClass)0x00000000, // '␛' 
        (CharacterClass)0x00000000, // '␜' 
        (CharacterClass)0x00000000, // '␝' 
        (CharacterClass)0x00000000, // '␞' 
        (CharacterClass)0x00000000, // '␟' 
        (CharacterClass)0x00000001, // ' ' (Whitespace)
        (CharacterClass)0x00006000, // '!' (LogicalOperator, Punctuation)
        (CharacterClass)0x00008000, // '"' (Quote)
        (CharacterClass)0x00020000, // '#' (Sigil)
        (CharacterClass)0x00020000, // '$' (Sigil)
        (CharacterClass)0x00001000, // '%' (ArithmeticOperator)
        (CharacterClass)0x00002000, // '&' (LogicalOperator)
        (CharacterClass)0x00008000, // ''' (Quote)
        (CharacterClass)0x00000800, // '(' (Bracket)
        (CharacterClass)0x00000800, // ')' (Bracket)
        (CharacterClass)0x00001000, // '*' (ArithmeticOperator)
        (CharacterClass)0x00001400, // '+' (NumericLiteral, ArithmeticOperator)
        (CharacterClass)0x00004000, // ',' (Punctuation)
        (CharacterClass)0x00001400, // '-' (NumericLiteral, ArithmeticOperator)
        (CharacterClass)0x00004400, // '.' (NumericLiteral, Punctuation)
        (CharacterClass)0x00011000, // '/' (ArithmeticOperator, Slash)
        (CharacterClass)0x000007d8, // '0' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, BinaryDigit, NumericLiteral)
        (CharacterClass)0x000007d8, // '1' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, BinaryDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '2' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '3' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '4' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '5' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '6' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000005d8, // '7' (IdentifierPart, Digit, Alphanumeric, HexDigit, OctalDigit, NumericLiteral)
        (CharacterClass)0x000004d8, // '8' (IdentifierPart, Digit, Alphanumeric, HexDigit, NumericLiteral)
        (CharacterClass)0x000004d8, // '9' (IdentifierPart, Digit, Alphanumeric, HexDigit, NumericLiteral)
        (CharacterClass)0x00004000, // ':' (Punctuation)
        (CharacterClass)0x00004000, // ';' (Punctuation)
        (CharacterClass)0x00002000, // '<' (LogicalOperator)
        (CharacterClass)0x00002000, // '=' (LogicalOperator)
        (CharacterClass)0x00002000, // '>' (LogicalOperator)
        (CharacterClass)0x00004000, // '?' (Punctuation)
        (CharacterClass)0x00020000, // '@' (Sigil)
        (CharacterClass)0x000000ec, // 'A' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'B' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'C' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'D' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000004ec, // 'E' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit, NumericLiteral)
        (CharacterClass)0x000000ec, // 'F' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x0000006c, // 'G' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'H' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'I' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'J' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'K' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'L' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'M' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'N' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'O' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'P' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'Q' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'R' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'S' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'T' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'U' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'V' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'W' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'X' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'Y' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'Z' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x00000800, // '[' (Bracket)
        (CharacterClass)0x00011000, // '\' (ArithmeticOperator, Slash)
        (CharacterClass)0x00000800, // ']' (Bracket)
        (CharacterClass)0x00002000, // '^' (LogicalOperator)
        (CharacterClass)0x0000000c, // '_' (IdentifierStart, IdentifierPart)
        (CharacterClass)0x00008000, // '`' (Quote)
        (CharacterClass)0x000000ec, // 'a' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'b' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'c' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000000ec, // 'd' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x000004ec, // 'e' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit, NumericLiteral)
        (CharacterClass)0x000000ec, // 'f' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric, HexDigit)
        (CharacterClass)0x0000006c, // 'g' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'h' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'i' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'j' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'k' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'l' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'm' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'n' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'o' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'p' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'q' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'r' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 's' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 't' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'u' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'v' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'w' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'x' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'y' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x0000006c, // 'z' (IdentifierStart, IdentifierPart, Alphabetic, Alphanumeric)
        (CharacterClass)0x00000800, // '{' (Bracket)
        (CharacterClass)0x00002000, // '|' (LogicalOperator)
        (CharacterClass)0x00000800, // '}' (Bracket)
        (CharacterClass)0x00002000  // '~' (LogicalOperator)
    };

    public static CharacterClass FromChar(char ch)
    {
        return ch < 127 ? Lookup[ch] : 0;
    }
}
