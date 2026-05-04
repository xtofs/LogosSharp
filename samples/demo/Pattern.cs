namespace Logos;

using System.Reflection;

record Pattern(string Name, bool IsRegex, string Value)
{
    public static Pattern? FromMemberInfo(MemberInfo m)
    {
        var t = m.GetCustomAttribute<TokenAttribute>();
        if (t != null) return new Pattern(m.Name, false, t.Literal);
        var r = m.GetCustomAttribute<RegexAttribute>();
        if (r != null) return new Pattern(m.Name, true, r.Pattern);
        return null;
    }
}

