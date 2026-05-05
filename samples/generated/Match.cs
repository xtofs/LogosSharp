
static class Match
{
    // Skip: /[ \t\r\n]+/
    // Token Let: Literal 'let'
    public static bool Let(ReadOnlySpan<char> src, ref int i)
    {
        var success = src[i..].StartsWith("let");
        if (success) { i += 3; }
        return success;
    }
    // Token Identifier: Regex  /[a-zA-Z_][a-zA-Z0-9_]*/ 
    //!! [a-zA-Z_][a-zA-Z0-9_]*   
    public static bool Identifier(ReadOnlySpan<char> src, ref int i)
    {


        // CharRange: a-z
        static bool IsInRange0(char ch) => ch is >= 'a' and <= 'z';

        // CharRange: A-Z
        static bool IsInRange1(char ch) => ch is >= 'A' and <= 'Z';

        // CharRange: _
        static bool IsChar2(char ch) => ch == '_';

        // CharClass:  [a-z, A-Z, _]
        static bool IsInCharClass3(char ch) => IsInRange0(ch) || IsInRange1(ch) || IsChar2(ch);

        // Repetition: 1 .. 1 [a-zA-Z_] 
        if (IsInCharClass3(src[i]))
        {
            i += 1;
            return true;
        }


        // CharRange: a-z
        static bool IsInRange4(char ch) => ch is >= 'a' and <= 'z';

        // CharRange: A-Z
        static bool IsInRange5(char ch) => ch is >= 'A' and <= 'Z';

        // CharRange: 0-9
        static bool IsInRange6(char ch) => ch is >= '0' and <= '9';

        // CharRange: _
        static bool IsChar7(char ch) => ch == '_';

        // CharClass:  [a-z, A-Z, 0-9, _]
        static bool IsInCharClass8(char ch) => IsInRange4(ch) || IsInRange5(ch) || IsInRange6(ch) || IsChar7(ch);

        // Repetition: 0 .. ∞ [a-zA-Z0-9_] 
        var n = 0;
        while (IsInCharClass8(src[i]))
        {
            i += 1;
            n += 1;
        }
        // TODO: check for 0 <= i <= ∞ 
        return n is >= 0;
        return false;
    }
    // Token Number: Regex  /[0-9]+/ 
    //!! [0-9]+   
    public static bool Number(ReadOnlySpan<char> src, ref int i)
    {


        // CharRange: 0-9
        static bool IsInRange0(char ch) => ch is >= '0' and <= '9';

        // CharClass:  [0-9]
        static bool IsInCharClass1(char ch) => IsInRange0(ch);

        // Repetition: 1 .. ∞ [0-9] 
        var n = 0;
        while (IsInCharClass1(src[i]))
        {
            i += 1;
            n += 1;
        }
        // TODO: check for 1 <= i <= ∞ 
        return n is >= 1;
        return false;
    }
    // Token Equals: Literal '='
    public static bool Equals(ReadOnlySpan<char> src, ref int i)
    {
        var success = src[i..].StartsWith("=");
        if (success) { i += 1; }
        return success;
    }
    // Token Plus: Literal '+'
    public static bool Plus(ReadOnlySpan<char> src, ref int i)
    {
        var success = src[i..].StartsWith("+");
        if (success) { i += 1; }
        return success;
    }
    // Token StringLiteral: Regex  /[^"]*/ 
    //!! [^"]*   
    public static bool StringLiteral(ReadOnlySpan<char> src, ref int i)
    {


        // CharRange: "
        static bool IsChar0(char ch) => ch == '"';

        // CharClass: Negated  ["]
        static bool IsInCharClass1(char ch) => IsChar0(ch);

        // Repetition: 0 .. ∞ ^["] 
        var n = 0;
        while (IsInCharClass1(src[i]))
        {
            i += 1;
            n += 1;
        }
        // TODO: check for 0 <= i <= ∞ 
        return n is >= 0;
        return false;
    }
}
