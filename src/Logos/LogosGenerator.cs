namespace Logos;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[Generator]
public sealed class LogosGenerator : IIncrementalGenerator
{
    private const string LogosAttributeName = "Logos.LogosAttribute";
    private const string RegexAttributeName = "Logos.RegexAttribute";
    private const string TokenAttributeName = "Logos.TokenAttribute";
    private const string MatchAttributeName = "Logos.MatchAttribute";

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
        var collection = CollectEnumModel(enumSymbol);
        if (collection.Diagnostic is not null)
        {
            return GenerationInput.FromDiagnostic(collection.Diagnostic);
        }

        return GenerationInput.FromModel(collection.Model!);
    }

    private static EnumModelCollectionResult CollectEnumModel(INamedTypeSymbol enumSymbol)
    {
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
            var matchAttribute = FindAttribute(member, MatchAttributeName);

            var patternAttributeCount = 0;
            if (tokenAttribute is not null)
            {
                patternAttributeCount += 1;
            }

            if (regexAttribute is not null)
            {
                patternAttributeCount += 1;
            }

            if (matchAttribute is not null)
            {
                patternAttributeCount += 1;
            }

            if (patternAttributeCount > 1)
            {
                return EnumModelCollectionResult.FromDiagnostic(Diagnostic.Create(
                    Diagnostics.MultiplePatternAttributes,
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    enumSymbol.Name));
            }

            if (tokenAttribute is not null)
            {
                var val = (string)tokenAttribute.ConstructorArguments[0].Value!;
                var igc = (bool)tokenAttribute.ConstructorArguments[1].Value!;
                var pat = new TokenPatternModel(member.Name, PatternKind.Literal, val, igc, null);
                patterns.Add(pat);
            }
            else if (regexAttribute is not null)
            {
                string patternString = (string)regexAttribute.ConstructorArguments[0].Value!;
                var pat = new TokenPatternModel(member.Name, PatternKind.Regex, patternString, false, null);
                patterns.Add(pat);
            }
            else if (matchAttribute is not null)
            {
                var matcherType = (INamedTypeSymbol)matchAttribute.ConstructorArguments[0].Value!;
                var methodName = (string)matchAttribute.ConstructorArguments[1].Value!;
                var fullyQualifiedCall = $"{matcherType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{methodName}";
                var startsWith = TryGetNamedArgument(matchAttribute, "StartsWith");
                var pat = new TokenPatternModel(member.Name, PatternKind.CustomMatcher, fullyQualifiedCall, false, startsWith);
                patterns.Add(pat);
            }
        }

        if (patterns.Count == 0)
        {
            return EnumModelCollectionResult.FromDiagnostic(Diagnostic.Create(
                Diagnostics.NoTokenPatterns,
                enumSymbol.Locations.FirstOrDefault(),
                enumSymbol.Name));
        }

        if (endMemberName is null)
        {
            return EnumModelCollectionResult.FromDiagnostic(Diagnostic.Create(
                Diagnostics.MissingEndToken,
                enumSymbol.Locations.FirstOrDefault(),
                enumSymbol.Name));
        }

        var logosAttribute = FindAttribute(enumSymbol, LogosAttributeName)!;
        var skipPattern = TryGetNamedArgument(logosAttribute, "Skip");
        var containingNamespace = enumSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : enumSymbol.ContainingNamespace.ToDisplayString();

        return EnumModelCollectionResult.FromModel(new LogosEnumModel(
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
        var dispatchTable = BuildAsciiDispatchTable(model.Patterns);

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
        EmitTokenizer(writer, model, dispatchTable);
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
        writer.Indent += 1;
        writer.WriteLine($"public {model.TokenTypeName}({model.EnumTypeName} kind, ReadOnlySpan<char> value, int start)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("Kind = kind;");
        writer.WriteLine("Value = value;");
        writer.WriteLine("Start = start;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public {model.EnumTypeName} Kind {{ get; }}");
        writer.WriteLine();
        writer.WriteLine("public ReadOnlySpan<char> Value { get; }");
        writer.WriteLine();
        writer.WriteLine("public int Start { get; }");
        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitTokenizer(CodeWriter writer, LogosEnumModel model, int[][] dispatchTable)
    {
        var dispatchConstants = model.Patterns
            .Select((pattern, index) => BuildDispatchConstantName(pattern, index))
            .ToArray();

        writer.WriteLine($"public ref struct {model.TokenizerTypeName}");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("private readonly ReadOnlySpan<char> _src;");
        writer.WriteLine("private int _pos;");
        writer.WriteLine("private bool _emittedEnd;");
        writer.WriteLine();
        EmitMatcherSpecDefinition(writer, model);
        writer.WriteLine();
        EmitDispatchConstants(writer, model, dispatchConstants);
        writer.WriteLine();
        EmitMatcherSpecs(writer, model, dispatchConstants);
        writer.WriteLine();
        EmitDispatchTable(writer, dispatchTable, dispatchConstants);
        writer.WriteLine();
        writer.WriteLine($"public {model.TokenizerTypeName}(ReadOnlySpan<char> src)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("_src = src;");
        writer.WriteLine("_pos = 0;");
        writer.WriteLine("_emittedEnd = false;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public bool TryGetNext(out {model.TokenTypeName} token)");
        writer.WriteLine("{");
        writer.Indent += 1;

        if (!string.IsNullOrWhiteSpace(model.SkipPattern))
        {
            writer.WriteLine("SkipIgnored();");
            writer.WriteLine();
        }

        writer.WriteLine("if (_pos >= _src.Length)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("if (_emittedEnd)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("token = default;");
        writer.WriteLine("return false;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("_emittedEnd = true;");
        writer.WriteLine($"token = new {model.TokenTypeName}({model.EnumTypeName}.{model.EndMemberName}, ReadOnlySpan<char>.Empty, _pos);");
        writer.WriteLine("return true;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("var start = _pos;");
        writer.WriteLine("if ((uint)_src[_pos] < 128u)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("var candidates = Dispatch[_src[_pos]];");
        writer.WriteLine("for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("var matcherId = candidates[candidateIndex];");
        writer.WriteLine("if (TryMatchById(matcherId, _src, ref _pos))");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("var spec = Specs[matcherId];");
        writer.WriteLine($"token = new {model.TokenTypeName}(spec.Kind, _src.Slice(start, _pos - start), start);");
        writer.WriteLine("return true;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine("else");
        writer.WriteLine("{");
        writer.Indent += 1;
        for (var patternIndex = 0; patternIndex < model.Patterns.Length; patternIndex++)
        {
            writer.WriteLine($"if (TryMatchById({dispatchConstants[patternIndex]}, _src, ref _pos))");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.WriteLine($"var spec = Specs[{dispatchConstants[patternIndex]}];");
            writer.WriteLine($"token = new {model.TokenTypeName}(spec.Kind, _src.Slice(start, _pos - start), start);");
            writer.WriteLine("return true;");
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.WriteLine();
        }
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();

        writer.WriteLine("throw new InvalidOperationException($\"Unexpected character '{_src[_pos]}' at position {_pos}.\");");
        writer.Indent -= 1;
        writer.WriteLine("}");

        if (!string.IsNullOrWhiteSpace(model.SkipPattern))
        {
            writer.WriteLine();
            writer.WriteLine("private void SkipIgnored()");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.WriteLine("while (true)");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.WriteLine("var start = _pos;");
            writer.WriteLine($"if (!{model.MatchTypeName}.Skip(_src, ref _pos) || _pos == start)");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.WriteLine("return;");
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.Indent -= 1;
            writer.WriteLine("}");
        }

        writer.WriteLine();
        EmitTryMatchById(writer, model, dispatchConstants);

        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitExtensions(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine($"public static class {model.ExtensionsTypeName}");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine($"extension({model.EnumTypeName})");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine($"public static {model.TokenizerTypeName} CreateTokenizer(ReadOnlySpan<char> source)");
        writer.Indent += 1;
        writer.WriteLine($"=> new {model.TokenizerTypeName}(source);");
        writer.Indent -= 1;
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitMatchClass(CodeWriter writer, LogosEnumModel model, AsciiCharClassBuilder charClasses, RegexPattern? skipPattern)
    {
        writer.WriteLine($"public static class {model.MatchTypeName}");
        writer.WriteLine("{");
        writer.Indent++;

        var definitions = charClasses.Definitions;
        if (definitions.Count > 0)
        {
            foreach (var definition in definitions)
            {
                writer.WriteLine($"private static readonly CharacterSet {definition.ConstantName} = new CharacterSet(0x{definition.Set.Lower:X}UL, 0x{definition.Set.Upper:X}UL);");
            }

            writer.WriteLine();
            EmitCharacterSetDefinition(writer);
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
            else if (pattern.Kind == PatternKind.Regex)
            {
                EmitRegexPattern(writer, pattern.Name, RegexPattern.Parse(pattern.Value), charClasses);
            }
            else
            {
                continue;
            }

            writer.WriteLine();
        }

        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitCharacterSetDefinition(CodeWriter writer)
    {
        writer.WriteMultiLine(
                        """
                // Compact 128-char ASCII bitset used by generated regex/class checks.
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
                        // Bits 0..63 live in _lower, bits 64..127 in _upper.
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

    private static void EmitLiteral(CodeWriter writer, string name, string value, bool ignoreCase)
    {
        writer.WriteLine($"public static bool {name}(ReadOnlySpan<char> src, ref int i)");
        writer.WriteLine("{");
        writer.Indent += 1;

        if (value.Length == 0)
        {
            writer.WriteLine("return true;");
            writer.Indent -= 1;
            writer.WriteLine("}");
            return;
        }

        var literalStr = FormatStringLiteral(value);
        var comparisonStr = ignoreCase ? $", StringComparison.OrdinalIgnoreCase" : "";
        writer.WriteLine($"if (src.Slice(i).StartsWith({literalStr}{comparisonStr}))");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine($"i += {value.Length};");
        writer.WriteLine("return true;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine("return false;");
        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static string FormatStringLiteral(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    private static string BuildMatcherInvocation(TokenPatternModel pattern, LogosEnumModel model)
    {
        if (pattern.Kind == PatternKind.CustomMatcher)
        {
            return pattern.Value;
        }

        return $"{model.MatchTypeName}.{pattern.Name}";
    }

    private static string BuildDispatchConstantName(TokenPatternModel pattern, int index)
    {
        return $"Matcher_{SanitizeIdentifier(pattern.Name)}_{index}";
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "Unknown";
        }

        var buffer = new char[value.Length + 1];
        var length = 0;

        if (!char.IsLetter(value[0]) && value[0] != '_')
        {
            buffer[length++] = '_';
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            buffer[length++] = char.IsLetterOrDigit(c) || c == '_' ? c : '_';
        }

        return new string(buffer, 0, length);
    }

    private static void EmitDispatchConstants(CodeWriter writer, LogosEnumModel model, string[] dispatchConstants)
    {
        writer.WriteLine("// Stable matcher ids used by dispatch, specs, and TryMatchById.");
        for (var index = 0; index < model.Patterns.Length; index++)
        {
            writer.WriteLine($"private const int {dispatchConstants[index]} = {index};");
        }
    }

    private static void EmitMatcherSpecDefinition(CodeWriter writer, LogosEnumModel model)
    {
        writer.WriteLine("// Associates a matcher id with its resulting token kind.");
        writer.WriteLine("private readonly struct MatcherSpec");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine($"public MatcherSpec(int matcherId, {model.EnumTypeName} kind)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("MatcherId = matcherId;");
        writer.WriteLine("Kind = kind;");
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("public int MatcherId { get; }");
        writer.WriteLine();
        writer.WriteLine($"public {model.EnumTypeName} Kind {{ get; }}");
        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitMatcherSpecs(CodeWriter writer, LogosEnumModel model, string[] dispatchConstants)
    {
        writer.WriteLine("// Indexed by matcher id. Lets TryGetNext map a successful matcher back to token kind.");
        writer.WriteLine("private static readonly MatcherSpec[] Specs =");
        writer.WriteLine("[");
        writer.Indent += 1;
        for (var index = 0; index < model.Patterns.Length; index++)
        {
            var pattern = model.Patterns[index];
            writer.WriteLine($"new MatcherSpec({dispatchConstants[index]}, {model.EnumTypeName}.{pattern.Name}),");
        }

        writer.Indent -= 1;
        writer.WriteLine("]; ");
    }

    private static void EmitTryMatchById(CodeWriter writer, LogosEnumModel model, string[] dispatchConstants)
    {
        writer.WriteLine("// Central matcher dispatcher used by both ASCII fast-path and non-ASCII fallback.");
        writer.WriteLine("private static bool TryMatchById(int matcherId, ReadOnlySpan<char> src, ref int pos)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("switch (matcherId)");
        writer.WriteLine("{");
        writer.Indent += 1;
        for (var patternIndex = 0; patternIndex < model.Patterns.Length; patternIndex++)
        {
            var pattern = model.Patterns[patternIndex];
            var invocation = BuildMatcherInvocation(pattern, model);
            writer.WriteLine($"case {dispatchConstants[patternIndex]}:");
            writer.Indent += 1;
            writer.WriteLine($"return {invocation}(src, ref pos);");
            writer.Indent -= 1;
        }

        writer.WriteLine("default:");
        writer.Indent += 1;
        writer.WriteLine("return false;");
        writer.Indent -= 1;
        writer.Indent -= 1;
        writer.WriteLine("}");
        writer.Indent -= 1;
        writer.WriteLine("}");
    }

    private static void EmitDispatchTable(CodeWriter writer, int[][] dispatchTable, string[] dispatchConstants)
    {
        writer.WriteLine("// ASCII dispatch table: first character -> ordered matcher ids to attempt.");
        writer.WriteLine("private static readonly int[][] Dispatch = new int[][]");
        writer.WriteLine("{");
        writer.Indent += 1;
        int charIndex = 0;
        foreach (var row in dispatchTable)
        {
            var ch = (char)(charIndex <= 32 ? charIndex + 0x2400 : charIndex);
            if (row.Length == 0)
            {
                writer.WriteLine($"[], // {ch} ({charIndex})");
            }
            else
            {
                var symbols = row.Select(index => dispatchConstants[index]);
                writer.WriteLine($"[ {string.Join(", ", symbols)} ], // {ch} ({charIndex})");
            }
            charIndex++;
        }

        writer.Indent -= 1;
        writer.WriteLine("};");
    }

    private static int[][] BuildAsciiDispatchTable(TokenPatternModel[] patterns)
    {
        var startsByPattern = patterns
            .Select(BuildPatternAsciiStartSet)
            .ToArray();

        var table = new int[128][];
        for (var c = 0; c < 128; c++)
        {
            var candidates = new List<int>();
            for (var index = 0; index < patterns.Length; index++)
            {
                var starts = startsByPattern[index];
                if (starts is null || starts.Contains(c))
                {
                    candidates.Add(index);
                }
            }

            table[c] = candidates.ToArray();
        }

        return table;
    }

    private static HashSet<int>? BuildPatternAsciiStartSet(TokenPatternModel pattern)
    {
        if (pattern.Kind == PatternKind.Literal)
        {
            return BuildLiteralAsciiStartSet(pattern.Value, pattern.IgnoreCase);
        }

        if (pattern.Kind == PatternKind.Regex)
        {
            return BuildRegexAsciiStartSet(pattern.Value);
        }

        if (pattern.Kind == PatternKind.CustomMatcher)
        {
            return BuildCustomMatcherAsciiStartSet(pattern.StartsWith);
        }

        return null;
    }

    private static HashSet<int>? BuildLiteralAsciiStartSet(string literal, bool ignoreCase)
    {
        if (literal.Length == 0)
        {
            return null;
        }

        var result = new HashSet<int>();
        AddAscii(result, literal[0]);
        if (ignoreCase)
        {
            AddAscii(result, char.ToLowerInvariant(literal[0]));
            AddAscii(result, char.ToUpperInvariant(literal[0]));
        }

        return result;
    }

    private static HashSet<int>? BuildRegexAsciiStartSet(string regexPattern)
    {
        var parsed = RegexPattern.Parse(regexPattern);
        if (parsed.Sequence.Length == 0)
        {
            return null;
        }

        var first = parsed.Sequence[0];
        if (first.Min < 1)
        {
            return null;
        }

        if (first.Atom is SingleChar single)
        {
            var result = new HashSet<int>();
            AddAscii(result, single.Value);
            return result;
        }

        if (first.Atom is CharClass charClass)
        {
            return BuildCharClassAsciiStartSet(charClass);
        }

        return null;
    }

    private static HashSet<int>? BuildCharClassAsciiStartSet(CharClass charClass)
    {
        var allowed = new bool[128];
        if (charClass.Negated)
        {
            for (var i = 0; i < allowed.Length; i++)
            {
                allowed[i] = true;
            }
        }

        foreach (var range in charClass.Ranges)
        {
            var start = Math.Max(range.Start, (char)0);
            var end = Math.Min(range.End, (char)127);
            if (end < start)
            {
                continue;
            }

            for (var c = start; c <= end; c++)
            {
                allowed[c] = !charClass.Negated;
            }
        }

        var result = new HashSet<int>();
        for (var c = 0; c < 128; c++)
        {
            if (allowed[c])
            {
                result.Add(c);
            }
        }

        return result;
    }

    private static HashSet<int>? BuildCustomMatcherAsciiStartSet(string? startsWith)
    {
        if (string.IsNullOrEmpty(startsWith))
        {
            return null;
        }

        var chars = startsWith!;
        var result = new HashSet<int>();
        foreach (var c in chars)
        {
            AddAscii(result, c);
        }

        return result;
    }

    private static void AddAscii(ISet<int> set, char c)
    {
        if (c < 128)
        {
            set.Add(c);
        }
    }

    private static void EmitRegexPattern(CodeWriter writer, string name, RegexPattern pattern, AsciiCharClassBuilder charClasses)
    {
        writer.WriteLine($"public static bool {name}(ReadOnlySpan<char> src, ref int i)");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("var pos = i;");

        foreach (var repetition in pattern.Sequence)
        {
            EmitRepetition(writer, repetition, charClasses);
        }

        writer.WriteLine("i = pos;");
        writer.WriteLine("return true;");
        writer.Indent -= 1;
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
            writer.Indent += 1;
            writer.WriteLine("pos += 1;");
            writer.Indent -= 1;
            writer.WriteLine("}");
            return;
        }

        if (repetition.Min == 0 && repetition.Max is null)
        {
            writer.WriteLine($"while (pos < src.Length && ({condition}))");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.WriteLine("pos += 1;");
            writer.Indent -= 1;
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
            writer.Indent += 1;
            writer.WriteLine("pos += 1;");
            writer.Indent -= 1;
            writer.WriteLine("}");
            return;
        }

        var max = repetition.Max ?? int.MaxValue;
        writer.WriteLine("var count = 0;");
        writer.WriteLine($"while (count < {max} && pos < src.Length && ({condition}))");
        writer.WriteLine("{");
        writer.Indent += 1;
        writer.WriteLine("pos += 1;");
        writer.WriteLine("count += 1;");
        writer.Indent -= 1;
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
        writer.Indent += 1;
        writer.WriteLine("pos = i;");
        writer.WriteLine("return false;");
        writer.Indent -= 1;
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

    private readonly struct EnumModelCollectionResult
    {
        public EnumModelCollectionResult(LogosEnumModel? model, Diagnostic? diagnostic)
        {
            Model = model;
            Diagnostic = diagnostic;
        }

        public LogosEnumModel? Model { get; }

        public Diagnostic? Diagnostic { get; }

        public static EnumModelCollectionResult FromModel(LogosEnumModel model)
        {
            return new EnumModelCollectionResult(model, null);
        }

        public static EnumModelCollectionResult FromDiagnostic(Diagnostic diagnostic)
        {
            return new EnumModelCollectionResult(null, diagnostic);
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

    private sealed class TokenPatternModel(string name, LogosGenerator.PatternKind kind, string value, bool ignoreCase, string? startsWith)
    {
        public string Name { get; } = name;

        public PatternKind Kind { get; } = kind;

        public string Value { get; } = value;

        public bool IgnoreCase { get; } = ignoreCase;

        public string? StartsWith { get; } = startsWith;
    }

    private enum PatternKind
    {
        Literal,
        Regex,
        CustomMatcher,
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
            messageFormat: "Enum '{0}' must declare at least one member annotated with [Token], [Regex], or [Match]",
            category: "Logos",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultiplePatternAttributes = new(
            id: "LOGOS003",
            title: "Conflicting pattern attributes",
            messageFormat: "Enum member '{0}' on '{1}' cannot be annotated with more than one of [Token], [Regex], and [Match]",
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