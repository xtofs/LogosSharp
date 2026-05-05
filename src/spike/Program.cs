using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Principal;

//  var patterns = new[]
// {
//     "\r\n \t",
//     "a-zA-Z_",
//     "a-zA-Z0-9_",
//     "0-9",
//     "0-9a-fA-F",
//     "+-*/\\%",
// };

internal class Program
{
    private static void Main(string[] args)
    {
        var patterns = new[]
{
            "\r\n \t",
            "\r\n",
            "a-zA-Z_",
            "a-zA-Z0-9_",
            "0-9",
            "a-zA-Z",
            "a-zA-Z0-9",
            "0-9a-fA-F",
            "0-7",
            "01",
            "0-9.eE+-",
            "()[]{}",
            "*/\\%+-",
            "<>=!&|^~",
            ".,;:?!",
            "\"'`",
            "\\/",
            "@#$",
        };



        var characterClasses = CharClassBuilder.CreateClasses(patterns);
        var max = characterClasses.Max(kv => kv.Value.Name.Length) + 2;

        Console.WriteLine("[Flags]");
        Console.WriteLine("public enum CharacterClass");
        Console.WriteLine("{");
        foreach (var (set, (name, val)) in characterClasses.OrderBy(kv => kv.Value.Value))
        {
            var chars = CharClassBuilder.CharsFromSet(set);
            Console.WriteLine($"    {(name + " =").PadRight(max)} 0x{val:x4}, // '{chars}'");
        }
        Console.WriteLine("}");
        Console.WriteLine();

        var classLookup = CharClassBuilder.CreateLookup(patterns, characterClasses);
        Console.WriteLine("public static class CharClasses");
        Console.WriteLine("{");
        Console.WriteLine("    private static readonly CharacterClass[] Lookup = new CharacterClass[] {");
        var len = classLookup.Length;
        for (int i = 0; i < len; i++)
        {
            var ch = i < 32 ? (char)(i + 0x2400) : (char)i; // Use Unicode Control Pictures for non-printable characters
            var names = string.Join(", ", characterClasses.Where(kv => (kv.Key & UInt128.One << i) != UInt128.Zero).OrderBy(kv => kv.Value.Item2).Select(kv => kv.Value.Item1));
            var comma = i < len - 1 ? "," : " ";
            Console.WriteLine($"        (CharacterClass)0x{classLookup[i]:x8}{comma} // '{ch}' {(string.IsNullOrEmpty(names) ? "" : $"({names})")}");
        }
        Console.WriteLine("    };");
        Console.WriteLine();

        Console.WriteLine("""
            public static CharacterClass FromChar(char ch)
            {
                return ch < 127 ? Lookup[ch] : 0;
            }
        """);


        Console.WriteLine("}");
    }
}