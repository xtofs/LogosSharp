namespace Logos;

using System.CodeDom.Compiler;
using System.Reflection;

static partial class Compiler
{
    public static void Compile<TToken>(TextWriter writer) where TToken : Enum
    {
        var w = new IndentedTextWriter(writer, "    ");
        Compile(typeof(TToken), w);
    }

    public static void Compile(Type type, IndentedTextWriter writer)
    {

        writer.WriteLine();
        writer.WriteLine("static class Match {");
        writer.Indent++;

        var a = type.GetCustomAttribute<LogosAttribute>();

        if (a != null)
        {
            writer.WriteLine($"// Skip: /{a.Skip}/");
        }

        var patterns = type
            .GetMembers()
            .Select(Pattern.FromMemberInfo)
            .Where(x => x != null).Select(x => x!)
            .ToList();

        foreach (var pattern in patterns)
        {
            writer.WriteLine($"// Token {pattern.Name}: {(pattern.IsRegex ? "Regex" : "Literal")} {(pattern.IsRegex ? $" /{pattern.Value}/ " : $"'{pattern.Value}'")}");
            if (pattern.IsRegex)
            {
                CompileRegexPattern(pattern.Name, pattern.Value, writer);
            }
            else
            {
                CompileLiteral(pattern.Name, pattern.Value, writer);
            }
        }

        writer.WriteLine("}");
    }

    private static void CompileLiteral(string name, string value, TextWriter @out)
    {
        var code = $$"""
        public static bool {{name}}(ReadOnlySpan<char> src, ref int i)
        {
            var success = src[i..].StartsWith("{{value}}");
            if (success) { i += {{value.Length}}; }
            return success;
        }
        """;

        @out.WriteLine(code);
    }

    private static void CompileRegexPattern(string name, string value, IndentedTextWriter writer)
    {
        var pattern = RegexPattern.Parse(value);
        writer.WriteLine($"// Regex AST: {pattern}");

        writer.WriteMultiline($$"""
            public static bool {{name}}(ReadOnlySpan<char> src, ref int i)
            {
                var pos = i;
            """);
        writer.Indent++;

        foreach (var rep in pattern.Sequence)
        {
            CompileRepetition(rep, writer);
        }

        writer.WriteLine("i = pos;");
        writer.WriteLine("return true;");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void CompileRepetition(Repetition rep, IndentedTextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine($"// Repetition: {rep.Min}..{(rep.Max.HasValue ? rep.Max.Value.ToString() : "inf")} {rep.Atom}");
        var atomCondition = BuildAtomCondition(rep.Atom, "src[pos]");

        if (rep.Min == 1 && rep.Max == 1)
        {
            writer.WriteLine($"if (!(pos < src.Length && ({atomCondition})))");
            writer.WriteLine("{");
            writer.Indent++;
            EmitFailure(writer);
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("pos += 1;");
            return;
        }

        if (rep.Min == 0 && rep.Max == 1)
        {
            writer.WriteLine($"if (pos < src.Length && ({atomCondition}))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("pos += 1;");
            writer.Indent--;
            writer.WriteLine("}");
            return;
        }

        if (rep.Min == 0 && rep.Max is null)
        {
            writer.WriteLine($"while (pos < src.Length && ({atomCondition}))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("pos += 1;");
            writer.Indent--;
            writer.WriteLine("}");
            return;
        }

        if (rep.Min == 1 && rep.Max is null)
        {
            writer.WriteLine($"if (!(pos < src.Length && ({atomCondition})))");
            writer.WriteLine("{");
            writer.Indent++;
            EmitFailure(writer);
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("pos += 1;");
            writer.WriteLine($"while (pos < src.Length && ({atomCondition}))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("pos += 1;");
            writer.Indent--;
            writer.WriteLine("}");
            return;
        }

        if (rep.Max is int max)
        {
            writer.WriteLine("var count = 0;");
            writer.WriteLine($"while (count < {max} && pos < src.Length && ({atomCondition}))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("pos += 1;");
            writer.WriteLine("count += 1;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine($"if (count < {rep.Min})");
            writer.WriteLine("{");
            writer.Indent++;
            EmitFailure(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }
        else
        {
            writer.WriteLine("var count = 0;");
            writer.WriteLine($"while (pos < src.Length && ({atomCondition}))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("pos += 1;");
            writer.WriteLine("count += 1;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine($"if (count < {rep.Min})");
            writer.WriteLine("{");
            writer.Indent++;
            EmitFailure(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    private static void EmitFailure(IndentedTextWriter writer)
    {
        writer.WriteLine("pos = i;");
        writer.WriteLine("return false;");
    }

    private static string BuildAtomCondition(Atom atom, string charExpression)
    {
        switch (atom)
        {
            case CharClass c:
                var rangeChecks = c.Ranges.Select(range => BuildRangeCondition(range, charExpression)).ToList();
                var combined = rangeChecks.Count == 0 ? "false" : string.Join(" || ", rangeChecks);
                return c.Negated ? $"!({combined})" : combined;

            case SingleChar c:
                return $"{charExpression} == {FormatCharLiteral(c.value)}";

            default:
                throw new NotImplementedException();
        }
    }

    private static string BuildRangeCondition(CharRange range, string charExpression)
    {
        if (range.Start == range.End)
        {
            return $"{charExpression} == {FormatCharLiteral(range.Start)}";
        }

        return $"{charExpression} is >= {FormatCharLiteral(range.Start)} and <= {FormatCharLiteral(range.End)}";
    }

    private static string FormatCharLiteral(char value)
    {
        return value switch
        {
            '\\' => "'\\\\'",
            '\'' => "'\\\''",
            '\0' => "'\\0'",
            '\a' => "'\\a'",
            '\b' => "'\\b'",
            '\f' => "'\\f'",
            '\n' => "'\\n'",
            '\r' => "'\\r'",
            '\t' => "'\\t'",
            '\v' => "'\\v'",
            _ when char.IsControl(value) => $"'\\u{(int)value:X4}'",
            _ => $"'{value}'"
        };
    }
}
