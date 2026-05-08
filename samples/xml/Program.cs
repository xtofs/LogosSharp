namespace XmlSample;

public class Program
{
    public static void Main()
    {
        var input = """
            <?xml version="1.0"?>
            <catalog>
              <book id="b1" />
              <book id='b2'></book>
            </catalog>
            """;

        var tokenizer = XmlTokenKind.CreateTokenizer(input);
        while (tokenizer.TryGetNext(out var token))
        {
            Console.WriteLine($"{token.Start,3} {token.Kind,-24} '{token.Value.ToString()}'");
        }
    }
}
