using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Principal;

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
            "a-zA-Z_",
            "a-zA-Z0-9_",
            "a-zA-Z0-9",
            "0-9",
            "0-9a-fA-F",
            "0-7",
            "01",
            "0-9.eE+-",
            "()[]{}",
            "+-*/\\%",
            "<>=!&|^~",
            ".,;:?!",
            "\"'`",
            "\\/",
            "@#$",
            "foobarbaz",
        };

        // CharClassBuilder.FromCharClassPatterns(patterns,
        //     out var charToClassFlagsLookup,
        //     out var characterClasses
        // );
        Console.WriteLine("Character Class table");
        var characterClasses = CharClassBuilder.CreateClasses(patterns);
        foreach (var (set, (name, ix)) in characterClasses.OrderBy(kv => kv.Value.Item2))
        {
            var chars = CharClassBuilder.CharsFromSet(set);
            Console.WriteLine($"{ix:b32}: {name}. ({chars})");
        }
        Console.WriteLine();

        Console.WriteLine("Character to Class Flags Lookup");
        var classLookup = CharClassBuilder.CreateLookup(patterns, characterClasses);
        for (int i = 0; i < classLookup.Length; i++)
        {
            var ch = i < 32 ? (char)(i + 0x2400) : (char)i; // Use Unicode Control Pictures for non-printable characters
            var names = string.Join(", ", characterClasses.Where(kv => (kv.Key & (UInt128.One << i)) != UInt128.Zero).OrderBy(kv => kv.Value.Item2).Select(kv => kv.Value.Item1));

            Console.WriteLine($"{i,3} '{ch}': {classLookup[i]:b32} ({names})");
        }
    }
}
