[AttributeUsage(AttributeTargets.Field)]
public sealed class TokenAttribute(string literal) : Attribute
{
    public string Literal { get; } = literal;
}
