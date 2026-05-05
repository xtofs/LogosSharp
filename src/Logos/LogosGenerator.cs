using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Logos;

[Generator]
public sealed class LogosGenerator : IIncrementalGenerator
{
    private const string LogosAttributeName = "Logos.LogosAttribute";
    private const string RegexAttributeName = "Logos.RegexAttribute";
    private const string TokenAttributeName = "Logos.TokenAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enums = context.SyntaxProvider.ForAttributeWithMetadataName(
            LogosAttributeName,
            static (node, _) => node is EnumDeclarationSyntax,
            static (ctx, cancellationToken) => CreateGenerationInput(ctx, cancellationToken));

        context.RegisterSourceOutput(enums, static (sourceProductionContext, input) =>
        {
            if (input.Diagnostics.Length > 0)
            {
                foreach (var diagnostic in input.Diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                return;
            }

            if (input.Model is null)
            {
                return;
            }

            try
            {
                var source = Emit(input.Model);
                sourceProductionContext.AddSource(input.Model.HintName, source);
            }
            catch (FormatException ex)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidRegexPattern,
                    input.Model.Location,
                    input.Model.EnumName,
                    ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.GenerationFailed,
                    input.Model.Location,
                    input.Model.EnumName,
                    ex.Message));
            }
        });
    }

    private static GenerationInput CreateGenerationInput(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enumSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var patterns = new List<TokenPatternModel>();
        string? endMemberName = null;

        foreach (var member in enumSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.IsImplicitlyDeclared || member.ConstantValue is null)
            {
                continue;
            }

            if (member.Name == "End")
            {
                endMemberName ??= member.Name;
            }

            var tokenAttribute = FindAttribute(member, TokenAttributeName);
            var regexAttribute = FindAttribute(member, RegexAttributeName);

            if (tokenAttribute is not null && regexAttribute is not null)
            {
                return GenerationInput.FromDiagnostic(Diagnostic.Create(
                    Diagnostics.MultiplePatternAttributes,
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    enumSymbol.Name));
            }

            if (tokenAttribute is not null)
            {
                patterns.Add(new TokenPatternModel(member.Name, PatternKind.Literal, (string)tokenAttribute.ConstructorArguments[0].Value!));
            }
            else if (regexAttribute is not null)
            {
                patterns.Add(new TokenPatternModel(member.Name, PatternKind.Regex, (string)regexAttribute.ConstructorArguments[0].Value!));
            }
        }

        if (patterns.Count == 0)
        {
            return GenerationInput.FromDiagnostic(Diagnostic.Create(
                Diagnostics.NoTokenPatterns,
                enumSymbol.Locations.FirstOrDefault(),
                enumSymbol.Name));
        }

        if (endMemberName is null)
        {
            return GenerationInput.FromDiagnostic(Diagnostic.Create(
                Diagnostics.MissingEndToken,
                enumSymbol.Locations.FirstOrDefault(),
                enumSymbol.Name));
        }

        var logosAttribute = FindAttribute(enumSymbol, LogosAttributeName)!;
        var skipPattern = TryGetNamedArgument(logosAttribute, "Skip");
        var containingNamespace = enumSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : enumSymbol.ContainingNamespace.ToDisplayString();

        return GenerationInput.FromModel(new LogosEnumModel(
            containingNamespace,
            enumSymbol.Name,
            BuildGeneratedTypeName(enumSymbol),
            enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            endMemberName,
            skipPattern,
            patterns.ToArray(),
            enumSymbol.Locations.FirstOrDefault()));
    }

    private static string Emit(LogosEnumModel model)
    {
        var regexPatterns = new List<RegexPattern>();
        RegexPattern? skipPattern = null;

        foreach (var pattern in model.Patterns)
        {
            if (pattern.Kind == PatternKind.Regex)
            {
                regexPatterns.Add(RegexPattern.Parse(pattern.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(model.SkipPattern))
        {
            skipPattern = RegexPattern.Parse(model.SkipPattern!);
            regexPatterns.Add(skipPattern);
        }

        var charClasses = AsciiCharClassBuilder.Create(regexPatterns);
        var writer = new CodeWriter();

        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("using System;");

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            writer.WriteLine();
            writer.WriteLine($"namespace {model.Namespace};");
        }

        writer.WriteLine();
        writer.WriteLine($"public static class {model.GeneratedTypeName}");
        writer.WriteLine("{");
        writer.Indent();

        EmitToken(writer, model);
        writer.WriteLine();
        EmitTokenizer(writer, model);
        writer.WriteLine();
        EmitMatchClass(writer, model, charClasses, skipPattern);

        writer.Outdent();
        writer.WriteLine("}");

        return writer.ToString();
    }

    private static void EmitToken(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine("public readonly ref struct Token");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"public Token({model.EnumTypeName} kind, ReadOnlySpan<char> value)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("Kind = kind;");
        writer.WriteLine("Value = value;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public {model.EnumTypeName} Kind {{ get; }}");
        writer.WriteLine();
        writer.WriteLine("public ReadOnlySpan<char> Value { get; }");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitTokenizer(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine("public ref struct Tokenizer");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("private readonly ReadOnlySpan<char> _src;");
        writer.WriteLine("private int _pos;");
        writer.WriteLine();
        writer.WriteLine("public Tokenizer(ReadOnlySpan<char> src)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("_src = src;");
        writer.WriteLine("_pos = 0;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("public Token NextToken()");
        writer.WriteLine("{");
        writer.Indent();

        if (!string.IsNullOrWhiteSpace(model.SkipPattern))
        {
            writer.WriteLine("SkipIgnored();");
            writer.WriteLine();
        }

        writer.WriteLine("if (_pos >= _src.Length)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"return new Token({model.EnumTypeName}.{model.EndMemberName}, default);");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("var start = _pos;");

        foreach (var pattern in model.Patterns)
        {
            writer.WriteLine($"if (Match.{pattern.Name}(_src, ref _pos))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine($"return new Token({model.EnumTypeName}.{pattern.Name}, _src.Slice(start, _pos - start));");
            writer.Outdent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.WriteLine("throw new InvalidOperationException($\"Unexpected character '{_src[_pos]}' at position {_pos}.\");");
        writer.Outdent();
        writer.WriteLine("}");

        if (!string.IsNullOrWhiteSpace(model.SkipPattern))
        {
            writer.WriteLine();
            writer.WriteLine("private void SkipIgnored()");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("while (true)");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("var start = _pos;");
            writer.WriteLine("if (!Match.Skip(_src, ref _pos) || _pos == start)");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("return;");
            writer.Outdent();
            writer.WriteLine("}");
            writer.Outdent();
            writer.WriteLine("}");
            writer.Outdent();
            writer.WriteLine("}");
        }

        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitMatchClass(CodeWriter writer, LogosEnumModel model, AsciiCharClassBuilder charClasses, RegexPattern? skipPattern)
    {
        writer.WriteLine("public static class Match");
        writer.WriteLine("{");
        writer.Indent();

        var definitions = charClasses.Definitions;
        if (definitions.Count > 0)
        {
            foreach (var definition in definitions)
            {
                writer.WriteLine($"private const ulong {definition.ConstantName} = 1UL << {definition.BitIndex};");
            }

            writer.WriteLine();
            writer.WriteLine("private static readonly ulong[] s_charClassLookup = new ulong[]");
            writer.WriteLine("{");
            writer.Indent();
            var lookup = charClasses.BuildLookup();
            for (var index = 0; index < lookup.Length; index += 8)
            {
                var values = new List<string>(8);
                for (var offset = 0; offset < 8 && index + offset < lookup.Length; offset++)
                {
                    values.Add($"{lookup[index + offset]}UL");
                }

                writer.WriteLine(string.Join(", ", values) + ",");
            }

            writer.Outdent();
            writer.WriteLine("};");
            writer.WriteLine();
            writer.WriteLine("private static bool HasCharClass(char value, ulong flag)");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("return value < s_charClassLookup.Length && (s_charClassLookup[value] & flag) != 0;");
            writer.Outdent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        if (skipPattern is not null)
        {
            EmitRegexPattern(writer, "Skip", skipPattern, charClasses);
            writer.WriteLine();
        }

        foreach (var pattern in model.Patterns)
        {
            if (pattern.Kind == PatternKind.Literal)
            {
                EmitLiteral(writer, pattern.Name, pattern.Value);
            }
            else
            {
                EmitRegexPattern(writer, pattern.Name, RegexPattern.Parse(pattern.Value), charClasses);
            }

            writer.WriteLine();
        }

        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitLiteral(CodeWriter writer, string name, string value)
    {
        writer.WriteLine($"public static bool {name}(ReadOnlySpan<char> src, ref int i)");
        writer.WriteLine("{");
        writer.Indent();

        if (value.Length == 0)
        {
            writer.WriteLine("return true;");
            writer.Outdent();
            writer.WriteLine("}");
            return;
        }

        writer.WriteLine($"if (src.Length - i < {value.Length})");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("return false;");
        writer.Outdent();
        writer.WriteLine("}");

        for (var index = 0; index < value.Length; index++)
        {
            writer.WriteLine($"if (src[i + {index}] != {FormatCharLiteral(value[index])})");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("return false;");
            writer.Outdent();
            writer.WriteLine("}");
        }

        writer.WriteLine($"i += {value.Length};");
        writer.WriteLine("return true;");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitRegexPattern(CodeWriter writer, string name, RegexPattern pattern, AsciiCharClassBuilder charClasses)
    {
        writer.WriteLine($"public static bool {name}(ReadOnlySpan<char> src, ref int i)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("var pos = i;");

        foreach (var repetition in pattern.Sequence)
        {
            EmitRepetition(writer, repetition, charClasses);
        }

        writer.WriteLine("i = pos;");
        writer.WriteLine("return true;");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitRepetition(CodeWriter writer, Repetition repetition, AsciiCharClassBuilder charClasses)
    {
        var condition = BuildAtomCondition(repetition.Atom, charClasses);

        if (repetition.Min == 1 && repetition.Max == 1)
        {
            writer.WriteLine($"if (!(pos < src.Length && ({condition})))");
            EmitFailureBlock(writer);
            writer.WriteLine("pos += 1;");
            return;
        }

        if (repetition.Min == 0 && repetition.Max == 1)
        {
            writer.WriteLine($"if (pos < src.Length && ({condition}))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("pos += 1;");
            writer.Outdent();
            writer.WriteLine("}");
            return;
        }

        if (repetition.Min == 0 && repetition.Max is null)
        {
            writer.WriteLine($"while (pos < src.Length && ({condition}))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("pos += 1;");
            writer.Outdent();
            writer.WriteLine("}");
            return;
        }

        if (repetition.Min == 1 && repetition.Max is null)
        {
            writer.WriteLine($"if (!(pos < src.Length && ({condition})))");
            EmitFailureBlock(writer);
            writer.WriteLine("pos += 1;");
            writer.WriteLine($"while (pos < src.Length && ({condition}))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("pos += 1;");
            writer.Outdent();
            writer.WriteLine("}");
            return;
        }

        var max = repetition.Max ?? int.MaxValue;
        writer.WriteLine("var count = 0;");
        writer.WriteLine($"while (count < {max} && pos < src.Length && ({condition}))");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("pos += 1;");
        writer.WriteLine("count += 1;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine($"if (count < {repetition.Min})");
        EmitFailureBlock(writer);
    }

    private static string BuildAtomCondition(Atom atom, AsciiCharClassBuilder charClasses)
    {
        if (atom is SingleChar singleChar)
        {
            return $"src[pos] == {FormatCharLiteral(singleChar.Value)}";
        }

        if (atom is CharClass charClass)
        {
            if (charClasses.TryGetDefinition(charClass, out var definition) && definition is not null)
            {
                var lookup = $"HasCharClass(src[pos], {definition.ConstantName})";
                return charClass.Negated ? $"!{lookup}" : lookup;
            }

            var conditions = charClass.Ranges.Select(range => BuildRangeCondition(range, "src[pos]"));
            var combined = string.Join(" || ", conditions);
            if (string.IsNullOrEmpty(combined))
            {
                combined = "false";
            }

            return charClass.Negated ? $"!({combined})" : combined;
        }

        throw new NotSupportedException($"Unsupported atom type '{atom.GetType().Name}'.");
    }

    private static string BuildRangeCondition(CharRange range, string charExpression)
    {
        if (range.Start == range.End)
        {
            return $"{charExpression} == {FormatCharLiteral(range.Start)}";
        }

        return $"{charExpression} >= {FormatCharLiteral(range.Start)} && {charExpression} <= {FormatCharLiteral(range.End)}";
    }

    private static void EmitFailureBlock(CodeWriter writer)
    {
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("pos = i;");
        writer.WriteLine("return false;");
        writer.Outdent();
        writer.WriteLine("}");
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
            _ => $"'{value}'",
        };
    }

    private static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);
    }

    private static string? TryGetNamedArgument(AttributeData attribute, string name)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == name)
            {
                return namedArgument.Value.Value as string;
            }
        }

        return null;
    }

    private static string BuildGeneratedTypeName(INamedTypeSymbol enumSymbol)
    {
        var parts = new Stack<string>();
        var current = enumSymbol.ContainingType;
        while (current is not null)
        {
            parts.Push(current.Name);
            current = current.ContainingType;
        }

        parts.Push(enumSymbol.Name);
        return string.Join("_", parts) + "Logos";
    }

    private sealed class CodeWriter
    {
        private readonly StringBuilder _builder = new();
        private int _indent;

        public void Indent()
        {
            _indent++;
        }

        public void Outdent()
        {
            _indent--;
        }

        public void WriteLine(string text = "")
        {
            if (text.Length == 0)
            {
                _builder.AppendLine();
                return;
            }

            _builder.Append(' ', _indent * 4);
            _builder.AppendLine(text);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }

    private readonly struct GenerationInput
    {
        public GenerationInput(LogosEnumModel? model, Diagnostic[] diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        public LogosEnumModel? Model { get; }

        public Diagnostic[] Diagnostics { get; }

        public static GenerationInput FromModel(LogosEnumModel model)
        {
            return new GenerationInput(model, Array.Empty<Diagnostic>());
        }

        public static GenerationInput FromDiagnostic(Diagnostic diagnostic)
        {
            return new GenerationInput(null, new[] { diagnostic });
        }
    }

    private sealed class LogosEnumModel
    {
        public LogosEnumModel(string? @namespace, string enumName, string generatedTypeName, string enumTypeName, string endMemberName, string? skipPattern, TokenPatternModel[] patterns, Location? location)
        {
            Namespace = @namespace;
            EnumName = enumName;
            GeneratedTypeName = generatedTypeName;
            EnumTypeName = enumTypeName;
            EndMemberName = endMemberName;
            SkipPattern = skipPattern;
            Patterns = patterns;
            Location = location;
        }

        public string? Namespace { get; }

        public string EnumName { get; }

        public string GeneratedTypeName { get; }

        public string EnumTypeName { get; }

        public string EndMemberName { get; }

        public string? SkipPattern { get; }

        public TokenPatternModel[] Patterns { get; }

        public Location? Location { get; }

        public string HintName => $"{GeneratedTypeName}.g.cs";
    }

    private sealed class TokenPatternModel
    {
        public TokenPatternModel(string name, PatternKind kind, string value)
        {
            Name = name;
            Kind = kind;
            Value = value;
        }

        public string Name { get; }

        public PatternKind Kind { get; }

        public string Value { get; }
    }

    private enum PatternKind
    {
        Literal,
        Regex,
    }

    private static class Diagnostics
    {
        public static readonly DiagnosticDescriptor MissingEndToken = new(
            id: "LOGOS001",
            title: "Missing End token",
            messageFormat: "Enum '{0}' must declare an 'End' member so the generated tokenizer can signal end of input",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NoTokenPatterns = new(
            id: "LOGOS002",
            title: "No token patterns found",
            messageFormat: "Enum '{0}' must declare at least one member annotated with [Token] or [Regex]",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultiplePatternAttributes = new(
            id: "LOGOS003",
            title: "Conflicting pattern attributes",
            messageFormat: "Enum member '{0}' on '{1}' cannot be annotated with both [Token] and [Regex]",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidRegexPattern = new(
            id: "LOGOS004",
            title: "Invalid regex pattern",
            messageFormat: "Failed to generate tokenizer for enum '{0}': {1}",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenerationFailed = new(
            id: "LOGOS005",
            title: "Code generation failed",
            messageFormat: "Failed to generate tokenizer for enum '{0}': {1}",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}