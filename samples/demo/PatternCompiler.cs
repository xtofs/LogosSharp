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
        int uniqueId = 0;

        var pattern = RegexPattern.Parse(value);
        writer.WriteLine($"//!! {pattern}   ");

        writer.WriteMultiline($$"""
            public static bool {{name}}(ReadOnlySpan<char> src, ref int i)
            {
            """);
        writer.Indent++;

        foreach (var rep in pattern.Sequence)
        {
            CompileRepetion(rep, ref uniqueId, writer);
        }

        writer.WriteLine("return false;");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void CompileRepetion(Repetition rep, ref int unique, IndentedTextWriter writer)
    {
        var atomName = CompileAtom(rep.Atom, ref unique, writer);

        writer.WriteLine();
        writer.Indent++;
        writer.WriteLine($"// Repetition: {rep.Min} .. {(rep.Max.HasValue ? rep.Max.Value.ToString() : "∞")} {rep.Atom} ");

        if (rep.Min == 1 && rep.Max == 1)
        {
            writer.WriteLine($"if ({atomName}(src[i])) {{");
            writer.WriteLine("  i += 1;");
            writer.WriteLine("  return true;");
            writer.WriteLine("}");
        }
        else
        {
            var check = $"n is >= {rep.Min}{(rep.Max.HasValue ? $" and <= {rep.Max.Value}" : "")}";

            writer.WriteMultiline($$"""
            var n = 0;
            while({{atomName}}(src[i])) {
                i += 1;
                n += 1;
            }            
            // TODO: check for {{rep.Min}} <= i <= {{rep.Max?.ToString() ?? "∞"}} 
            return {{check}}; 
            """);
        }

        writer.Indent--;
    }

    private static string CompileAtom(Atom atom, ref int unique, IndentedTextWriter writer)
    {
        switch (atom)
        {
            case CharClass c:
                writer.Indent++;
                writer.WriteLine();
                var names = new List<string>();
                foreach (var range in c.Ranges)
                {
                    var rName = CompileRange(range, ref unique, writer);
                    names.Add(rName);
                }

                writer.WriteLine();
                writer.WriteLine($"// CharClass: {(c.Negated ? "Negated " : "")} {c.Ranges}");
                var name = $"IsInCharClass{unique++}";
                writer.WriteMultiline($$"""
                static bool {{name}}(char ch) => {{string.Join(" || ", names.Select(name => $"{name}(ch)"))}};
                """);
                writer.Indent--;
                return name;

            case SingleChar c:
                writer.WriteLine($"// SingleChar: '{c.value}' (escaped)");
                return "IsSingleChar";

            default:
                throw new NotImplementedException();
        }
    }

    private static string CompileRange(CharRange range, ref int unique, IndentedTextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine($"// CharRange: {range}");
        if (range.Start == range.End)
        {
            var name = $"IsChar{unique++}";
            writer.WriteMultiline($$"""
            static bool {{name}}(char ch ) => ch == '{{range.Start}}';
            """);
            return name;
        }
        else
        {
            var name = $"IsInRange{unique++}";
            writer.WriteLine($$"""
            static bool {{name}}(char ch ) => ch is >= '{{range.Start}}' and <= '{{range.End}}';
            """);
            return name;
        }
    }
}
