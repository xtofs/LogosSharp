namespace MiniLangSample;

public class Program
{
    public static void Main()
    {
        var input = """
            function fib(n) {
                if (n <= 1) return n;
                var a = fib(n - 1);
                var b = fib(n - 2);
                return a + b;
            }
            """;

        var tokenizer = MiniTokenKind.CreateTokenizer(input);
        while (tokenizer.TryGetNext(out var token))
        {
            Console.WriteLine($"{token.Start,3} {token.Kind,-16} '{token.Value.ToString()}'");
        }
    }
}
