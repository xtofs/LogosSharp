namespace Logos;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


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
                var val = (string)tokenAttribute.ConstructorArguments[0].Value!;
                var igc = (bool)tokenAttribute.ConstructorArguments[1].Value!;
                var pat = new TokenPatternModel(member.Name, PatternKind.Literal, val, igc);
                patterns.Add(pat);
            }
            else if (regexAttribute is not null)
            {
                string patternString = (string)regexAttribute.ConstructorArguments[0].Value!;
                var pat = new TokenPatternModel(member.Name, PatternKind.Regex, patternString, false);
                patterns.Add(pat);
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
            @namespace: containingNamespace,
            enumName: enumSymbol.Name,
            generatedTypeName: BuildGeneratedTypeName(enumSymbol),
            enumTypeName: enumSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            endMemberName: endMemberName,
            skipPattern: skipPattern,
            patterns: patterns.ToArray(),
            location: enumSymbol.Locations.FirstOrDefault()));
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

        EmitToken(writer, model);
        writer.WriteLine();
        EmitTokenizer(writer, model);
        writer.WriteLine();
        EmitMatchClass(writer, model, charClasses, skipPattern);
        writer.WriteLine();
        EmitExtensions(writer, model);

        return writer.ToString();
    }

    private static void EmitToken(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine($"public readonly ref struct {model.TokenTypeName}");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"public {model.TokenTypeName}({model.EnumTypeName} kind, ReadOnlySpan<char> value, int start)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("Kind = kind;");
        writer.WriteLine("Value = value;");
        writer.WriteLine("Start = start;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public {model.EnumTypeName} Kind {{ get; }}");
        writer.WriteLine();
        writer.WriteLine("public ReadOnlySpan<char> Value { get; }");
        writer.WriteLine();
        writer.WriteLine("public int Start { get; }");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitTokenizer(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine($"public ref struct {model.TokenizerTypeName}");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("private readonly ReadOnlySpan<char> _src;");
        writer.WriteLine("private int _pos;");
        writer.WriteLine("private bool _emittedEnd;");
        writer.WriteLine();
        writer.WriteLine($"public {model.TokenizerTypeName}(ReadOnlySpan<char> src)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("_src = src;");
        writer.WriteLine("_pos = 0;");
        writer.WriteLine("_emittedEnd = false;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public bool TryGetNext(out {model.TokenTypeName} token)");
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
        writer.WriteLine("if (_emittedEnd)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("token = default;");
        writer.WriteLine("return false;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("_emittedEnd = true;");
        writer.WriteLine($"token = new {model.TokenTypeName}({model.EnumTypeName}.{model.EndMemberName}, ReadOnlySpan<char>.Empty, _pos);");
        writer.WriteLine("return true;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("var start = _pos;");

        foreach (var pattern in model.Patterns)
        {
            writer.WriteLine($"if ({model.MatchTypeName}.{pattern.Name}(_src, ref _pos))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine($"token = new {model.TokenTypeName}({model.EnumTypeName}.{pattern.Name}, _src.Slice(start, _pos - start), start);");
            writer.WriteLine("return true;");
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
            writer.WriteLine($"if (!{model.MatchTypeName}.Skip(_src, ref _pos) || _pos == start)");
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

    private static void EmitExtensions(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine($"public static class {model.ExtensionsTypeName}");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"extension({model.EnumTypeName})");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"public static {model.TokenizerTypeName} CreateTokenizer(ReadOnlySpan<char> source)");
        writer.Indent();
        writer.WriteLine($"=> new {model.TokenizerTypeName}(source);");
        writer.Outdent();
        writer.Outdent();
        writer.WriteLine("}");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static void EmitMatchClass(CodeWriter writer, LogosEnumModel model, AsciiCharClassBuilder charClasses, RegexPattern? skipPattern)
    {
        writer.WriteLine($"public static class {model.MatchTypeName}");
        writer.WriteLine("{");
        writer.Indent();

        var definitions = charClasses.Definitions;
        if (definitions.Count > 0)
        {
            foreach (var definition in definitions)
            {
                writer.WriteLine($"private static readonly CharacterSet {definition.ConstantName} = new CharacterSet(0x{definition.Set.Lower:X}UL, 0x{definition.Set.Upper:X}UL);");
            }

            writer.WriteLine();
            writer.WriteMultiLine(
                """
                private readonly struct CharacterSet
                {
                    private readonly ulong _lower;
                    private readonly ulong _upper;

                    public CharacterSet(ulong lower, ulong upper)
                    {
                        _lower = lower;
                        _upper = upper;
                    }

                    public bool Contains(char value)
                    {
                        if (value < 64)
                        {
                            return (_lower & (1UL << value)) != 0;
                        }

                        if (value >= 128)
                        {
                            return false;
                        }

                        return (_upper & (1UL << (value - 64))) != 0;
                    }
                }
                """);
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
                EmitLiteral(writer, pattern.Name, pattern.Value, pattern.IgnoreCase);
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

    private static void EmitLiteral(CodeWriter writer, string name, string value, bool ignoreCase)
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

        var literalStr = FormatStringLiteral(value);
        var comparisonStr = ignoreCase ? $", StringComparison.OrdinalIgnoreCase" : "";
        writer.WriteLine($"if (src.Slice(i).StartsWith({literalStr}{comparisonStr}))");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"i += {value.Length};");
        writer.WriteLine("return true;");
        writer.Outdent();
        writer.WriteLine("}");
        writer.WriteLine("return false;");
        writer.Outdent();
        writer.WriteLine("}");
    }

    private static string FormatStringLiteral(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
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
                var lookup = $"{definition.ConstantName}.Contains(src[pos])";
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
        return string.Join("_", parts);
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

        public string TokenTypeName => GeneratedTypeName + "Token";

        public string TokenizerTypeName => GeneratedTypeName + "Tokenizer";

        public string MatchTypeName => GeneratedTypeName + "Match";

        public string ExtensionsTypeName => GeneratedTypeName + "Extensions";

        public string EnumTypeName { get; }

        public string EndMemberName { get; }

        public string? SkipPattern { get; }

        public TokenPatternModel[] Patterns { get; }

        public Location? Location { get; }

        public string HintName => $"{GeneratedTypeName}Logos.g.cs";
    }

    private sealed class TokenPatternModel(string name, LogosGenerator.PatternKind kind, string value, bool ignoreCase)
    {
        public string Name { get; } = name;

        public PatternKind Kind { get; } = kind;

        public string Value { get; } = value;

        public bool IgnoreCase { get; } = ignoreCase;
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