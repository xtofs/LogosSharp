namespace Logos;

[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class LogosAttribute : Attribute
{
    public string? Skip { get; set; }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class RegexAttribute : Attribute
{
    public RegexAttribute(string pattern)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public string Pattern { get; }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class TokenAttribute : Attribute
{
    public TokenAttribute(string literal)
    {
        Literal = literal ?? throw new ArgumentNullException(nameof(literal));
    }

    public string Literal { get; }
}