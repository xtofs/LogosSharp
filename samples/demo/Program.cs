namespace Demo;

public class Program
{
    public static void Main()
    {
        var tokenizer = TokenKind.CreateTokenizer("Let x = 42 + y");

        while (tokenizer.TryGetNext(out var token))
        {
            Console.WriteLine($"{token.Start,3} {token.Kind}: '{token.Value.ToString()}'");
        }
    }
}
