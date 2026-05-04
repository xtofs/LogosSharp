
namespace Logos;


[AttributeUsage(AttributeTargets.Field)]
public sealed class RegexAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
}
