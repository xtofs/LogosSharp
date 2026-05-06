namespace Demo;

public class Program
{
    public static void Main()
    {
        var input = """
            Let x = 42 + y
            """;
        var tokenizer = TokenKind.CreateTokenizer(input);

        while (tokenizer.TryGetNext(out var token))
        {
            Console.WriteLine($"{token.Start,3} {token.Kind}: '{token.Value.ToString()}'");
        }
    }
}
