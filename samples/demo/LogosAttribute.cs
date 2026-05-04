namespace Logos;

[AttributeUsage(AttributeTargets.Enum)]
internal class LogosAttribute : Attribute
{
    public string Skip { get; set; } = null!;
}