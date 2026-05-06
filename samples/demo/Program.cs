namespace Demo;

public class Program
{
    public static void Main()
    {
        var input = """
            let x = 42 + y + "hello \"world\""
            in x * 2
            """;
        var tokenizer = TokenKind.CreateTokenizer(input);

        while (tokenizer.TryGetNext(out var token))
        {
            Console.WriteLine($"{token.Start,3} {token.Kind}: '{token.Value.ToString()}'");
        }
    }
}
