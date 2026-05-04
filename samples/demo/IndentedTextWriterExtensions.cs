namespace Logos;

using System.CodeDom.Compiler;

public static class IndentedTextWriterExtensions
{
    public static void WriteMultiline(this IndentedTextWriter writer, string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            writer.WriteLine(line);
        }
    }
}